// MA0051: OpenReaderCore and ReadNext contain the full streaming pipeline; fragmenting them
// into micro-methods would obscure the ring-buffer state-machine flow.
#pragma warning disable MA0051
// CA1000: Nested generics are intentional — FileHelperAsyncEngine<T> is the public entry type.
#pragma warning disable CA1000
// MA0004: Library code; ConfigureAwait(false) not used per project pattern (callers control context).
#pragma warning disable MA0004
// CA1710/MA0046: Delegate names and signatures intentionally match FileHelpers compatibility surface.
#pragma warning disable CA1710, MA0046
// CS0067: Event wiring is phased; declarations exist before full usage.
#pragma warning disable CS0067

using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;
using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Blocking-streaming engine for reading and writing records of type <typeparamref name="T"/>.
/// FileHelpers 3.x-compatible API: implements the BeginRead*/ReadNext()/Close() pull model
/// and the BeginWrite*/WriteNext()/Close() push model.
///
/// <para>D-01: This engine streams line-by-line via <see cref="TextReader.ReadLine"/> — it does
/// NOT buffer the entire file. Use this for large files where <see cref="FileHelperEngine{T}"/>
/// batch materialisation is too memory-intensive.</para>
///
/// <para>D-02: <see cref="IEnumerable{T}"/> is implemented so callers can use
/// <c>foreach</c> over an open <c>BeginRead*</c> session. The engine does NOT open the
/// reader itself on <see cref="GetEnumerator"/> — <see cref="BeginReadFile"/>,
/// <see cref="BeginReadStream"/>, or <see cref="BeginReadString"/> must be called first.</para>
///
/// <para>D-03: <see cref="RecycleRecords"/> is reserved for Plan 04 (<c>IRecyclableDescriptor&lt;T&gt;</c>
/// generator extension). The field is present so the property compiles, but the recycling path
/// is never activated in Plan 03 (<c>_recyclable</c> is always null).</para>
/// </summary>
/// <typeparam name="T">
/// A record type annotated with <see cref="DelimitedRecordAttribute"/> or
/// <see cref="FixedLengthRecordAttribute"/>. The source generator must have produced an
/// <see cref="IRecordDescriptor{T}"/> registered in <see cref="FileHelperRegistry{T}"/>
/// via a <c>[ModuleInitializer]</c> before the engine is constructed.
/// </typeparam>
public sealed class FileHelperAsyncEngine<T> : IFileHelperAsyncEngine<T>
{
    private readonly IRecordDescriptor<T> _descriptor;
    private readonly RecordConditionMatcher? _conditionMatcher;
    private readonly bool _implementsNotifyWrite;   // INTF-06

    // LIMIT-02: Compile-time detection of INotifyRead<T>
    private static readonly bool s_hasNotifyRead = typeof(INotifyRead<T>).IsAssignableFrom(typeof(T));

    // ---- Reader state -------------------------------------------------------
    private TextReader? _reader;
    private bool _ownsReader;
    private string? _currentLine;      // pre-fetched content line (null = EOF)
    private int _currentLineNumber;    // physical line number for _currentLine
    private Queue<BufferedLine>? _tailBuffer; // ring buffer for IgnoreLast
    private bool _earlyStop;

    // Phase 18 MULT-02: Multiline field assembly state for streaming mode
    private readonly StringBuilder _multilineBuffer = new();
    private int _multilineStartLineNumber;

    /// <summary>Line terminator used when joining physical lines for multiline quoted fields (MULT-02).</summary>
    private string _readMultilineJoin = "\r\n";

    // ---- Writer state -------------------------------------------------------
    private TextWriter? _writer;
    private bool _ownsWriter;
    private StringWriter? _stringWriter; // non-null only for BeginWriteString path
    private RentedBufferWriter? _writeBuf;

    // ---- RecycleRecords (Plan 04) ------------------------------------------
    // Cast at construction time — returns non-null only for class record types
    // whose descriptor was generated with IRecyclableDescriptor<T> by the source generator.
    // Struct descriptors do not implement IRecyclableDescriptor<T>; cast returns null.
    private readonly IRecyclableDescriptor<T>? _recyclable;
    private T? _recycled;
    private readonly ErrorManager _errorManager = new();

    // ---- Lifecycle ----------------------------------------------------------
    // NOTE: No _disposed flag — this engine is intentionally reusable after Close()/Dispose().
    // Close() is idempotent (nulls reader/writer), so calling Dispose() and then BeginRead*
    // again is a supported pattern (matches FileHelpers 3.x engine behavior).

    /// <summary>
    /// Creates an engine instance backed by the source-generated descriptor for <typeparamref name="T"/>.
    /// CREC-01, CREC-11: Initializes RecordConditionMatcher if ConditionalRecord is configured.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no descriptor is registered for <typeparamref name="T"/> — typically because
    /// the record type is missing <c>[DelimitedRecord]</c>/<c>[FixedLengthRecord]</c> or the
    /// source generator did not run. See <see cref="FileHelperRegistry{T}.Descriptor"/>.
    /// </exception>
    public FileHelperAsyncEngine()
    {
        _descriptor = FileHelperRegistry<T>.Descriptor;
        // Cast once at construction: non-null only for class record types (IRecyclableDescriptor<T>
        // is emitted by the source generator for class types only). Struct types return null here.
        _recyclable = _descriptor as IRecyclableDescriptor<T>;

        // INTF-06: Detect interface implementation once at construction
        _implementsNotifyWrite = typeof(INotifyWrite<T>).IsAssignableFrom(typeof(T));

        // CREC-11: Initialize matcher once per engine instance
        var opts = _descriptor.Options;
        if (opts.Condition != RecordCondition.None && !string.IsNullOrEmpty(opts.ConditionPattern))
        {
            _conditionMatcher = new RecordConditionMatcher(opts.Condition, opts.ConditionPattern);
        }
    }

    // =========================================================================
    // Public properties
    // =========================================================================

    /// <summary>
    /// 1-based line number of the most recently parsed line (counting through header,
    /// empty, and comment lines so it reflects the original file position).
    /// </summary>
    public int LineNumber { get; private set; }

    /// <summary>Number of records yielded since the last <c>BeginRead*</c> or <c>BeginWrite*</c>.</summary>
    public int TotalRecords { get; private set; }

    /// <summary>
    /// Header lines captured from the leading <see cref="IgnoreFirstAttribute"/> lines during
    /// <c>BeginRead*</c>. Setting this property emits it as the first line in <c>BeginWrite*</c>.
    /// </summary>
    public string? HeaderText { get; set; }

    /// <summary>
    /// Footer lines captured from the trailing <see cref="IgnoreLastAttribute"/> lines at EOF.
    /// Setting this property emits it as the last line on <see cref="Close"/>.
    /// </summary>
    public string? FooterText { get; set; }

    /// <summary>Line terminator written after each record. Defaults to <c>"\r\n"</c>.</summary>
    public string NewLineForWrite { get; set; } = "\r\n";

    /// <summary>
    /// Encoding used by <see cref="BeginReadFile"/> and <see cref="BeginWriteFile"/>.
    /// Defaults to <see cref="Encoding.UTF8"/>.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// When <see langword="true"/>, <see cref="ReadNext()"/> reuses a single <typeparamref name="T"/>
    /// instance per call (zero-alloc for class types). Requires <c>IRecyclableDescriptor&lt;T&gt;</c>
    /// — wired in Plan 04. Has no effect in Plan 03 (<c>_recyclable</c> is always null).
    /// </summary>
    public bool RecycleRecords { get; set; }

    /// <summary>The last record returned by <see cref="ReadNext()"/>.</summary>
    public T? LastRecord { get; private set; }

    /// <summary>ERRH-05: Shared error manager instance for this engine.</summary>
    public ErrorManager ErrorManager => _errorManager;

    /// <summary>ERRH-05: Shortcut to <see cref="ErrorManager.ErrorMode"/>.</summary>
    public ErrorMode ErrorMode
    {
        get => _errorManager.ErrorMode;
        set => _errorManager.ErrorMode = value;
    }

    public event BeforeReadHandler<T>? BeforeReadRecord;
    public event AfterReadHandler<T>? AfterReadRecord;
    public event BeforeWriteHandler<T>? BeforeWriteRecord;
    public event AfterWriteHandler<T>? AfterWriteRecord;
    public event ProgressChangeHandler? Progress;

    // =========================================================================
    // Blocking streaming read — ENGI-09
    // =========================================================================

    /// <summary>
    /// ENGI-09: Opens <paramref name="filePath"/> for streaming with the current
    /// <see cref="Encoding"/>. The engine owns the underlying <see cref="StreamReader"/>
    /// and disposes it on <see cref="Close"/>/<see cref="Dispose"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a read session is already open.</exception>
    public void BeginReadFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ThrowIfReaderOpen();
        _readMultilineJoin = Parsing.MultilineLineJoin.DetectFromFileHead(filePath, Encoding);
        _reader = new StreamReader(filePath, Encoding);
        _ownsReader = true;
        OpenReaderCore();
    }

    /// <summary>
    /// ENGI-09: Uses <paramref name="reader"/> for streaming. The engine does NOT dispose
    /// the reader on <see cref="Close"/>/<see cref="Dispose"/> — the caller owns it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a read session is already open.</exception>
    public void BeginReadStream(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ThrowIfReaderOpen();
        _readMultilineJoin = NewLineForWrite;
        _reader = reader;
        _ownsReader = false;
        OpenReaderCore();
    }

    /// <summary>
    /// ENGI-09: Wraps <paramref name="content"/> in a <see cref="StringReader"/> for streaming.
    /// The engine owns the underlying reader.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a read session is already open.</exception>
    public void BeginReadString(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        ThrowIfReaderOpen();
        _readMultilineJoin = Parsing.MultilineLineJoin.Detect(content.AsSpan());
        _reader = new StringReader(content);
        _ownsReader = true;
        OpenReaderCore();
    }

    // =========================================================================
    // Per-record pull — ENGI-10
    // =========================================================================

    /// <summary>
    /// ENGI-10: Returns the next parsed record, or <see langword="null"/> at EOF.
    /// Honors <see cref="IgnoreLastAttribute"/> ring-buffer, <see cref="IgnoreEmptyLinesAttribute"/>,
    /// and <see cref="IgnoreCommentedLinesAttribute"/> filters.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no read session is open.</exception>
    public T? ReadNext()
    {
        ThrowIfNoReader();
        if (_earlyStop)
            return default;
        var opts = _descriptor.Options;

        // Ring-buffer / filter loop — advances until a parseable line is dequeued.
        while (_currentLine is not null)
        {
            string line = _currentLine;
            int lineNumber = _currentLineNumber;

            // Pre-fetch the next raw line before any filtering so the ring buffer always
            // holds the most-recent N lines (where N = opts.IgnoreLast).
            _currentLine = _reader!.ReadLine();
            if (_currentLine is not null)
                _currentLineNumber++;
            else
                _currentLineNumber = 0;

            // IgnoreLast ring buffer: hold lines in the queue; only yield after the
            // queue overflows (the oldest dequeued line is safe to parse).
            if (opts.IgnoreLast > 0)
            {
                _tailBuffer!.Enqueue(new BufferedLine(line, lineNumber));
                if (_tailBuffer.Count <= opts.IgnoreLast)
                    continue; // buffer not yet full — don't yield this line yet
                var dequeued = _tailBuffer.Dequeue();
                line = dequeued.Text;
                lineNumber = dequeued.LineNumber;
            }

            // Line-level filters
            if (opts.IgnoreEmptyLines && line.Length == 0) continue;
            if (opts.CommentMarker != '\0' && line.Length > 0 && line[0] == opts.CommentMarker) continue;

            // CREC-10: ConditionalRecord filtering runs before parse attempt
            if (_conditionMatcher is not null && !_conditionMatcher.ShouldInclude(line.AsSpan()))
                continue;

            // Phase 18 MULT-02: Multiline assembly for quoted fields
            if (opts.MultilineMode == MultilineMode.AllowForRead)
            {
                var assemblyResult = AssembleMultilineLine(line, lineNumber);
                if (!assemblyResult.IsComplete)
                {
                    // Need more lines - skip to next iteration
                    continue;
                }
                line = assemblyResult.AssembledLine ?? line;
                lineNumber = assemblyResult.AssembledLineNumber;
            }

            string activeLine = line;
            if (BeforeReadRecord is not null || s_hasNotifyRead)
            {
                var beforeArgs = new BeforeReadEventArgs<T>(this, activeLine, lineNumber);

                if (BeforeReadRecord is not null)
                {
                    BeforeReadRecord.Invoke(this, beforeArgs);
                }

                if (s_hasNotifyRead)
                {
                    var notifyRecord = _descriptor.CreateDefaultInstance();
                    if (notifyRecord is INotifyRead<T> notifyRead)
                    {
                        notifyRead.BeforeRead(beforeArgs);
                    }
                }

                if (beforeArgs.StopReading)
                {
                    _earlyStop = true;
                    if (opts.IgnoreLast > 0 && _tailBuffer!.Count > 0)
                    {
                        FooterText = JoinBufferedLines(_tailBuffer);
                        _tailBuffer.Clear();
                    }
                    return default;
                }
                if (beforeArgs.SkipThisRecord)
                    continue;
                if (beforeArgs.RecordLineChanged)
                    activeLine = beforeArgs.RecordLine;
            }

            // Parse — Phase 14 MLIN-02: Multi-line record support
            T record;
            try
            {
                EnsureDelimitedFieldCount(activeLine, opts.Delimiter, _descriptor.Fields.Length, _descriptor.Fields);
                // Phase 14 MLIN-02: Pass lineCount=1 for now (single-line records in streaming mode)
                if (RecycleRecords && _recyclable is not null && _recycled is not null)
                {
                    _recyclable.ParseInto(activeLine.AsSpan(), lineCount: 1, _recycled);
                    record = _recycled;
                }
                else
                {
                    record = _descriptor.Parse(activeLine.AsSpan(), lineCount: 1);
                }
            }
            catch (Exception ex)
            {
                // Phase 18 MULT-04: Use multiline start line number for error reporting
                int errorLineNumber = _multilineBuffer.Length > 0 ? _multilineStartLineNumber : lineNumber;
                switch (_errorManager.ErrorMode)
                {
                    case ErrorMode.ThrowException:
                        throw CreateParseContextException(errorLineNumber, activeLine, ex);
                    case ErrorMode.IgnoreAndContinue:
                        continue;
                    case ErrorMode.SaveAndContinue:
                        _errorManager.AddError(new ErrorInfo(errorLineNumber, activeLine, ex));
                        continue;
                }
                break;
            }

            // INTF-05, INTF-07: AfterRead event and interface notification
            if (AfterReadRecord is not null || s_hasNotifyRead)
            {
                var afterArgs = new AfterReadEventArgs<T>(this, record, activeLine, lineNumber);

                if (AfterReadRecord is not null)
                {
                    AfterReadRecord.Invoke(this, afterArgs);
                    if (afterArgs.SkipThisRecord)
                        continue;
                }

                if (s_hasNotifyRead && record is INotifyRead<T> notifyRead)
                {
                    notifyRead.AfterRead(afterArgs);
                    if (afterArgs.SkipThisRecord)
                        continue;
                }
            }

            LineNumber = lineNumber;
            TotalRecords++;
            LastRecord = record;
            _recycled = record;
            if (Progress is not null)
                Progress.Invoke(this, new ProgressEventArgs(TotalRecords, -1, 0L, 0L));
            return record;
        }

        // EOF: drain remaining tail buffer lines into FooterText.
        if (opts.IgnoreLast > 0 && _tailBuffer!.Count > 0)
        {
            FooterText = JoinBufferedLines(_tailBuffer);
            _tailBuffer.Clear();
        }

        return default;
    }

    /// <summary>
    /// ENGI-10: Returns up to <paramref name="count"/> records from the open read session.
    /// The returned array may be shorter than <paramref name="count"/> if EOF is reached.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no read session is open.</exception>
    public T[] ReadNext(int count)
    {
        ThrowIfNoReader();
        var results = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            var record = ReadNext();
            if (record is null) break;
            results.Add(record);
        }
        return results.ToArray();
    }

    /// <summary>
    /// Reads all remaining records from the current position until EOF.
    /// Useful after calling <see cref="ReadNext()"/> a few times and then
    /// wanting to consume the rest in one call.
    /// </summary>
    /// <returns>Array of all remaining parsed records.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no read session is open.</exception>
    public T[] ReadRemaining()
    {
        ThrowIfNoReader();

        var results = new List<T>();
        T? record;
        while ((record = ReadNext()) is not null)
        {
            results.Add(record);
        }
        return results.ToArray();
    }

    // =========================================================================
    // IAsyncEnumerable stub — ENGI-11 (Plan 04)
    // =========================================================================

    /// <summary>
    /// ENGI-11: Returns an <see cref="IAsyncEnumerable{T}"/> that streams records from
    /// <paramref name="filePath"/> via <see cref="PipelineLineReader"/>.
    /// Honors <see cref="IgnoreFirstAttribute"/>, <see cref="IgnoreEmptyLinesAttribute"/>,
    /// <see cref="IgnoreCommentedLinesAttribute"/>, and <see cref="IgnoreLastAttribute"/>.
    /// </summary>
    /// <param name="filePath">Path to the file to read. Must not be null.</param>
    /// <param name="cancellationToken">
    /// Cancellation token. The <c>[EnumeratorCancellation]</c> attribute ensures callers using
    /// <c>.WithCancellation(ct)</c> have their token correctly merged with this parameter.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    public async IAsyncEnumerable<T> ReadAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var multilineJoin = Parsing.MultilineLineJoin.DetectFromFileHead(filePath, Encoding);

        var opts = _descriptor.Options;
        int ignoredFirst = 0;
        int lineNumber = 0;

        // Phase 18 MULT-06: Multiline support for async streaming
        var lineBuffer = new StringBuilder();
        int multilineStartLine = 0;
        bool isBuffering = false;

        // LIMIT-01: tail buffer for IgnoreLast in async streaming
        Queue<T>? tailBuffer = null;
        Queue<int>? tailLineNumbers = null;
        if (opts.IgnoreLast > 0)
        {
            tailBuffer = new Queue<T>(opts.IgnoreLast + 1);
            tailLineNumbers = new Queue<int>(opts.IgnoreLast + 1);
        }

        using var stream = File.OpenRead(filePath);
        await using var lineReader = new PipelineLineReader(stream, Encoding);

        await foreach (var line in lineReader.ReadLinesAsync(cancellationToken))
        {
            lineNumber++;

            // IgnoreFirst: skip the first N lines (header rows)
            if (ignoredFirst < opts.IgnoreFirst)
            {
                ignoredFirst++;
                continue;
            }

            // Phase 18 MULT-06: Multiline assembly for async streaming
            if (opts.MultilineMode == MultilineMode.AllowForRead)
            {
                if (!isBuffering)
                {
                    // Check if this line starts a multiline quoted field
                    if (DelimitedFieldEnumerator.HasUnclosedQuote(line.Span))
                    {
                        lineBuffer.Clear();
                        lineBuffer.Append(line.Span);
                        multilineStartLine = lineNumber;
                        isBuffering = true;
                        continue;  // Need more lines
                    }
                }
                else
                {
                    // Currently assembling multiline
                    lineBuffer.Append(multilineJoin);
                    lineBuffer.Append(line.Span);

                    var assembled = lineBuffer.ToString();
                    if (!DelimitedFieldEnumerator.HasUnclosedQuote(assembled.AsSpan()))
                    {
                        // Quote closed - process assembled line
                        isBuffering = false;
                        var assembledSpan = assembled.AsSpan();

                        // Apply remaining filters and parse
                        if (opts.IgnoreEmptyLines && assembledSpan.IsEmpty)
                        {
                            lineBuffer.Clear();
                            continue;
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        // LIMIT-02: BeforeRead event and interface notification
                        if (BeforeReadRecord is not null || s_hasNotifyRead)
                        {
                            var beforeReadArgs = new BeforeReadEventArgs<T>(this, assembled, multilineStartLine);

                            if (BeforeReadRecord is not null)
                            {
                                BeforeReadRecord.Invoke(this, beforeReadArgs);
                            }

                            if (s_hasNotifyRead)
                            {
                                var notifyRecord = _descriptor.CreateDefaultInstance();
                                if (notifyRecord is INotifyRead<T> notifyReadBefore)
                                {
                                    notifyReadBefore.BeforeRead(beforeReadArgs);
                                }
                            }

                            if (beforeReadArgs.StopReading)
                            {
                                lineBuffer.Clear();
                                yield break;
                            }
                            if (beforeReadArgs.SkipThisRecord)
                            {
                                lineBuffer.Clear();
                                continue;
                            }
                            // RecordLineChanged not applicable for multiline (assembled line is fixed)
                        }

                        // Phase 18: Parse multiline record - error handling outside try-catch for yield compatibility
                        var parseResult = TryParseRecord(assembled, opts.Delimiter, _descriptor.Fields.Length, multilineStartLine, out var parsed);
                        if (!parseResult.Success)
                        {
                            // Error handled in TryParseRecord
                            lineBuffer.Clear();
                            if (parseResult.ShouldContinue)
                                continue;
                            yield break;
                        }

                        lineBuffer.Clear();

                        // INTF-05, INTF-07: AfterRead interface notification
                        if (s_hasNotifyRead && parsed is INotifyRead<T> notifyRead)
                        {
                            var afterArgs = new AfterReadEventArgs<T>(this, parsed, assembled, multilineStartLine);
                            notifyRead.AfterRead(afterArgs);
                            if (afterArgs.SkipThisRecord)
                                continue;
                        }

                        // LIMIT-01: tail buffer logic
                        if (tailBuffer is not null)
                        {
                            tailBuffer.Enqueue(parsed!);
                            tailLineNumbers!.Enqueue(multilineStartLine);
                            if (tailBuffer.Count > opts.IgnoreLast)
                            {
                                var toYield = tailBuffer.Dequeue();
                                LineNumber = tailLineNumbers.Dequeue();
                                TotalRecords++;
                                LastRecord = toYield;
                                yield return toYield;
                            }
                        }
                        else
                        {
                            TotalRecords++;
                            LineNumber = multilineStartLine;
                            LastRecord = parsed;
                            yield return parsed!;
                        }
                    }
                    continue;  // Still buffering or just processed
                }
            }

            // IgnoreEmptyLines (single-line path)
            if (opts.IgnoreEmptyLines && line.Length == 0) continue;

            // CommentMarker
            if (opts.CommentMarker != '\0' && line.Length > 0 && line.Span[0] == opts.CommentMarker) continue;

            // CREC-10: ConditionalRecord filtering runs before parse attempt
            if (_conditionMatcher is not null && !_conditionMatcher.ShouldInclude(line.Span))
                continue;

            cancellationToken.ThrowIfCancellationRequested();

            // LIMIT-02: BeforeRead event and interface notification
            string lineStr = line.Span.ToString();
            if (BeforeReadRecord is not null || s_hasNotifyRead)
            {
                var beforeReadArgs = new BeforeReadEventArgs<T>(this, lineStr, lineNumber);

                if (BeforeReadRecord is not null)
                {
                    BeforeReadRecord.Invoke(this, beforeReadArgs);
                }

                if (s_hasNotifyRead)
                {
                    var notifyRecord = _descriptor.CreateDefaultInstance();
                    if (notifyRecord is INotifyRead<T> notifyReadBefore)
                    {
                        notifyReadBefore.BeforeRead(beforeReadArgs);
                    }
                }

                if (beforeReadArgs.StopReading)
                    yield break;
                if (beforeReadArgs.SkipThisRecord)
                    continue;
                if (beforeReadArgs.RecordLineChanged)
                {
                    lineStr = beforeReadArgs.RecordLine;
                }
            }

            // Phase 18: Parse single-line record - error handling outside try-catch for yield compatibility
            var singleParseResult = TryParseRecord(lineStr, opts.Delimiter, _descriptor.Fields.Length, lineNumber, out var singleParsed);
            if (!singleParseResult.Success)
            {
                if (singleParseResult.ShouldContinue)
                    continue;
                yield break;
            }

            // INTF-05, INTF-07: AfterRead interface notification for async enumerable
            if (s_hasNotifyRead && singleParsed is INotifyRead<T> notifyReadSingle)
            {
                var afterArgs = new AfterReadEventArgs<T>(this, singleParsed!, lineStr, lineNumber);
                notifyReadSingle.AfterRead(afterArgs);
                if (afterArgs.SkipThisRecord)
                    continue;
            }

            // LIMIT-01: tail buffer logic
            if (tailBuffer is not null)
            {
                tailBuffer.Enqueue(singleParsed!);
                tailLineNumbers!.Enqueue(lineNumber);
                if (tailBuffer.Count > opts.IgnoreLast)
                {
                    var toYield = tailBuffer.Dequeue();
                    LineNumber = tailLineNumbers.Dequeue();
                    TotalRecords++;
                    LastRecord = toYield;
                    yield return toYield;
                }
            }
            else
            {
                TotalRecords++;
                LineNumber = lineNumber;
                LastRecord = singleParsed;
                yield return singleParsed!;
            }
        }

        // Phase 18 MULT-04: Handle any remaining buffered multiline content at EOF
        if (isBuffering && lineBuffer.Length > 0)
        {
            var finalLine = lineBuffer.ToString();
            var finalParseResult = TryParseRecord(finalLine, opts.Delimiter, _descriptor.Fields.Length, multilineStartLine, out var finalParsed);
            if (finalParseResult.Success)
            {
                if (tailBuffer is not null)
                {
                    tailBuffer.Enqueue(finalParsed!);
                    tailLineNumbers!.Enqueue(multilineStartLine);
                    if (tailBuffer.Count > opts.IgnoreLast)
                    {
                        var toYield = tailBuffer.Dequeue();
                        LineNumber = tailLineNumbers.Dequeue();
                        TotalRecords++;
                        LastRecord = toYield;
                        yield return toYield;
                    }
                }
                else
                {
                    TotalRecords++;
                    LineNumber = multilineStartLine;
                    LastRecord = finalParsed;
                    yield return finalParsed!;
                }
            }
        }

        // LIMIT-01: capture footer text from remaining tail buffer entries
        if (tailBuffer is not null && tailBuffer.Count > 0)
        {
            var footerLines = new string[tailBuffer.Count];
            int i = 0;
            while (tailBuffer.Count > 0)
            {
                var rec = tailBuffer.Dequeue();
                using var buf = new RentedBufferWriter(256);
                _descriptor.Format(rec, buf);
                footerLines[i++] = buf.WrittenSpan.ToString();
            }
            FooterText = string.Join(NewLineForWrite, footerLines);
        }
    }

    /// <summary>
    /// Phase 18: Helper method to parse a record with error handling outside try-catch
    /// to allow yield return in calling async iterator.
    /// Phase 41: Supports recycling via _recyclable.ParseInto when RecycleRecords is enabled.
    /// </summary>
    private (bool Success, bool ShouldContinue) TryParseRecord(
        string line,
        string delimiter,
        int fieldCount,
        int lineNumber,
        out T? record)
    {
        record = default;

        try
        {
            EnsureDelimitedFieldCount(line, delimiter, fieldCount, _descriptor.Fields);
            if (RecycleRecords && _recyclable is not null)
            {
                if (_recycled is null)
                {
                    _recycled = _descriptor.Parse(line.AsSpan(), lineCount: 1);
                }
                else
                {
                    _recyclable.ParseInto(line.AsSpan(), lineCount: 1, _recycled);
                }
                record = _recycled;
            }
            else
            {
                record = _descriptor.Parse(line.AsSpan(), lineCount: 1);
            }
            return (true, true);
        }
        catch (Exception ex)
        {
            switch (_errorManager.ErrorMode)
            {
                case ErrorMode.ThrowException:
                    throw CreateParseContextException(lineNumber, line, ex);
                case ErrorMode.IgnoreAndContinue:
                    return (false, true);
                case ErrorMode.SaveAndContinue:
                    _errorManager.AddError(new ErrorInfo(lineNumber, line, ex));
                    return (false, true);
                default:
                    return (false, false);
            }
        }
    }

    // =========================================================================
    // Streaming write — ENGI-12
    // =========================================================================

    /// <summary>
    /// ENGI-12: Creates or overwrites <paramref name="filePath"/> for streaming writes.
    /// The engine owns the <see cref="StreamWriter"/> and disposes it on <see cref="Close"/>/<see cref="Dispose"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a write session is already open.</exception>
    public void BeginWriteFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ThrowIfWriterOpen();
        _writer = new StreamWriter(filePath, append: false, Encoding);
        _ownsWriter = true;
        InitWriteSession();
    }

    /// <summary>
    /// Opens <paramref name="filePath"/> in append mode for streaming writes.
    /// Records written via <see cref="WriteNext(T)"/> are appended to the end of the file.
    /// The engine owns the <see cref="StreamWriter"/> and disposes it on <see cref="Close"/>/<see cref="Dispose"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a write session is already open.</exception>
    public void BeginAppendFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ThrowIfWriterOpen();
        _writer = new StreamWriter(filePath, append: true, Encoding);
        _ownsWriter = true;
        InitWriteSession();
    }

    /// <summary>
    /// ENGI-12: Uses <paramref name="writer"/> for streaming writes. The engine does NOT dispose
    /// the writer on <see cref="Close"/>/<see cref="Dispose"/> — the caller owns it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a write session is already open.</exception>
    public void BeginWriteStream(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ThrowIfWriterOpen();
        _writer = writer;
        _ownsWriter = false;
        InitWriteSession();
    }

    /// <summary>
    /// ENGI-12: Accumulates written records into an internal <see cref="StringWriter"/>.
    /// Retrieve the result via <see cref="EndWriteString"/> (which also calls <see cref="Close"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a write session is already open.</exception>
    public void BeginWriteString()
    {
        ThrowIfWriterOpen();
        _stringWriter = new StringWriter();
        _writer = _stringWriter;
        _ownsWriter = true;
        InitWriteSession();
    }

    /// <summary>
    /// Flushes and closes the current write session and returns the accumulated string content.
    /// Only valid after <see cref="BeginWriteString"/>; returns an empty string otherwise.
    /// </summary>
    public string EndWriteString()
    {
        string result = _stringWriter?.ToString() ?? string.Empty;
        Close();
        return result;
    }

    // =========================================================================
    // Per-record push — ENGI-13
    // =========================================================================

    /// <summary>
    /// ENGI-13: Formats <paramref name="record"/> via the descriptor and writes it to the
    /// open write session, followed by <see cref="NewLineForWrite"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no write session is open.</exception>
    public void WriteNext(T record)
    {
        ThrowIfNoWriter();

        // INTF-06, INTF-07: BeforeWrite event and interface notification
        if (BeforeWriteRecord is not null || _implementsNotifyWrite)
        {
            var beforeArgs = new BeforeWriteEventArgs<T>(this, record, TotalRecords + 1);

            if (BeforeWriteRecord is not null)
            {
                BeforeWriteRecord.Invoke(this, beforeArgs);
                if (beforeArgs.SkipThisRecord)
                    return;
            }

            if (_implementsNotifyWrite && record is INotifyWrite<T> notifyWrite)
            {
                notifyWrite.BeforeWrite(beforeArgs);
                if (beforeArgs.SkipThisRecord)
                    return;
            }
        }

        _writeBuf!.Reset();
        _descriptor.Format(record, _writeBuf);

        // INTF-06, INTF-07: AfterWrite event and interface notification
        if (AfterWriteRecord is not null || _implementsNotifyWrite)
        {
            string formattedLine = _writeBuf.WrittenSpan.ToString();
            var afterArgs = new AfterWriteEventArgs<T>(this, record, TotalRecords + 1, formattedLine);

            if (AfterWriteRecord is not null)
            {
                AfterWriteRecord.Invoke(this, afterArgs);
            }

            if (_implementsNotifyWrite && record is INotifyWrite<T> notifyWrite)
            {
                notifyWrite.AfterWrite(afterArgs);
            }

            _writer!.Write(afterArgs.RecordLine);
        }
        else
        {
            _writer!.Write(_writeBuf.WrittenSpan);
        }
        _writer.Write(NewLineForWrite);
        TotalRecords++;
    }

    /// <summary>ENGI-13: Writes each record in <paramref name="records"/> via <see cref="WriteNext(T)"/>.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="records"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no write session is open.</exception>
    public void WriteNext(IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (var record in records)
            WriteNext(record);
    }

    // =========================================================================
    // Lifecycle — ENGI-14
    // =========================================================================

    /// <summary>
    /// ENGI-14: Flushes and closes the current read or write session. Idempotent.
    /// Writes <see cref="FooterText"/> (if set) before flushing the writer.
    /// </summary>
    public void Close()
    {
        // Flush writer session
        if (_writer is not null)
        {
            if (FooterText is not null)
            {
                _writer.Write(FooterText);
                _writer.Write(NewLineForWrite);
            }
            _writer.Flush();
            if (_ownsWriter)
                _writer.Dispose();
            _writer = null;
            _stringWriter = null;
            _ownsWriter = false;
        }

        // Release write buffer
        _writeBuf?.Dispose();
        _writeBuf = null;

        // Release reader
        if (_reader is not null)
        {
            if (_ownsReader)
                _reader.Dispose();
            _reader = null;
            _ownsReader = false;
        }

        // Clear ring buffer
        _tailBuffer?.Clear();
        _currentLine = null;
    }

    /// <summary>
    /// ENGI-14: Calls <see cref="Close"/>. Idempotent and reuse-safe — the engine can be
    /// opened again with a new <c>BeginRead*</c> or <c>BeginWrite*</c> call after disposal.
    /// This matches FileHelpers 3.x engine behavior where the engine object is reused across
    /// multiple file-processing sessions.
    /// </summary>
    public void Dispose()
    {
        Close();
    }

    // =========================================================================
    // IEnumerable<T> — ENGI-14
    // =========================================================================

    /// <summary>
    /// ENGI-14: Iterates the open read session via <see cref="ReadNext()"/>.
    /// A <see cref="BeginReadFile"/>, <see cref="BeginReadStream"/>, or
    /// <see cref="BeginReadString"/> call is required before enumerating.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no read session is open.</exception>
    public IEnumerator<T> GetEnumerator()
    {
        ThrowIfNoReader();
        while (ReadNext() is { } record)
            yield return record;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Consumes IgnoreFirst header lines, initialises the ring buffer for IgnoreLast,
    /// and pre-fetches the first content line into <see cref="_currentLine"/>.
    /// </summary>
    private void OpenReaderCore()
    {
        var opts = _descriptor.Options;
        LineNumber = 0;
        TotalRecords = 0;
        HeaderText = null;
        FooterText = null;
        _currentLineNumber = 0;
        _tailBuffer = null;
        _errorManager.ClearErrors();
        _earlyStop = false;

        // Consume IgnoreFirst header lines and capture them into HeaderText.
        var headerLines = new List<string>(opts.IgnoreFirst);
        for (int i = 0; i < opts.IgnoreFirst; i++)
        {
            string? line = _reader!.ReadLine();
            if (line is null) break;
            headerLines.Add(line);
            _currentLineNumber++;
        }
        if (headerLines.Count > 0)
            HeaderText = string.Join(NewLineForWrite, headerLines);

        // Initialise ring buffer for IgnoreLast (capacity = N+1 to detect overflow).
        if (opts.IgnoreLast > 0)
            _tailBuffer = new Queue<BufferedLine>(opts.IgnoreLast + 1);

        // Pre-read first content line.
        _currentLine = _reader!.ReadLine();
        if (_currentLine is not null) _currentLineNumber++;
    }

    private void InitWriteSession()
    {
        int initialSize = Math.Max(4096, _descriptor.Fields.Length * 64);
        _writeBuf = new RentedBufferWriter(initialSize);
        TotalRecords = 0;

        if (HeaderText is not null)
        {
            _writer!.Write(HeaderText);
            _writer.Write(NewLineForWrite);
        }
    }

    private void ThrowIfReaderOpen()
    {
        if (_reader is not null)
            throw new InvalidOperationException("A read session is already open. Call Close() first.");
    }

    private void ThrowIfNoReader()
    {
        if (_reader is null)
            throw new InvalidOperationException("No read session is open. Call BeginReadFile, BeginReadStream, or BeginReadString first.");
    }

    private void ThrowIfWriterOpen()
    {
        if (_writer is not null)
            throw new InvalidOperationException("A write session is already open. Call Close() first.");
    }

    private void ThrowIfNoWriter()
    {
        if (_writer is null)
            throw new InvalidOperationException("No write session is open. Call BeginWriteFile, BeginWriteStream, or BeginWriteString first.");
    }

    private static void EnsureDelimitedFieldCount(
        string line,
        string delimiter,
        int expectedFieldCount,
        ReadOnlySpan<FieldDescriptor> fields)
    {
        if (string.IsNullOrEmpty(delimiter) || expectedFieldCount <= 0)
            return;

        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].Delimiter is not null)
                return;
        }

        int count = 1;
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && IsDelimiterAt(line, delimiter, i))
            {
                count++;
                i += delimiter.Length - 1;
            }
        }

        if (count != expectedFieldCount)
            throw new FormatException($"Expected {expectedFieldCount} fields but found {count} in line: '{line}'. Check that all delimiters are present and no fields contain unescaped delimiter characters.");
    }

    private static bool IsDelimiterAt(string line, string delimiter, int index)
    {
        if (index + delimiter.Length > line.Length)
            return false;

        return line.AsSpan(index, delimiter.Length).SequenceEqual(delimiter.AsSpan());
    }

    private string JoinBufferedLines(Queue<BufferedLine> lines)
    {
        var values = new List<string>(lines.Count);
        foreach (var line in lines)
            values.Add(line.Text);

        return string.Join(NewLineForWrite, values);
    }

    /// <summary>
    /// Phase 18 MULT-02: Assembles multiline quoted fields in streaming mode.
    /// Checks if the current line starts a multiline field, and if so, buffers additional lines.
    /// </summary>
    private (string? AssembledLine, int AssembledLineNumber, bool IsComplete) AssembleMultilineLine(string line, int lineNumber)
    {
        var opts = _descriptor.Options;

        // If not in multiline mode, return as-is
        if (opts.MultilineMode != MultilineMode.AllowForRead)
        {
            return (line, lineNumber, true);
        }

        // Check if we need to start buffering
        if (_multilineBuffer.Length == 0)
        {
            // Check if this line starts a multiline quoted field
            if (DelimitedFieldEnumerator.HasUnclosedQuote(line.AsSpan()))
            {
                // Start buffering
                _multilineBuffer.Clear();
                _multilineBuffer.Append(line);
                _multilineStartLineNumber = lineNumber;
                return (null, lineNumber, false);  // Need more lines
            }

            // Single-line record
            return (line, lineNumber, true);
        }
        else
        {
            // Already buffering - append this line
                _multilineBuffer.Append(_readMultilineJoin);
                _multilineBuffer.Append(line);

            // Check if quote is now closed
            var assembled = _multilineBuffer.ToString();
            if (!DelimitedFieldEnumerator.HasUnclosedQuote(assembled.AsSpan()))
            {
                // Quote closed - return assembled line
                var result = assembled;
                int resultLineNumber = _currentLineNumber > 0 ? _currentLineNumber : _multilineStartLineNumber;
                _multilineBuffer.Clear();
                return (result, resultLineNumber, true);
            }

            // Still buffering - need more lines
            return (null, lineNumber, false);
        }
    }

    private static FormatException CreateParseContextException(int lineNumber, string line, Exception inner, int columnIndex = -1, string? fieldName = null)
    {
        var columnInfo = columnIndex >= 0 ? $", column {columnIndex + 1}" : "";
        var fieldInfo = fieldName != null ? $", field '{fieldName}'" : "";
        return new FormatException($"Error parsing line {lineNumber}{columnInfo}{fieldInfo}: '{line}'. {inner.Message}", inner);
    }

    private readonly record struct BufferedLine(string Text, int LineNumber);
}
