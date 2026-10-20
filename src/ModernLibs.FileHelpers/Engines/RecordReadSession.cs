using System.Buffers;
using System.Text;
using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>
/// The single record-reading pipeline. Every read path (batch, callback, streaming, async, the
/// pull-model engine, parallel chunks) feeds physical lines into a session, which applies in order:
/// <list type="number">
/// <item>header (<see cref="IgnoreFirstAttribute"/>, original text);</item>
/// <item>footer (<see cref="IgnoreLastAttribute"/>, the last N raw lines, original text);</item>
/// <item>multiline quoted-field assembly (lines inside a quoted field are never filtered);</item>
/// <item>grouping of <see cref="FieldInNewLineAttribute"/> physical lines (a filtered line never starts a
/// group: 3.x applies the filters to the first line of a record only);</item>
/// <item>empty-line / comment / <see cref="ConditionalRecordAttribute"/> filters;</item>
/// <item><c>BeforeReadRecord</c> / <see cref="INotifyRead{T}.BeforeRead"/>;</item>
/// <item>field-count validation and parse, failures routed through <see cref="ErrorMode"/> with the
/// record's first physical line number (for <see cref="FieldInNewLineAttribute"/> records the failing physical
/// line, and after a layout error in a line before the last, the following lines start the next record, as
/// in 3.x);</item>
/// <item><c>AfterReadRecord</c> / <see cref="INotifyRead{T}.AfterRead"/>, progress, max-records.</item>
/// </list>
/// At most one record results from each pushed line. At end of input an unclosed quoted field or an
/// incomplete multi-line record is reported as an error.
/// </summary>
internal sealed class RecordReadSession<T> : IDisposable
{

    private readonly RecordReadCore<T> _core;
    private readonly object _sender;
    private readonly IRecordReadHost<T>? _host;
    private readonly ErrorManager _errors;
    private readonly string _newLine;
    private readonly int _maxRecords;
    private readonly int _lineOffset;
    private readonly bool _hasBeforeRead;
    private readonly bool _hasAfterRead;
    private readonly bool _singleLineRecords;

    // Physical line tracking.
    private int _physicalLines;

    // Header.
    private int _headerRemaining;
    private StringBuilder? _header;

    // Footer: either a ring of the last N raw lines (unknown input length) or a known data end.
    private readonly int _footerLines;
    private char[][]? _ringText;   // pooled per-slot buffers, reused for every line (no per-line string)
    private int[]? _ringLength;
    private int[]? _ringLine;
    private LineEnding[]? _ringEnding;
    private int _ringStart;
    private int _ringCount;
    private int _knownTotalLines = -1;
    private int _knownDataEnd = -1;
    private StringBuilder? _footer;

    // Multiline assembly and FieldInNewLine grouping; lines after a failing line of a FieldInNewLine record.
    private readonly LogicalLineAssembler _assembler = new();
    private readonly PendingLines _pending = new();

    private T _current = default!;
    private bool _completed;

    /// <summary>Creates a session for one read.</summary>
    /// <param name="core">Record type configuration.</param>
    /// <param name="sender">Engine passed as event sender.</param>
    /// <param name="host">Engine hooks; <see langword="null"/> for headless parsing (parallel chunks, multi-record).</param>
    /// <param name="errors">Error sink honouring <see cref="ErrorManager.ErrorMode"/>.</param>
    /// <param name="newLine">Separator used to join multi-line header/footer text.</param>
    /// <param name="maxRecords">Stop after this many records; <c>&lt;= 0</c> = no limit.</param>
    /// <param name="applyHeaderFooter"><see langword="false"/> for chunks of an input whose header/footer were already removed.</param>
    /// <param name="lineOffset">Added to physical line numbers (parallel chunks).</param>
    public RecordReadSession(
        RecordReadCore<T> core,
        object sender,
        IRecordReadHost<T>? host,
        ErrorManager errors,
        string newLine,
        int maxRecords,
        bool applyHeaderFooter = true,
        int lineOffset = 0)
    {
        _core = core;
        _sender = sender;
        _host = host;
        _errors = errors;
        _newLine = newLine;
        _maxRecords = maxRecords;
        _lineOffset = lineOffset;
        _hasBeforeRead = host?.BeforeReadHandler is not null || RecordReadCore<T>.HasNotifyRead;
        _hasAfterRead = host?.AfterReadHandler is not null || RecordReadCore<T>.HasNotifyRead;
        _singleLineRecords = !core.Multiline && core.LinesPerRecord == 1;

        if (applyHeaderFooter)
        {
            _headerRemaining = Math.Max(0, core.Options.IgnoreFirst);
            _footerLines = Math.Max(0, core.Options.IgnoreLast);
        }
    }

    /// <summary>Instance recycling for this read (callback/streaming reads only).</summary>
    public bool AllowRecycle { get; set; }

    /// <summary>The record produced by the last <see cref="Push"/> that returned <see langword="true"/>.</summary>
    public ref T Current => ref _current;

    /// <summary>Records delivered so far.</summary>
    public int RecordCount { get; private set; }

    /// <summary>Last physical line (1-based) of the last delivered record; 0 when none.</summary>
    public int LastLineNumber { get; private set; }

    /// <summary>Reading must stop: <c>StopReading</c> was requested or max-records was reached.</summary>
    public bool Stopped { get; private set; }

    /// <summary>Header lines are still expected.</summary>
    public bool InHeader => _headerRemaining > 0;

    /// <summary>
    /// After <see cref="Stopped"/>, the caller should keep pushing lines only when this is true
    /// (known-length input with footer lines still to collect).
    /// </summary>
    public bool WantsFooterLines => _knownDataEnd >= 0 && _footerLines > 0 && _physicalLines < _knownTotalLines;

    /// <summary>Upper bound of data lines (known-length input), for pre-sizing result buffers.</summary>
    public int DataLineCapacity { get; private set; } = -1;

    /// <summary>
    /// Declares the total number of physical lines of a complete in-memory input so the footer can be
    /// identified without buffering lines.
    /// </summary>
    public void SetKnownLineCount(int totalLines)
    {
        _knownTotalLines = totalLines;
        int header = Math.Min(_headerRemaining, totalLines);
        int footer = Math.Min(_footerLines, totalLines - header);
        _knownDataEnd = totalLines - footer;
        DataLineCapacity = Math.Max(0, _knownDataEnd - header);
    }

    /// <summary>
    /// Pushes one physical line; returns <see langword="true"/> when <see cref="Current"/> holds a new record.
    /// A caller that goes on reading after an error was thrown calls <see cref="ResumePending"/> first.
    /// </summary>
    public bool Push(ReadOnlySpan<char> line, LineEnding ending)
    {
        int physical = ++_physicalLines;

        if (_headerRemaining > 0)
        {
            AppendHeader(line);
            return false;
        }

        if (_knownDataEnd >= 0)
        {
            if (physical > _knownDataEnd)
            {
                AppendFooter(line);
                return false;
            }

            return !Stopped && ProcessDataLine(line, physical + _lineOffset, ending);
        }

        if (Stopped)
            return false;

        if (_footerLines > 0)
            return PushThroughFooterRing(line, physical + _lineOffset, ending);

        return ProcessDataLine(line, physical + _lineOffset, ending);
    }

    /// <summary>
    /// Ends the input: publishes header/footer text and reports a pending unclosed quoted field or an
    /// incomplete multi-line record (unless reading was stopped early). Idempotent.
    /// </summary>
    public void Complete()
    {
        if (_completed)
            return;
        _completed = true;

        if (_header is not null && _headerRemaining > 0)
        {
            _headerRemaining = 0;
            _host?.OnHeaderText(_header.ToString());
        }

        if (!Stopped || _knownDataEnd >= 0)
            PublishFooter();

        if (Stopped)
            return;

        // Lines left by an error thrown on the last pull start the last (incomplete) record.
        ResumePending();
        if (Stopped)
            return;

        var pending = _assembler.TakePending();
        if (pending is not null)
            ReportError(pending.StartLine, pending.Text, pending.Error);
    }

    public void Dispose()
    {
        _assembler.Dispose();
        if (_ringText is null)
            return;

        foreach (var slot in _ringText)
        {
            if (slot is { Length: > 0 })
                ArrayPool<char>.Shared.Return(slot);
        }

        _ringText = null;
        _ringCount = 0;
    }

    private void AppendHeader(ReadOnlySpan<char> line)
    {
        if (_header is null)
            _header = new StringBuilder(line.Length + 2);
        else
            _header.Append(_newLine);
        _header.Append(line);

        if (--_headerRemaining == 0)
            _host?.OnHeaderText(_header.ToString());
    }

    private void AppendFooter(ReadOnlySpan<char> line)
    {
        if (_footer is null)
            _footer = new StringBuilder(line.Length + 2);
        else
            _footer.Append(_newLine);
        _footer.Append(line);
    }

    private void PublishFooter()
    {
        if (_footer is not null)
        {
            _host?.OnFooterText(_footer.ToString());
            return;
        }

        if (_ringCount == 0)
            return;

        var footer = new StringBuilder();
        for (int i = 0; i < _ringCount; i++)
        {
            if (i > 0)
                footer.Append(_newLine);
            int slot = (_ringStart + i) % _footerLines;
            footer.Append(_ringText![slot].AsSpan(0, _ringLength![slot]));
        }

        _host?.OnFooterText(footer.ToString());
    }

    /// <summary>Holds back the last <c>IgnoreLast</c> raw lines; releases the oldest once the ring is full.</summary>
    private bool PushThroughFooterRing(ReadOnlySpan<char> line, int lineNumber, LineEnding ending)
    {
        if (_ringText is null)
        {
            _ringText = new char[_footerLines][];
            _ringLength = new int[_footerLines];
            _ringLine = new int[_footerLines];
            _ringEnding = new LineEnding[_footerLines];
        }

        if (_ringCount < _footerLines)
        {
            int slot = (_ringStart + _ringCount) % _footerLines;
            StoreInRing(slot, line, lineNumber, ending);
            _ringCount++;
            return false;
        }

        // Release the oldest line first (its record is complete or copied by the assembler when this returns),
        // then reuse its slot for the new line. The slot is replaced even when processing throws (a parse
        // error in ThrowException mode, an event handler), so the failing line is not processed again by the
        // next push and the new line is not lost.
        int oldest = _ringStart;
        try
        {
            return ProcessDataLine(_ringText[oldest].AsSpan(0, _ringLength![oldest]), _ringLine![oldest], _ringEnding![oldest]);
        }
        finally
        {
            StoreInRing(oldest, line, lineNumber, ending);
            _ringStart = (_ringStart + 1) % _footerLines;
        }
    }

    private void StoreInRing(int slot, ReadOnlySpan<char> line, int lineNumber, LineEnding ending)
    {
        // The session may have been disposed while the oldest line was processed (Close() called from an
        // event handler): its buffers are back in the pool and nothing reads the ring any more.
        if (_ringText is null)
            return;

        var buffer = _ringText[slot];
        if (buffer is null || buffer.Length < line.Length)
        {
            if (buffer is { Length: > 0 })
                ArrayPool<char>.Shared.Return(buffer);
            buffer = _ringText[slot] = ArrayPool<char>.Shared.Rent(Math.Max(line.Length, 64));
        }

        line.CopyTo(buffer);
        _ringLength![slot] = line.Length;
        _ringLine![slot] = lineNumber;
        _ringEnding![slot] = ending;
    }

    /// <summary>Assembles multiline / multi-physical-line records, then processes complete records.</summary>
    private bool ProcessDataLine(ReadOnlySpan<char> line, int lineNumber, LineEnding ending)
    {
        // Fast path: one physical line per record (no multiline quoting, no FieldInNewLine).
        if (_singleLineRecords)
            return ProcessRecord(line, lineNumber, lineNumber, line.Length, _core.IsEmptyLine(line));

        var assembler = _assembler;
        if (!assembler.Push(line, ending, lineNumber, _core, out var record))
            return false;
        return ProcessRecord(record, assembler.StartLine, assembler.EndLine, assembler.FirstLineLength, assembler.AllLinesEmpty);
    }

    private bool ProcessRecord(ReadOnlySpan<char> record, int startLine, int endLine, int firstLineLength, bool allLinesEmpty)
    {
        if (_core.IsFiltered(record[..firstLineLength], allLinesEmpty))
            return false;

        string? recordText = null;
        T? target = default;
        bool hasTarget = false;
        if (_hasBeforeRead)
        {
            hasTarget = RecordReadCore<T>.HasNotifyRead && _core.TryGetParseTarget(AllowRecycle, out target);
            recordText = RaiseBeforeRead(record, startLine, hasTarget ? target : default);
            if (recordText is null)
                return false;
            record = recordText.AsSpan();
        }

        T parsed;
        try
        {
            parsed = hasTarget ? _core.ParseInto(record, target!, AllowRecycle) : _core.Parse(record, AllowRecycle);
        }
        catch (Exception ex)
        {
            string text = recordText ?? record.ToString();
            int errorLine = startLine;
            if (ex is FileHelpersException located)
            {
                errorLine += located.RecordLineOffset;
                if (located.ResumeOffset >= 0 && located.ResumeOffset <= record.Length)
                    _pending.Hold(record[located.ResumeOffset..], startLine + LineSplitter.CountLineBreaks(record[..located.ResumeOffset], out _));
            }

            // The error comes first (ThrowException throws it, and the lines after the failing one are then read
            // by the next pull), then the lines after it are read again.
            ReportError(errorLine, text, ex);
            return ResumePending();
        }

        if (_hasAfterRead && SkippedByAfterRead(parsed, recordText ?? record.ToString(), endLine))
            return false;

        _current = parsed;
        RecordCount++;
        LastLineNumber = endLine;
        _host?.ProgressHandler?.Invoke(_sender, new ProgressEventArgs(RecordCount, -1, 0L, 0L));
        if (_maxRecords > 0 && RecordCount >= _maxRecords)
            Stopped = true;
        return true;
    }

    /// <summary>The lines after a failing line of a [FieldInNewLine] record wait to be read again.</summary>
    public bool HasPendingLines => _pending.HasLines;

    /// <summary>
    /// A [FieldInNewLine] record failed in one of its lines before the last: like 3.x, the lines after the
    /// failing one are read again as the start of the next record. They are fewer lines than a record, so they
    /// normally only start one; returns <see langword="true"/> when they complete one anyway. A pull reader
    /// whose previous pull threw the error (<see cref="ErrorMode.ThrowException"/>) calls this before it reads
    /// the next line; the lines not read yet stay pending when an error is thrown again.
    /// </summary>
    public bool ResumePending()
    {
        bool produced = false;
        while (!Stopped && _pending.TryRead(out var line, out int lineNumber, out var ending))
            produced |= ProcessDataLine(line, lineNumber, ending);
        return produced;
    }

    /// <summary>
    /// Raises <c>BeforeReadRecord</c> and <see cref="INotifyRead{T}.BeforeRead"/>. Returns the (possibly
    /// replaced) record text to parse, or <see langword="null"/> when the record is skipped or reading stops.
    /// </summary>
    /// <param name="record">Record text.</param>
    /// <param name="startLine">First physical line of the record.</param>
    /// <param name="target">
    /// The instance the record is parsed into (3.x: BeforeRead runs on the record that receives the values).
    /// <see langword="default"/> when the descriptor cannot parse into an existing instance (structs,
    /// constructor-built or init-only records): BeforeRead then runs on a default instance that is discarded.
    /// </param>
    private string? RaiseBeforeRead(ReadOnlySpan<char> record, int startLine, T? target)
    {
        var before = new BeforeReadEventArgs<T>(_sender, record.ToString(), startLine);
        _host?.BeforeReadHandler?.Invoke(_sender, before);
        if (RecordReadCore<T>.HasNotifyRead && (target ?? _core.Descriptor.CreateDefaultInstance()) is INotifyRead<T> notify)
            notify.BeforeRead(before);

        if (before.StopReading)
        {
            Stopped = true;
            return null;
        }

        return before.SkipThisRecord ? null : before.RecordLine;
    }

    /// <summary>Raises <c>AfterReadRecord</c> and <see cref="INotifyRead{T}.AfterRead"/>; true when skipped.</summary>
    private bool SkippedByAfterRead(T parsed, string recordText, int endLine)
    {
        var after = new AfterReadEventArgs<T>(_sender, parsed, recordText, endLine);
        var handler = _host?.AfterReadHandler;
        if (handler is not null)
        {
            handler.Invoke(_sender, after);
            if (after.SkipThisRecord)
                return true;
        }

        if (RecordReadCore<T>.HasNotifyRead && parsed is INotifyRead<T> notify)
        {
            notify.AfterRead(after);
            if (after.SkipThisRecord)
                return true;
        }

        return false;
    }

    private void ReportError(int lineNumber, string recordText, Exception ex)
    {
        switch (_errors.ErrorMode)
        {
            case ErrorMode.ThrowException:
                throw RecordErrors.ForThrow(lineNumber, recordText, ex);
            case ErrorMode.SaveAndContinue:
                _errors.AddError(RecordErrors.ToErrorInfo(lineNumber, recordText, ex));
                break;
        }
    }
}
