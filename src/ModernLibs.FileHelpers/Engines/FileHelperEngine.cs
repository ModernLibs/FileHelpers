// MA0051: ReadCore and WriteCore are single-pipeline methods; keeping each together is more
// readable than fragmenting into micro-methods that would obscure the flow.
#pragma warning disable MA0051
// CA1000: Nested generics are intentional — FileHelperEngine<T> is the public entry type.
#pragma warning disable CA1000
// CA1710/MA0046: Delegate names and signatures intentionally match FileHelpers compatibility surface.
#pragma warning disable CA1710, MA0046
// CS0067: Event wiring is phased; declarations exist before full usage.
#pragma warning disable CS0067

using System.Buffers;
using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Batch-mode engine for reading and writing records of type <typeparamref name="T"/>.
/// FileHelpers 3.x-compatible API: changing the <c>using</c> directive is the only migration step.
///
/// <para>D-01: Read pipeline is synchronous and span-based — <see cref="ReadFile(string)"/> and
/// <see cref="ReadStream(TextReader)"/> materialize the full input into a string before parsing.
/// Phase 8 introduces <c>FileHelperAsyncEngine&lt;T&gt;</c> for streaming large files.</para>
///
/// <para>D-02: <see cref="ReadFile(string)"/> opens a <see cref="StreamReader"/> with the current
/// <see cref="Encoding"/> (default <see cref="Encoding.UTF8"/>) and disposes it after
/// <c>ReadToEnd</c>. Callers who need non-owning stream behavior should use
/// <see cref="ReadStream(TextReader)"/>.</para>
///
/// <para>D-03, D-04, D-16: Write pipeline rents a single <c>char[]</c> from
/// <see cref="ArrayPool{T}.Shared"/> per batch session via a private <see cref="RentedBufferWriter"/>
/// buffer (initial size <c>max(4096, Fields.Length * 64)</c>, doubled on overflow).
/// The descriptor writes field content only; the engine appends <see cref="NewLineForWrite"/>
/// after each record and the optional <see cref="HeaderText"/>/<see cref="FooterText"/>.</para>
///
/// <para>D-07, D-08, D-10: Record-level filters (<see cref="IgnoreFirstAttribute"/>,
/// <see cref="IgnoreLastAttribute"/>, <see cref="IgnoreEmptyLinesAttribute"/>,
/// <see cref="IgnoreCommentedLinesAttribute"/>) are surfaced via
/// <see cref="IRecordDescriptor{T}.Options"/> and honored here without reflection.</para>
///
/// <para>D-12: <see cref="LineNumber"/> tracks the 1-based index of the last successfully parsed
/// line (original file position, counting through skipped header/empty/comment lines).
/// <see cref="TotalRecords"/> equals the number of records returned from the most recent call.
/// Both are reset at the start of every read.</para>
///
/// <para>D-15: <see cref="AppendToFile(string, T)"/> and <see cref="AppendToFile(string, IEnumerable{T})"/>
/// open with <c>FileMode.Append</c> semantics and never emit <see cref="HeaderText"/>/<see cref="FooterText"/>
/// — callers responsible for invariants across the pre-existing tail and appended records.</para>
/// </summary>
/// <typeparam name="T">
/// A record type annotated with <see cref="DelimitedRecordAttribute"/> or
/// <see cref="FixedLengthRecordAttribute"/>. The source generator must have produced an
/// <see cref="IRecordDescriptor{T}"/> that is registered in <see cref="FileHelperRegistry{T}"/>
/// via a <c>[ModuleInitializer]</c> before the engine is constructed.
/// </typeparam>
public sealed class FileHelperEngine<T>
{
    private readonly IRecordDescriptor<T> _descriptor;
    private readonly Parsing.RecordConditionMatcher? _conditionMatcher;
    private readonly bool _implementsNotifyRead;    // INTF-05
    private readonly bool _implementsNotifyWrite;   // INTF-06

    /// <summary>
    /// Creates an engine instance backed by the source-generated descriptor for <typeparamref name="T"/>.
    /// CREC-01, CREC-11: Initializes RecordConditionMatcher if ConditionalRecord is configured.
    /// INTF-05, INTF-06: Detects interface implementation once at construction.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no descriptor is registered for <typeparamref name="T"/> — typically because the
    /// record type is missing <c>[DelimitedRecord]</c>/<c>[FixedLengthRecord]</c> or the source generator
    /// did not run. See <see cref="FileHelperRegistry{T}.Descriptor"/>.
    /// </exception>
    public FileHelperEngine()
    {
        _descriptor = FileHelperRegistry<T>.Descriptor;

        // INTF-05, INTF-06: Detect interface implementation once at construction
        // to avoid repeated typeof operations in hot path
        _implementsNotifyRead = typeof(INotifyRead<T>).IsAssignableFrom(typeof(T));
        _implementsNotifyWrite = typeof(INotifyWrite<T>).IsAssignableFrom(typeof(T));

        // CREC-11: Initialize matcher once per engine instance
        var opts = _descriptor.Options;
        if (opts.Condition != RecordCondition.None && !string.IsNullOrEmpty(opts.ConditionPattern))
        {
            _conditionMatcher = new Parsing.RecordConditionMatcher(opts.Condition, opts.ConditionPattern);
        }
    }

    /// <summary>
    /// ENGI-08 / D-12: 1-based index of the last line that was successfully parsed during the most
    /// recent read. Counts through header (<see cref="IgnoreFirstAttribute"/>), empty, and comment
    /// lines — i.e. reflects the original file position. <c>0</c> after construction and after any
    /// read that produced zero records.
    /// </summary>
    public int LineNumber { get; private set; }

    /// <summary>
    /// ENGI-08 / D-12: Number of records returned from the most recent read, or written by the most
    /// recent write. Reset to <c>0</c> at the start of each call.
    /// </summary>
    public int TotalRecords { get; private set; }

    /// <summary>
    /// D-10: Header captured from the leading <see cref="IgnoreFirstAttribute"/> lines during the
    /// most recent read. <see langword="null"/> when <see cref="IgnoreFirstAttribute"/> is not set
    /// on <typeparamref name="T"/> or when the input had no header lines.
    /// Setting this property emits it as the first line before any records in
    /// <see cref="WriteString"/>, <see cref="WriteStream"/>, and <see cref="WriteFile"/>.
    /// Ignored by <see cref="AppendToFile(string, T)"/> and <see cref="AppendToFile(string, IEnumerable{T})"/> (D-15).
    /// </summary>
    public string? HeaderText { get; set; }

    /// <summary>
    /// D-10: Footer captured from the trailing <see cref="IgnoreLastAttribute"/> lines during the
    /// most recent read. <see langword="null"/> when <see cref="IgnoreLastAttribute"/> is not set
    /// on <typeparamref name="T"/> or when the input had no footer lines.
    /// Setting this property emits it as the last line after all records in
    /// <see cref="WriteString"/>, <see cref="WriteStream"/>, and <see cref="WriteFile"/>.
    /// Ignored by <see cref="AppendToFile(string, T)"/> and <see cref="AppendToFile(string, IEnumerable{T})"/> (D-15).
    /// </summary>
    public string? FooterText { get; set; }

    /// <summary>
    /// D-09: Line terminator used when writing and when joining captured header/footer lines
    /// into <see cref="HeaderText"/>/<see cref="FooterText"/>. Defaults to <c>"\r\n"</c> for
    /// FileHelpers 3.x parity.
    /// </summary>
    public string NewLineForWrite { get; set; } = "\r\n";

    /// <summary>
    /// ENGI-16: Encoding used by <see cref="ReadFile(string)"/>, <see cref="WriteFile"/>, and
    /// <see cref="AppendToFile(string, T)"/>/<see cref="AppendToFile(string, IEnumerable{T})"/>.
    /// Defaults to <see cref="Encoding.UTF8"/>. In-memory string reads/writes are Encoding-independent.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>ERRH-05: Shared error manager instance for this engine.</summary>
    public ErrorManager ErrorManager { get; } = new ErrorManager();

    /// <summary>ERRH-05: Shortcut to <see cref="ErrorManager.ErrorMode"/>.</summary>
    public ErrorMode ErrorMode
    {
        get => ErrorManager.ErrorMode;
        set => ErrorManager.ErrorMode = value;
    }

    public event BeforeReadHandler<T>? BeforeReadRecord;
    public event AfterReadHandler<T>? AfterReadRecord;
    public event BeforeWriteHandler<T>? BeforeWriteRecord;
    public event AfterWriteHandler<T>? AfterWriteRecord;
    public event ProgressChangeHandler? Progress;

    /// <summary>
    /// ENGI-03: Parses all records from <paramref name="content"/>.
    /// <para>WR-08 allocation note: the batch read path allocates one <see cref="string"/> per
    /// input line (into an internal <see cref="List{T}"/>) on top of the final <c>T[]</c> result.
    /// The <em>descriptor's</em> <c>Parse</c> method is zero-alloc for struct records, but the
    /// engine itself is not — allocation-sensitive consumers should use the forthcoming
    /// <c>FileHelperAsyncEngine&lt;T&gt;</c> (Phase 8) which streams rather than materialising.</para>
    /// </summary>
    public T[] ReadString(string content) => ReadCore(content, maxRecords: 0);

    /// <summary>
    /// ENGI-03 + D-11: Parses up to <paramref name="maxRecords"/> records from <paramref name="content"/>.
    /// <c>maxRecords &lt;= 0</c> means "no limit". See <see cref="ReadString(string)"/> for the
    /// per-line allocation note (WR-08).
    /// </summary>
    public T[] ReadString(string content, int maxRecords) => ReadCore(content, maxRecords);

    /// <summary>
    /// ENGI-02: Reads all content from <paramref name="reader"/> via
    /// <see cref="TextReader.ReadToEnd"/> and parses. <paramref name="reader"/> is NOT disposed —
    /// the caller owns its lifetime (matches FileHelpers 3.x).
    /// See <see cref="ReadString(string)"/> for the per-line allocation note (WR-08).
    /// </summary>
    public T[] ReadStream(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadCore(reader.ReadToEnd(), maxRecords: 0);
    }

    /// <summary>ENGI-02 + D-11: <see cref="ReadStream(TextReader)"/> with a record-count cap.</summary>
    public T[] ReadStream(TextReader reader, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadCore(reader.ReadToEnd(), maxRecords);
    }

    /// <summary>
    /// ENGI-01 / D-02: Opens <paramref name="filePath"/> with the current <see cref="Encoding"/>,
    /// reads to end, and parses. The underlying <see cref="StreamReader"/> is disposed.
    /// See <see cref="ReadString(string)"/> for the per-line allocation note (WR-08).
    /// </summary>
    public T[] ReadFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        using var sr = new StreamReader(filePath, Encoding);
        return ReadCore(sr.ReadToEnd(), maxRecords: 0);
    }

    /// <summary>ENGI-01 + D-11: <see cref="ReadFile(string)"/> with a record-count cap.</summary>
    public T[] ReadFile(string filePath, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        using var sr = new StreamReader(filePath, Encoding);
        return ReadCore(sr.ReadToEnd(), maxRecords);
    }

    /// <summary>
    /// ENGI-06: Formats all <paramref name="records"/> into a single string using the descriptor's
    /// Format body, with <see cref="NewLineForWrite"/> after each record and optional
    /// <see cref="HeaderText"/>/<see cref="FooterText"/> framing.
    /// </summary>
    public string WriteString(IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        using var sw = new StringWriter();
        WriteCore(sw, records, includeHeaderFooter: true);
        return sw.ToString();
    }

    /// <summary>
    /// ENGI-05: Writes <paramref name="records"/> to <paramref name="writer"/>. The writer is NOT
    /// disposed by the engine — the caller owns its lifetime.
    /// </summary>
    public void WriteStream(TextWriter writer, IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(records);
        WriteCore(writer, records, includeHeaderFooter: true);
    }

    /// <summary>
    /// ENGI-04: Creates or overwrites <paramref name="filePath"/> and writes <paramref name="records"/>
    /// using the current <see cref="Encoding"/>.
    /// </summary>
    public void WriteFile(string filePath, IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(records);
        using var sw = new StreamWriter(filePath, append: false, Encoding);
        WriteCore(sw, records, includeHeaderFooter: true);
    }

    /// <summary>
    /// ENGI-07 + D-15: Appends a single <paramref name="record"/> to <paramref name="filePath"/> using
    /// <c>FileMode.Append</c>. Does NOT write <see cref="HeaderText"/> or <see cref="FooterText"/>.
    /// <para>IN-08: avoids the 1-element <c>T[]</c> boxing the enumerable overload would incur by
    /// formatting the record directly into a rented buffer. Behaviour is identical to calling
    /// <see cref="AppendToFile(string, IEnumerable{T})"/> with a single-element enumerable.</para>
    /// </summary>
    public void AppendToFile(string filePath, T record)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        TotalRecords = 0;
        using var sw = new StreamWriter(filePath, append: true, Encoding);
        int initialSize = Math.Max(4096, _descriptor.Fields.Length * 64);
        using var buf = new RentedBufferWriter(initialSize);

        // INTF-06, INTF-07: BeforeWrite event and interface notification
        if (BeforeWriteRecord is not null || _implementsNotifyWrite)
        {
            var beforeArgs = new BeforeWriteEventArgs<T>(this, record, 1);

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

        _descriptor.Format(record, buf);

        // INTF-06, INTF-07: AfterWrite event and interface notification
        if (AfterWriteRecord is not null || _implementsNotifyWrite)
        {
            string formattedLine = buf.WrittenSpan.ToString();
            var afterArgs = new AfterWriteEventArgs<T>(this, record, 1, formattedLine);

            if (AfterWriteRecord is not null)
            {
                AfterWriteRecord.Invoke(this, afterArgs);
            }

            if (_implementsNotifyWrite && record is INotifyWrite<T> notifyWrite)
            {
                notifyWrite.AfterWrite(afterArgs);
            }

            sw.Write(afterArgs.RecordLine);
        }
        else
        {
            sw.Write(buf.WrittenSpan);
        }
        sw.Write(NewLineForWrite); // D-04: engine owns the record terminator.
        TotalRecords = 1;
    }

    /// <summary>
    /// ENGI-07 + D-15: Appends all <paramref name="records"/> to <paramref name="filePath"/> using
    /// <c>FileMode.Append</c>. Does NOT write <see cref="HeaderText"/> or <see cref="FooterText"/>.
    /// </summary>
    public void AppendToFile(string filePath, IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(records);
        using var sw = new StreamWriter(filePath, append: true, Encoding);
        WriteCore(sw, records, includeHeaderFooter: false);
    }

    /// <summary>
    /// Core read pipeline. See class-level remarks for the full algorithm (D-01, D-08, D-10, D-12).
    /// </summary>
    private T[] ReadCore(string content, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Step 1: Reset read-side state for this call.
        LineNumber = 0;
        TotalRecords = 0;
        HeaderText = null;
        FooterText = null;
        ErrorManager.ClearErrors();

        var opts = _descriptor.Options;

        // Step 2: Collect all lines as strings — we need random access (IgnoreLast) and the SpanLineReader
        // yields spans that cannot outlive its scope. Per-line allocations are acceptable in the batch
        // path (D-01); Phase 8 streaming will use a different pipeline.
        var lines = new List<string>();
        var reader = new SpanLineReader(content.AsSpan());
        while (reader.TryReadLine(out var lineSpan))
        {
            lines.Add(new string(lineSpan));
        }

        // Step 3: IgnoreFirst — capture header then drop leading lines.
        // IN-06: use CollectionsMarshal.AsSpan + string.Join(ReadOnlySpan) to avoid the
        // LINQ iterator wrapper allocation per call.
        if (opts.IgnoreFirst > 0 && lines.Count > 0)
        {
            int take = Math.Min(opts.IgnoreFirst, lines.Count);
            HeaderText = string.Join(NewLineForWrite, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lines)[..take]);
            lines.RemoveRange(0, take);
        }

        // Step 4: IgnoreLast — capture footer then drop trailing lines.
        if (opts.IgnoreLast > 0 && lines.Count > 0)
        {
            int take = Math.Min(opts.IgnoreLast, lines.Count);
            FooterText = string.Join(NewLineForWrite, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lines)[(lines.Count - take)..]);
            lines.RemoveRange(lines.Count - take, take);
        }

        // Phase 18 MULT-02: Multiline assembly for quoted fields spanning multiple lines
        List<int> logicalStartPhysicalIndices0;
        List<int> logicalEndPhysicalIndices0;
        if (opts.MultilineMode == MultilineMode.AllowForRead)
        {
            var multilineJoin = Parsing.MultilineLineJoin.Detect(content.AsSpan());
            (lines, logicalStartPhysicalIndices0, logicalEndPhysicalIndices0) = AssembleMultilineRecords(lines, multilineJoin);
        }
        else
        {
            logicalStartPhysicalIndices0 = new List<int>(lines.Count);
            logicalEndPhysicalIndices0 = new List<int>(lines.Count);
            for (int i = 0; i < lines.Count; i++)
            {
                logicalStartPhysicalIndices0.Add(i);
                logicalEndPhysicalIndices0.Add(i);
            }
        }

        // Step 5: Parse loop. Track the original line position so LineNumber reflects the file's
        // 1-based index of the last parsed line (D-12) — header lines already consumed from the list
        // still count toward that position.
        var result = new List<T>(lines.Count);
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);
        for (int li = 0; li < lines.Count;)
        {
            int available = lines.Count - li;
            bool incomplete = available < linesPerRecord;
            // Physical lines consumed this iteration: full logical record, or the remainder on a truncated tail.
            int physicalLinesInGroup = incomplete ? available : linesPerRecord;

            try
            {
            string firstLine = lines[li];
            string joinedLogicalLine;
            if (physicalLinesInGroup == 1)
            {
                joinedLogicalLine = firstLine;
            }
            else
            {
                // Join with '\n' so generated Parse multi-line splitting (IndexOfAny CR/LF) matches.
                if (physicalLinesInGroup == 2)
                {
                    joinedLogicalLine = string.Concat(lines[li], "\n", lines[li + 1]);
                }
                else
                {
                    var sb = new StringBuilder();
                    for (int j = 0; j < physicalLinesInGroup; j++)
                    {
                        if (j > 0)
                            sb.Append('\n');
                        sb.Append(lines[li + j]);
                    }

                    joinedLogicalLine = sb.ToString();
                }
            }

            int startLineOneBased = opts.IgnoreFirst + 1 + logicalStartPhysicalIndices0[li];
            int endLineOneBased = opts.IgnoreFirst + 1 + logicalEndPhysicalIndices0[li + physicalLinesInGroup - 1];

            // D-08: IgnoreEmptyLines — skip when every physical line in this logical record is empty.
            // When fewer physical lines remain than required (truncated file), do not skip here — the
            // structural FormatException path must run so ErrorMode can apply.
            if (opts.IgnoreEmptyLines && !incomplete)
            {
                bool allEmpty = true;
                for (int j = 0; j < linesPerRecord; j++)
                {
                    if (lines[li + j].Length != 0)
                    {
                        allEmpty = false;
                        break;
                    }
                }

                if (allEmpty)
                    continue;
            }

            // D-08: CommentMarker — '\0' signals "no marker"; otherwise skip lines beginning with it.
            if (!incomplete && opts.CommentMarker != '\0' && firstLine.Length > 0 && firstLine[0] == opts.CommentMarker)
                continue;

            // CREC-10: ConditionalRecord filtering runs before parse attempt
            if (!incomplete && _conditionMatcher is not null && !_conditionMatcher.ShouldInclude(firstLine.AsSpan()))
                continue;

            string activeLine = joinedLogicalLine;
            // Truncated tail (fewer physical lines than LineCountPerLogicalRecord): same as the old
            // pre-parse throw — do not fire BeforeRead until the structural check in try succeeds.
            if (!incomplete && BeforeReadRecord is not null)
            {
                var beforeArgs = new BeforeReadEventArgs<T>(this, activeLine, startLineOneBased);
                BeforeReadRecord.Invoke(this, beforeArgs);
                if (beforeArgs.StopReading)
                    break;
                if (beforeArgs.SkipThisRecord)
                    continue;
                if (beforeArgs.RecordLineChanged)
                    activeLine = beforeArgs.RecordLine;
            }

            T record;
            try
            {
                if (incomplete)
                {
                    throw new FormatException(
                        $"Expected {linesPerRecord} physical line(s) per logical record; only {available} line(s) remain after line {opts.IgnoreFirst + li + 1}.");
                }

                EnsureDelimitedFieldCount(activeLine, opts.Delimiter, _descriptor.Fields.Length, _descriptor.Fields);
                // Phase 14 MLIN-02: lineCount matches FieldInNewLine logical records (RecordOptions.LineCountPerLogicalRecord).
                record = _descriptor.Parse(activeLine.AsSpan(), lineCount: linesPerRecord);
            }
            catch (Exception ex)
            {
                switch (ErrorManager.ErrorMode)
                {
                    case ErrorMode.ThrowException:
                        throw CreateParseContextException(startLineOneBased, activeLine, ex);
                    case ErrorMode.IgnoreAndContinue:
                        continue;
                    case ErrorMode.SaveAndContinue:
                        ErrorManager.AddError(new ErrorInfo(startLineOneBased, activeLine, ex));
                        continue;
                }
                break;
            }

            // INTF-05, INTF-07: AfterRead event and interface notification
            // Both event handler and interface method receive same args instance
            if (AfterReadRecord is not null || _implementsNotifyRead)
            {
                var afterArgs = new AfterReadEventArgs<T>(this, record, activeLine, endLineOneBased);

                if (AfterReadRecord is not null)
                {
                    AfterReadRecord.Invoke(this, afterArgs);
                    if (afterArgs.SkipThisRecord)
                        continue;
                }

                // INTF-05: Call record's AfterRead method if it implements INotifyRead<T>
                if (_implementsNotifyRead && record is INotifyRead<T> notifyRead)
                {
                    notifyRead.AfterRead(afterArgs);
                    if (afterArgs.SkipThisRecord)
                        continue;
                }
            }

            result.Add(record);
            LineNumber = endLineOneBased;
            if (Progress is not null)
                Progress.Invoke(this, new ProgressEventArgs(result.Count, -1, 0L, 0L));

            // D-11: Early termination after N valid records.
            if (maxRecords > 0 && result.Count >= maxRecords)
                break;
            }
            finally
            {
                li += physicalLinesInGroup;
            }
        }

        TotalRecords = result.Count;
        return result.ToArray();
    }

    /// <summary>
    /// Core write pipeline (D-03, D-04, D-10, D-16). Rents one <see cref="RentedBufferWriter"/>
    /// per session, emits optional <see cref="HeaderText"/>, calls
    /// <see cref="IRecordDescriptor{T}.Format(T, IBufferWriter{char})"/> per record followed by
    /// <see cref="NewLineForWrite"/>, and emits optional <see cref="FooterText"/>.
    /// </summary>
    private void WriteCore(TextWriter writer, IEnumerable<T> records, bool includeHeaderFooter)
    {
        TotalRecords = 0;

        // D-16: Initial buffer size — at least 4096 chars, or 64 chars per field for wide records.
        int initialSize = Math.Max(4096, _descriptor.Fields.Length * 64);
        using var buf = new RentedBufferWriter(initialSize);

        // D-10: Header emission (write pipeline only).
        if (includeHeaderFooter && HeaderText is not null)
        {
            writer.Write(HeaderText);
            writer.Write(NewLineForWrite);
        }

        int count = 0;
        foreach (var record in records)
        {
            // INTF-06, INTF-07: BeforeWrite event and interface notification
            if (BeforeWriteRecord is not null || _implementsNotifyWrite)
            {
                var beforeArgs = new BeforeWriteEventArgs<T>(this, record, count + 1);

                if (BeforeWriteRecord is not null)
                {
                    BeforeWriteRecord.Invoke(this, beforeArgs);
                    if (beforeArgs.SkipThisRecord)
                        continue;
                }

                if (_implementsNotifyWrite && record is INotifyWrite<T> notifyWrite)
                {
                    notifyWrite.BeforeWrite(beforeArgs);
                    if (beforeArgs.SkipThisRecord)
                        continue;
                }
            }

            buf.Reset();
            _descriptor.Format(record, buf);

            // INTF-06, INTF-07: AfterWrite event and interface notification
            if (AfterWriteRecord is not null || _implementsNotifyWrite)
            {
                string formattedLine = buf.WrittenSpan.ToString();
                var afterArgs = new AfterWriteEventArgs<T>(this, record, count + 1, formattedLine);

                if (AfterWriteRecord is not null)
                {
                    AfterWriteRecord.Invoke(this, afterArgs);
                }

                if (_implementsNotifyWrite && record is INotifyWrite<T> notifyWrite)
                {
                    notifyWrite.AfterWrite(afterArgs);
                }

                writer.Write(afterArgs.RecordLine);
            }
            else
            {
                writer.Write(buf.WrittenSpan);
            }
            writer.Write(NewLineForWrite); // D-04: engine owns the record terminator.
            count++;
        }

        // D-10: Footer emission.
        if (includeHeaderFooter && FooterText is not null)
        {
            writer.Write(FooterText);
            writer.Write(NewLineForWrite);
        }

        TotalRecords = count;
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
            throw new FormatException($"Expected {expectedFieldCount} fields but found {count}.");
    }

    private static bool IsDelimiterAt(string line, string delimiter, int index)
    {
        if (index + delimiter.Length > line.Length)
            return false;

        return line.AsSpan(index, delimiter.Length).SequenceEqual(delimiter.AsSpan());
    }

    /// <summary>
    /// Phase 18 MULT-02: Assembles multiline quoted records from individual lines.
    /// Lines with unclosed quotes are buffered until the quote closes.
    /// </summary>
    /// <returns>
    /// Assembled logical lines and, for each, the 0-based indices of the first and last physical
    /// source lines that contributed to that logical line (for error start line vs. success end line).
    /// </returns>
    private static (List<string> Lines, List<int> StartPhysicalIndices0, List<int> EndPhysicalIndices0)
        AssembleMultilineRecords(
        List<string> lines,
        string newLine)
    {
        var result = new List<string>(lines.Count);
        var startPhysical = new List<int>(lines.Count);
        var endPhysical = new List<int>(lines.Count);
        var buffer = new StringBuilder();
        int bufferStartPhysical0 = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

            if (buffer.Length == 0)
            {
                // Not currently buffering - check if this line starts multiline
                if (DelimitedFieldEnumerator.HasUnclosedQuote(line.AsSpan()))
                {
                    buffer.Append(line);
                    bufferStartPhysical0 = i;
                    // Continue to next line without adding to result
                }
                else
                {
                    // Single line record - add directly
                    result.Add(line);
                    startPhysical.Add(i);
                    endPhysical.Add(i);
                }
            }
            else
            {
                // Currently buffering - append this line
                buffer.Append(newLine);
                buffer.Append(line);

                // Check if quote is now closed
                var assembled = buffer.ToString();
                if (!DelimitedFieldEnumerator.HasUnclosedQuote(assembled.AsSpan()))
                {
                    // Quote closed - add assembled line to result
                    result.Add(assembled);
                    startPhysical.Add(bufferStartPhysical0);
                    endPhysical.Add(i);
                    buffer.Clear();
                    bufferStartPhysical0 = -1;
                }
                // Else: continue buffering
            }
        }

        // Handle unclosed quote at EOF (will trigger ErrorMode during parse)
        if (buffer.Length > 0)
        {
            result.Add(buffer.ToString());
            startPhysical.Add(bufferStartPhysical0 >= 0 ? bufferStartPhysical0 : lines.Count - 1);
            endPhysical.Add(lines.Count - 1);
        }

        return (result, startPhysical, endPhysical);
    }

    private static FormatException CreateParseContextException(int lineNumber, string line, Exception inner)
    {
        return new FormatException($"Error parsing line {lineNumber}: '{line}'. {inner.Message}", inner);
    }

}
