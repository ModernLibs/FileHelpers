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
using System.Runtime.CompilerServices;
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
public sealed class FileHelperEngine<T> : IFileHelperEngine<T>
{
    // OPT-04: static readonly — JIT constant-folds this per generic instantiation,
    // eliminating the dead branch entirely in the hot path.
    private static readonly bool s_isStruct = typeof(T).IsValueType;

    // LIMIT-02: Compile-time detection of INotifyRead<T> — constant-folded per T,
    // eliminating the dead branch entirely for Native AOT.
    private static readonly bool s_hasNotifyRead = typeof(INotifyRead<T>).IsAssignableFrom(typeof(T));

    private readonly IRecordDescriptor<T> _descriptor;
    private readonly IRecyclableDescriptor<T>? _recyclable;
    private readonly Parsing.RecordConditionMatcher? _conditionMatcher;
    private readonly bool _implementsNotifyWrite;   // INTF-06
    private T? _recycled;

    /// <summary>
    /// Creates an engine instance backed by the source-generated descriptor for <typeparamref name="T"/>.
    /// Uses <see cref="Encoding.UTF8"/> for file I/O operations.
    /// CREC-01, CREC-11: Initializes RecordConditionMatcher if ConditionalRecord is configured.
    /// INTF-05, INTF-06: Detects interface implementation once at construction.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no descriptor is registered for <typeparamref name="T"/> — typically because the
    /// record type is missing <c>[DelimitedRecord]</c>/<c>[FixedLengthRecord]</c> or the source generator
    /// did not run. See <see cref="FileHelperRegistry{T}.Descriptor"/>.
    /// </exception>
    public FileHelperEngine()
        : this(Encoding.UTF8)
    {
    }

    /// <summary>
    /// Creates an engine instance with the specified encoding for file I/O operations.
    /// 3.x parity: <c>new FileHelperEngine&lt;T&gt;(Encoding.GetEncoding(0))</c> restores
    /// the legacy system-default ANSI code page behavior.
    /// </summary>
    /// <param name="encoding">Encoding used by <see cref="ReadFile(string)"/>, <see cref="WriteFile"/>, and <see cref="AppendToFile(string,T)"/>.</param>
    public FileHelperEngine(Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        Encoding = encoding;

        _descriptor = FileHelperRegistry<T>.Descriptor;
        _recyclable = _descriptor as IRecyclableDescriptor<T>;

        // INTF-06: Detect interface implementation once at construction
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

    /// <summary>
    /// CREC-04: When <see langword="true"/>, callback-based read methods reuse a single
    /// <typeparamref name="T"/> instance per callback invocation (zero-alloc for class types).
    /// Requires <c>IRecyclableDescriptor&lt;T&gt;</c> — emitted by the source generator for
    /// class record types without init-only fields. Has no effect for struct types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Important:</strong> The callback receives the same reference each time.
    /// If the caller needs to retain record data, it must copy the values before the
    /// next callback invocation.
    /// </para>
    /// </remarks>
    public bool RecycleRecords { get; set; }

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
    /// OPT-06: Parses all records from a <see cref="ReadOnlySpan{T}"/> of characters.
    /// Zero content allocation — the span is used directly for line boundary tracking
    /// and field slicing. Ideal for callers with span-based input (MemoryMappedFile,
    /// pooled buffers, pipeline readers).
    /// </summary>
    public T[] ReadSpan(ReadOnlySpan<char> content) => ReadCoreSpan(content, maxRecords: 0);

    /// <summary>
    /// OPT-06: Parses up to <paramref name="maxRecords"/> records from a <see cref="ReadOnlySpan{T}"/>.
    /// Zero content allocation. <c>maxRecords &lt;= 0</c> means "no limit".
    /// </summary>
    public T[] ReadSpan(ReadOnlySpan<char> content, int maxRecords) => ReadCoreSpan(content, maxRecords);

    /// <summary>
    /// OPT-11: Zero-allocation callback-based read from a <see cref="ReadOnlySpan{T}"/>.
    /// Invokes <paramref name="callback"/> for each parsed record without building a result array.
    /// Returns the total number of records processed.
    /// Ideal for streaming scenarios where records are consumed inline (e.g., database inserts,
    /// transformation pipelines) without needing to hold all records in memory.
    /// </summary>
    /// <param name="content">The input content as a character span.</param>
    /// <param name="callback">Called once per successfully parsed record.</param>
    /// <returns>The number of records that were parsed and passed to the callback.</returns>
    public int ReadSpan(ReadOnlySpan<char> content, Action<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return ReadCoreSpanCallback(content, callback, maxRecords: 0);
    }

    /// <summary>
    /// OPT-11: Zero-allocation callback-based read with a record-count cap.
    /// </summary>
    public int ReadSpan(ReadOnlySpan<char> content, Action<T> callback, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return ReadCoreSpanCallback(content, callback, maxRecords);
    }

    /// <summary>
    /// OPT-11: Zero-allocation callback-based read from a string.
    /// Invokes <paramref name="callback"/> for each parsed record without building a result array.
    /// </summary>
    public int ReadString(string content, Action<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(content);
        return ReadCoreCallback(content, callback, maxRecords: 0);
    }

    /// <summary>
    /// OPT-11: Zero-allocation callback-based read from a string with a record-count cap.
    /// </summary>
    public int ReadString(string content, Action<T> callback, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(content);
        return ReadCoreCallback(content, callback, maxRecords);
    }

    /// <summary>
    /// OPT-11 + REF-01: Zero-allocation callback-based read from a <see cref="ReadOnlySpan{T}"/>
    /// that passes each record by reference to avoid struct copying.
    /// Returns the total number of records processed.
    /// </summary>
    /// <param name="content">The input content as a character span.</param>
    /// <param name="callback">Called once per successfully parsed record, passed by reference.</param>
    /// <returns>The number of records that were parsed and passed to the callback.</returns>
    public int ReadSpan(ReadOnlySpan<char> content, SpanRecordCallback<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return ReadCoreSpanRefCallback(content, callback, maxRecords: 0);
    }

    /// <summary>OPT-11 + REF-01: <see cref="ReadSpan(ReadOnlySpan{char}, SpanRecordCallback{T})"/> with a record-count cap.</summary>
    public int ReadSpan(ReadOnlySpan<char> content, SpanRecordCallback<T> callback, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return ReadCoreSpanRefCallback(content, callback, maxRecords);
    }

    /// <summary>
    /// OPT-11 + REF-01: Zero-allocation callback-based read from a string that passes
    /// each record by reference to avoid struct copying.
    /// </summary>
    public int ReadString(string content, SpanRecordCallback<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(content);
        return ReadCoreRefCallback(content, callback, maxRecords: 0);
    }

    /// <summary>OPT-11 + REF-01: <see cref="ReadString(string, SpanRecordCallback{T})"/> with a record-count cap.</summary>
    public int ReadString(string content, SpanRecordCallback<T> callback, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(content);
        return ReadCoreRefCallback(content, callback, maxRecords);
    }

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
    /// reads to end, and parses via the optimized span pipeline. The underlying file handle is
    /// disposed after reading.
    /// <para>OPT-12: Uses <see cref="File.ReadAllText(string, Encoding)"/> → <see cref="ReadSpan(ReadOnlySpan{char})"/>
    /// to avoid the <see cref="StreamReader.ReadToEnd"/> intermediate buffer growth overhead.</para>
    /// </summary>
    public T[] ReadFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var content = File.ReadAllText(filePath, Encoding);
        return ReadSpan(content.AsSpan(), maxRecords: 0);
    }

    /// <summary>ENGI-01 + D-11: <see cref="ReadFile(string)"/> with a record-count cap.</summary>
    public T[] ReadFile(string filePath, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var content = File.ReadAllText(filePath, Encoding);
        return ReadSpan(content.AsSpan(), maxRecords);
    }

    /// <summary>
    /// OPT-12: Streaming file read with callback — zero string allocation, zero T[] allocation.
    /// Reads the file in chunks via <see cref="StreamReader"/>, detects line boundaries,
    /// parses each record, and invokes <paramref name="callback"/> inline.
    /// Returns the total number of records processed.
    /// Ideal for large files where holding the entire content in memory is undesirable.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="callback">Called once per successfully parsed record.</param>
    /// <returns>The number of records that were parsed and passed to the callback.</returns>
    public int ReadFile(string filePath, Action<T> callback)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(callback);
        using var sr = new StreamReader(filePath, Encoding);
        return ReadCoreStreamCallback(sr, callback, maxRecords: 0);
    }

    /// <summary>OPT-12: <see cref="ReadFile(string, Action{T})"/> with a record-count cap.</summary>
    public int ReadFile(string filePath, Action<T> callback, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(callback);
        using var sr = new StreamReader(filePath, Encoding);
        return ReadCoreStreamCallback(sr, callback, maxRecords);
    }

    /// <summary>
    /// OPT-12: Async streaming file read with callback.
    /// Reads the file asynchronously in chunks, parses records, and invokes <paramref name="callback"/> inline.
    /// Zero string allocation, zero T[] allocation. Cancellation supported.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="callback">Called once per successfully parsed record.</param>
    /// <param name="cancellationToken">Token to cancel the read operation.</param>
    /// <returns>The number of records that were parsed and passed to the callback.</returns>
    public async Task<int> ReadFileAsync(string filePath, Action<T> callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(callback);
        using var sr = new StreamReader(filePath, Encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 4096);
        return await ReadCoreStreamCallbackAsync(sr, callback, maxRecords: 0, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>OPT-12: <see cref="ReadFileAsync(string, Action{T}, CancellationToken)"/> with a record-count cap.</summary>
    public async Task<int> ReadFileAsync(string filePath, Action<T> callback, int maxRecords, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(callback);
        using var sr = new StreamReader(filePath, Encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 4096);
        return await ReadCoreStreamCallbackAsync(sr, callback, maxRecords, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// STRM-01: Asynchronous streaming read that yields records one at a time via
    /// <see cref="IAsyncEnumerable{T}"/>. Uses <see cref="Parsing.PipelineLineReader"/> for
    /// memory-efficient file reading — never materializes the full file content.
    /// Honors <see cref="IgnoreFirstAttribute"/>, <see cref="IgnoreLastAttribute"/>,
    /// <see cref="IgnoreEmptyLinesAttribute"/>, and <see cref="IgnoreCommentedLinesAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Allocation note:</strong> For class record types, each record is a new heap
    /// allocation. Enable <see cref="FileHelperAsyncEngine{T}.RecycleRecords"/> on
    /// <c>FileHelperAsyncEngine&lt;T&gt;</c> for zero-alloc streaming.
    /// </para>
    /// </remarks>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="cancellationToken">Token to cancel the async enumeration.</param>
    /// <returns>An async enumerable of parsed records.</returns>
    public async IAsyncEnumerable<T> ReadAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var opts = _descriptor.Options;
        int ignoredFirst = 0;
        int lineNumber = 0;
        int totalRecords = 0;

        var lineBuffer = new StringBuilder();
        bool isBuffering = false;
        int multilineStartLine = 0;

        // LIMIT-01: tail buffer for IgnoreLast in async streaming
        Queue<T>? tailBuffer = null;
        if (opts.IgnoreLast > 0)
            tailBuffer = new Queue<T>(opts.IgnoreLast + 1);

        using var stream = File.OpenRead(filePath);
        var lineReader = new Parsing.PipelineLineReader(stream, Encoding);
        try
        {
        await foreach (var line in lineReader.ReadLinesAsync(cancellationToken).ConfigureAwait(false))
        {
            lineNumber++;

            if (ignoredFirst < opts.IgnoreFirst)
            {
                ignoredFirst++;
                continue;
            }

            if (opts.MultilineMode == MultilineMode.AllowForRead)
            {
                if (!isBuffering)
                {
                    if (DelimitedFieldEnumerator.HasUnclosedQuote(line.Span))
                    {
                        lineBuffer.Clear();
                        lineBuffer.Append(line.Span);
                        multilineStartLine = lineNumber;
                        isBuffering = true;
                        continue;
                    }
                }
                else
                {
                    lineBuffer.Append(NewLineForWrite);
                    lineBuffer.Append(line.Span);

                    var assembled = lineBuffer.ToString();
                    if (!DelimitedFieldEnumerator.HasUnclosedQuote(assembled.AsSpan()))
                    {
                        isBuffering = false;
                        var assembledSpan = assembled.AsSpan();

                        if (opts.IgnoreEmptyLines && assembledSpan.IsEmpty)
                        {
                            lineBuffer.Clear();
                            continue;
                        }

                        if (opts.CommentMarker != '\0' && assembledSpan.Length > 0 && assembledSpan[0] == opts.CommentMarker)
                        {
                            lineBuffer.Clear();
                            continue;
                        }

                        if (_conditionMatcher is not null && !_conditionMatcher.ShouldInclude(assembledSpan))
                        {
                            lineBuffer.Clear();
                            continue;
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        var parseResult = TryParseRecordForAsyncStream(assembledSpan, assembled, multilineStartLine, opts);
                        if (parseResult == null)
                        {
                            lineBuffer.Clear();
                            yield break;
                        }

                        lineBuffer.Clear();

                        // LIMIT-01: tail buffer logic
                        if (tailBuffer is not null)
                        {
                            tailBuffer.Enqueue(parseResult);
                            if (tailBuffer.Count > opts.IgnoreLast)
                            {
                                var toYield = tailBuffer.Dequeue();
                                totalRecords++;
                                LineNumber = multilineStartLine;
                                yield return toYield;
                            }
                        }
                        else
                        {
                            totalRecords++;
                            LineNumber = multilineStartLine;
                            yield return parseResult;
                        }
                    }
                    continue;
                }
            }

            if (opts.IgnoreEmptyLines && line.Length == 0) continue;

            if (opts.CommentMarker != '\0' && line.Length > 0 && line.Span[0] == opts.CommentMarker) continue;

            if (_conditionMatcher is not null && !_conditionMatcher.ShouldInclude(line.Span)) continue;

            cancellationToken.ThrowIfCancellationRequested();

            var singleParseResult = TryParseRecordForAsyncStream(line.Span, line.Span.ToString(), lineNumber, opts);
            if (singleParseResult == null)
            {
                yield break;
            }

            // LIMIT-01: tail buffer logic
            if (tailBuffer is not null)
            {
                tailBuffer.Enqueue(singleParseResult);
                if (tailBuffer.Count > opts.IgnoreLast)
                {
                    var toYield = tailBuffer.Dequeue();
                    totalRecords++;
                    LineNumber = lineNumber;
                    yield return toYield;
                }
            }
            else
            {
                totalRecords++;
                LineNumber = lineNumber;
                yield return singleParseResult;
            }
        }

        if (isBuffering && lineBuffer.Length > 0)
        {
            var finalLine = lineBuffer.ToString();
            var finalSpan = finalLine.AsSpan();

            if (!DelimitedFieldEnumerator.HasUnclosedQuote(finalSpan))
            {
                var finalResult = TryParseRecordForAsyncStream(finalSpan, finalLine, multilineStartLine, opts);
                if (finalResult != null)
                {
                    if (tailBuffer is not null)
                    {
                        tailBuffer.Enqueue(finalResult);
                        if (tailBuffer.Count > opts.IgnoreLast)
                        {
                            var toYield = tailBuffer.Dequeue();
                            totalRecords++;
                            LineNumber = multilineStartLine;
                            yield return toYield;
                        }
                    }
                    else
                    {
                        totalRecords++;
                        LineNumber = multilineStartLine;
                        yield return finalResult;
                    }
                }
            }
        }

        // LIMIT-01: capture footer text from remaining tail buffer entries
        if (tailBuffer is not null && tailBuffer.Count > 0)
        {
            var footerRecords = new string[tailBuffer.Count];
            int i = 0;
            while (tailBuffer.Count > 0)
            {
                var rec = tailBuffer.Dequeue();
                using var buf = new RentedBufferWriter(256);
                _descriptor.Format(rec, buf);
                footerRecords[i++] = buf.WrittenSpan.ToString();
            }
            FooterText = string.Join(NewLineForWrite, footerRecords);
        }

        TotalRecords = totalRecords;
        }
        finally
        {
            await lineReader.DisposeAsync().ConfigureAwait(false);
        }
    }

    private T? TryParseRecordForAsyncStream(ReadOnlySpan<char> lineSpan, string lineString, int startLine, RecordOptions opts)
    {
        try
        {
            ValidateDelimitedFieldCount(lineSpan, opts.Delimiter, _descriptor.Fields.Length, _descriptor.Fields);
            return _descriptor.Parse(lineSpan, lineCount: opts.LineCountPerLogicalRecord);
        }
        catch (Exception ex)
        {
            switch (ErrorManager.ErrorMode)
            {
                case ErrorMode.ThrowException:
                    throw CreateParseContextException(startLine, lineString, ex);
                case ErrorMode.IgnoreAndContinue:
                    return default;
                case ErrorMode.SaveAndContinue:
                    ErrorManager.AddError(new ErrorInfo(startLine, lineString, ex));
                    return default;
            }
            return default;
        }
    }

    /// <summary>
    /// STRM-02: Synchronous streaming read from a <see cref="TextReader"/> that yields
    /// records one at a time via <see cref="IEnumerable{T}"/>. The reader is NOT disposed
    /// by the engine — the caller owns its lifetime.
    /// </summary>
    /// <param name="reader">The text reader to stream from.</param>
    /// <returns>An enumerable of parsed records.</returns>
    public IEnumerable<T> ReadSync(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadSyncCore(reader, maxRecords: 0);
    }

    /// <summary>STRM-02: <see cref="ReadSync(TextReader)"/> with a record-count cap.</summary>
    public IEnumerable<T> ReadSync(TextReader reader, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadSyncCore(reader, maxRecords);
    }

    /// <summary>
    /// STRM-03: Synchronous streaming read from a string that yields records one at a time
    /// via <see cref="IEnumerable{T}"/>. Lines are extracted via <see cref="SpanLineReader"/>
    /// to avoid full materialization.
    /// </summary>
    /// <param name="content">The string content to stream from.</param>
    /// <returns>An enumerable of parsed records.</returns>
    public IEnumerable<T> ReadSync(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ReadSyncCore(new StringReader(content), maxRecords: 0);
    }

    /// <summary>STRM-03: <see cref="ReadSync(string)"/> with a record-count cap.</summary>
    public IEnumerable<T> ReadSync(string content, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ReadSyncCore(new StringReader(content), maxRecords);
    }

    private IEnumerable<T> ReadSyncCore(TextReader reader, int maxRecords)
    {
        var opts = _descriptor.Options;
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);
        int lineNumber = 0;
        int totalRecords = 0;

        int ignoredFirst = 0;
        if (opts.IgnoreFirst > 0)
        {
            for (int i = 0; i < opts.IgnoreFirst; i++)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    TotalRecords = 0;
                    FooterText = null;
                    yield break;
                }
                lineNumber++;
                ignoredFirst++;
            }
        }

        var lineBuffer = new List<string>(linesPerRecord);
        var multilineBuffer = new StringBuilder();
        bool isBuffering = false;
        int multilineStartLine = 0;

        // LIMIT-01: tail buffer for IgnoreLast in sync streaming
        Queue<T>? tailBuffer = null;
        Queue<int>? tailStartLines = null;
        if (opts.IgnoreLast > 0)
        {
            tailBuffer = new Queue<T>(opts.IgnoreLast + 1);
            tailStartLines = new Queue<int>(opts.IgnoreLast + 1);
        }

        while (maxRecords <= 0 || totalRecords < maxRecords)
        {
            lineBuffer.Clear();
            int startLine = lineNumber + 1;

            for (int i = 0; i < linesPerRecord; i++)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    if (i == 0)
                    {
                        // EOF: drain tail buffer
                        if (tailBuffer is not null && tailBuffer.Count > 0)
                        {
                            var footerRecords = new string[tailBuffer.Count];
                            int fi = 0;
                            while (tailBuffer.Count > 0)
                            {
                                var rec = tailBuffer.Dequeue();
                                using var buf = new RentedBufferWriter(256);
                                _descriptor.Format(rec, buf);
                                footerRecords[fi++] = buf.WrittenSpan.ToString();
                            }
                            FooterText = string.Join(NewLineForWrite, footerRecords);
                        }
                        TotalRecords = totalRecords;
                        yield break;
                    }
                    break;
                }
                lineBuffer.Add(line);
                lineNumber++;
            }

            string joinedLine = linesPerRecord == 1 ? lineBuffer[0] : string.Join("\n", lineBuffer);

            if (opts.MultilineMode == MultilineMode.AllowForRead)
            {
                if (!isBuffering)
                {
                    if (DelimitedFieldEnumerator.HasUnclosedQuote(joinedLine.AsSpan()))
                    {
                        multilineBuffer.Clear();
                        multilineBuffer.Append(joinedLine);
                        multilineStartLine = startLine;
                        isBuffering = true;
                        continue;
                    }
                }
                else
                {
                    multilineBuffer.Append(NewLineForWrite);
                    multilineBuffer.Append(joinedLine);

                    var assembled = multilineBuffer.ToString();
                    if (!DelimitedFieldEnumerator.HasUnclosedQuote(assembled.AsSpan()))
                    {
                        isBuffering = false;
                        var parsed = ParseAndYieldSync(assembled.AsSpan(), assembled, multilineStartLine, opts, ref totalRecords);
                        multilineBuffer.Clear();

                        if (s_isStruct || !EqualityComparer<T>.Default.Equals(parsed, default))
                        {
                            if (tailBuffer is not null)
                            {
                                tailBuffer.Enqueue(parsed);
                                tailStartLines!.Enqueue(multilineStartLine);
                                if (tailBuffer.Count > opts.IgnoreLast)
                                {
                                    var toYield = tailBuffer.Dequeue();
                                    LineNumber = tailStartLines.Dequeue();
                                    yield return toYield;
                                }
                            }
                            else
                            {
                                LineNumber = multilineStartLine;
                                yield return parsed;
                            }
                        }
                        continue;
                    }
                    continue;
                }
            }

            if (opts.IgnoreEmptyLines && string.IsNullOrWhiteSpace(joinedLine)) continue;
            if (opts.CommentMarker != '\0' && joinedLine.Length > 0 && joinedLine[0] == opts.CommentMarker) continue;
            if (_conditionMatcher is not null && !_conditionMatcher.ShouldInclude(joinedLine.AsSpan())) continue;

            var parsedRecord = ParseAndYieldSync(joinedLine.AsSpan(), joinedLine, startLine, opts, ref totalRecords);
            if (s_isStruct || !EqualityComparer<T>.Default.Equals(parsedRecord, default))
            {
                if (tailBuffer is not null)
                {
                    tailBuffer.Enqueue(parsedRecord);
                    tailStartLines!.Enqueue(startLine);
                    if (tailBuffer.Count > opts.IgnoreLast)
                    {
                        var toYield = tailBuffer.Dequeue();
                        LineNumber = tailStartLines.Dequeue();
                        yield return toYield;
                    }
                }
                else
                {
                    LineNumber = startLine;
                    yield return parsedRecord;
                }
            }
        }

        // EOF: drain tail buffer into FooterText
        if (tailBuffer is not null && tailBuffer.Count > 0)
        {
            var footerRecords = new string[tailBuffer.Count];
            int fi = 0;
            while (tailBuffer.Count > 0)
            {
                var rec = tailBuffer.Dequeue();
                using var buf = new RentedBufferWriter(256);
                _descriptor.Format(rec, buf);
                footerRecords[fi++] = buf.WrittenSpan.ToString();
            }
            FooterText = string.Join(NewLineForWrite, footerRecords);
        }

        TotalRecords = totalRecords;
    }

    private T ParseAndYieldSync(ReadOnlySpan<char> lineSpan, string lineString, int startLine, RecordOptions opts, ref int totalRecords)
    {
        T record;
        try
        {
            ValidateDelimitedFieldCount(lineSpan, opts.Delimiter, _descriptor.Fields.Length, _descriptor.Fields);
            record = _descriptor.Parse(lineSpan, lineCount: 1);
        }
        catch (Exception ex)
        {
            switch (ErrorManager.ErrorMode)
            {
                case ErrorMode.ThrowException:
                    throw CreateParseContextException(startLine, lineString, ex);
                case ErrorMode.IgnoreAndContinue:
                case ErrorMode.SaveAndContinue:
                    if (ErrorManager.ErrorMode == ErrorMode.SaveAndContinue)
                        ErrorManager.AddError(new ErrorInfo(startLine, lineString, ex));
                    totalRecords++;
                    LineNumber = startLine;
                    return default!;
            }
            totalRecords++;
            LineNumber = startLine;
            return default!;
        }

        totalRecords++;
        LineNumber = startLine;
        return record;
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
    /// OPT-01: Uses line-boundary tracking instead of per-line string allocations for the common case.
    /// OPT-02: Eliminates redundant EnsureDelimitedFieldCount scan.
    /// OPT-03: Uses CollectionsMarshal.AsSpan for zero-copy result buffer writes.
    /// OPT-07: Two-pass line counting with pre-allocated int[] arrays (no List growth).
    /// OPT-08: SpanLineReader SIMD-accelerated newline detection via SearchValues.
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

        // OPT-01: Two-path approach.
        // Fast path (no multiline): track line boundaries as (start, length) tuples — zero per-line allocations.
        // Slow path (multiline): materialize strings as before (rare, complex case).
        if (opts.MultilineMode != MultilineMode.AllowForRead)
        {
            return ReadCoreFastPath(content, maxRecords, opts);
        }

        return ReadCoreMultilinePath(content, maxRecords, opts);
    }

    /// <summary>
    /// OPT-01/OPT-07/OPT-08 Fast path: zero per-line string allocations.
    /// Two-pass line counting with pre-allocated int[] (OPT-07).
    /// SpanLineReader SIMD-accelerated newline detection via SearchValues (OPT-08).
    /// </summary>
    private T[] ReadCoreFastPath(string content, int maxRecords, RecordOptions opts)
    {
        // Step 2a: Two-pass line boundary collection.
        // Pass 1: Count lines using SIMD-accelerated IndexOfAny.
        // Pass 2: Fill pre-allocated int[] arrays with starts and lengths.
        int contentLength = content.Length;
        int lineCount = CountLines(content);

        // Early exit for empty content
        if (lineCount == 0)
        {
            TotalRecords = 0;
            return [];
        }

        var lineStarts = new int[lineCount];
        var lineLengths = new int[lineCount];
        FillLineBoundaries(content, contentLength, lineStarts, lineLengths);

        // Step 3: IgnoreFirst — capture header
        int headerLineCount = 0;
        if (opts.IgnoreFirst > 0 && lineCount > 0)
        {
            headerLineCount = Math.Min(opts.IgnoreFirst, lineCount);
            HeaderText = BuildJoinedString(content, lineStarts, lineLengths, 0, headerLineCount, NewLineForWrite);
        }

        // Step 4: IgnoreLast — capture footer
        int footerLineCount = 0;
        if (opts.IgnoreLast > 0 && lineCount > 0)
        {
            int remainingAfterHeader = lineCount - headerLineCount;
            footerLineCount = Math.Min(opts.IgnoreLast, remainingAfterHeader);
            int footerStartIdx = lineCount - footerLineCount;
            FooterText = BuildJoinedString(content, lineStarts, lineLengths, footerStartIdx, footerLineCount, NewLineForWrite);
        }

        int dataStartIdx = headerLineCount;
        int dataEndIdx = lineCount - footerLineCount;
        int dataLineCount = Math.Max(0, dataEndIdx - dataStartIdx);

        // OPT-05: Use pre-allocated T[] for the result buffer.
        // Simpler and faster than ArrayPool for this use case — new T[n] for reference types
        // is essentially free (zeroed pointers), while ArrayPool.Rent has locking overhead.
        T[]? resultBuffer = dataLineCount > 0 ? new T[dataLineCount] : [];
        int resultIndex = 0;
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);

        // OPT-09: Hoist event subscription checks outside the loop to avoid
        // repeated null checks and unnecessary string materialization.
        bool hasBeforeRead = BeforeReadRecord is not null || s_hasNotifyRead;
        bool hasAfterRead = AfterReadRecord is not null || s_hasNotifyRead;

        for (int li = dataStartIdx; li < dataEndIdx;)
        {
            int available = dataEndIdx - li;
            bool incomplete = available < linesPerRecord;
            int physicalLinesInGroup = incomplete ? available : linesPerRecord;

            try
            {
                int firstStart = lineStarts[li];
                int firstLen = lineLengths[li];
                ReadOnlySpan<char> firstLineSpan = content.AsSpan(firstStart, firstLen);

                // Build joined logical line — only materialize for multi-line records
                ReadOnlySpan<char> activeLineSpan;
                string? activeLineString = null;

                if (physicalLinesInGroup == 1)
                {
                    activeLineSpan = firstLineSpan;
                }
                else
                {
                    activeLineString = BuildJoinedString(content, lineStarts, lineLengths, li, physicalLinesInGroup, "\n");
                    activeLineSpan = activeLineString.AsSpan();
                }

                int startLineOneBased = li + 1;
                int endLineOneBased = li + physicalLinesInGroup;

                // D-08: IgnoreEmptyLines
                if (opts.IgnoreEmptyLines && !incomplete)
                {
                    bool allEmpty = true;
                    for (int j = 0; j < physicalLinesInGroup; j++)
                    {
                        if (lineLengths[li + j] != 0)
                        {
                            allEmpty = false;
                            break;
                        }
                    }
                    if (allEmpty)
                        continue;
                }

                // D-08: CommentMarker
                if (!incomplete && opts.CommentMarker != '\0' && firstLen > 0 && content[firstStart] == opts.CommentMarker)
                    continue;

                // CREC-10: ConditionalRecord filtering
                if (!incomplete && _conditionMatcher is not null && !_conditionMatcher.ShouldInclude(firstLineSpan))
                    continue;

                // BeforeRead event and interface — requires string materialization
                if (!incomplete && hasBeforeRead)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    var beforeArgs = new BeforeReadEventArgs<T>(this, activeLineString, startLineOneBased);

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
                        break;
                    if (beforeArgs.SkipThisRecord)
                        continue;
                    if (beforeArgs.RecordLineChanged)
                    {
                        activeLineString = beforeArgs.RecordLine;
                        activeLineSpan = activeLineString.AsSpan();
                    }
                }

                T record;
                try
                {
                    if (incomplete)
                    {
                        activeLineString ??= activeLineSpan.ToString();
                        throw new FormatException(
                            $"Expected {linesPerRecord} physical line(s) per logical record; only {available} line(s) remain after line {li + 1}.");
                    }

                    // Validate field count before parsing — the generated Parse code does NOT
                    // validate delimited field count, it silently fills missing fields with defaults.
                    ValidateDelimitedFieldCount(activeLineSpan, opts.Delimiter, _descriptor.Fields.Length, _descriptor.Fields);
                    record = ParseOrRecycle(activeLineSpan, linesPerRecord);
                }
                catch (Exception ex)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    switch (ErrorManager.ErrorMode)
                    {
                        case ErrorMode.ThrowException:
                            throw CreateParseContextException(startLineOneBased, activeLineString, ex);
                        case ErrorMode.IgnoreAndContinue:
                            continue;
                        case ErrorMode.SaveAndContinue:
                            ErrorManager.AddError(new ErrorInfo(startLineOneBased, activeLineString, ex));
                            continue;
                    }
                    break;
                }

                // AfterRead event
                if (!incomplete && hasAfterRead)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    var afterArgs = new AfterReadEventArgs<T>(this, record, activeLineString, endLineOneBased);

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

                // OPT-05: Write directly into rented ArrayPool buffer.
                // No List<T>, no CollectionsMarshal, no growth checks.
                resultBuffer![resultIndex] = record;
                resultIndex++;

                LineNumber = endLineOneBased;
                if (Progress is not null)
                    Progress.Invoke(this, new ProgressEventArgs(resultIndex, -1, 0L, 0L));

                if (maxRecords > 0 && resultIndex >= maxRecords)
                    break;
            }
            finally
            {
                li += physicalLinesInGroup;
            }
        }

        TotalRecords = resultIndex;

        // OPT-05: Return exact-size array from pre-allocated buffer.
        if (dataLineCount == 0)
            return [];

        if (resultIndex == resultBuffer!.Length)
            return resultBuffer;

        // Skips occurred — create exact-size array and copy.
        var exactResult = new T[resultIndex];
        resultBuffer.AsSpan(0, resultIndex).CopyTo(exactResult);
        return exactResult;
    }

    /// <summary>
    /// OPT-08: Counts lines using SIMD-accelerated IndexOfAny('\r', '\n').
    /// </summary>
    private static readonly SearchValues<char> s_newlineChars = SearchValues.Create(['\r', '\n']);

    private static int CountLines(string content)
    {
        int count = 0;
        ReadOnlySpan<char> span = content.AsSpan();
        while (!span.IsEmpty)
        {
            int idx = span.IndexOfAny(s_newlineChars);
            if (idx < 0)
            {
                // Trailing content after last newline — counts as a line only if non-empty
                // or if the content itself is non-empty (handles "line\n" → 1 line, "line" → 1 line)
                if (span.Length > 0 || count == 0)
                    count++;
                break;
            }
            count++;
            char terminator = span[idx];
            span = span[(idx + 1)..];
            // CRLF handling
            if (terminator == '\r' && !span.IsEmpty && span[0] == '\n')
                span = span[1..];
        }
        return count;
    }

    /// <summary>
    /// OPT-08: Fills pre-allocated lineStarts and lineLengths arrays using SIMD-accelerated IndexOfAny.
    /// </summary>
    private static void FillLineBoundaries(string content, int contentLength, int[] lineStarts, int[] lineLengths)
    {
        int pos = 0;
        int lineIndex = 0;
        ReadOnlySpan<char> span = content.AsSpan();
        while (pos < contentLength && lineIndex < lineStarts.Length)
        {
            int lineStart = pos;
            ReadOnlySpan<char> remaining = span[pos..];
            int idx = remaining.IndexOfAny(s_newlineChars);
            int lineEnd;
            if (idx < 0)
            {
                lineEnd = contentLength;
                pos = contentLength;
            }
            else
            {
                lineEnd = pos + idx;
                pos = lineEnd;
                if (pos < contentLength && content[pos] == '\r')
                    pos++;
                if (pos < contentLength && content[pos] == '\n')
                    pos++;
            }

            lineStarts[lineIndex] = lineStart;
            lineLengths[lineIndex] = lineEnd - lineStart;
            lineIndex++;
        }
    }

    /// <summary>
    /// OPT-06: Span-based read pipeline. Zero content allocation — operates directly on
    /// the input span using line boundary tracking. Header/footer capture materializes
    /// strings only when needed (the public HeaderText/FooterText properties are strings).
    /// </summary>
    private T[] ReadCoreSpan(ReadOnlySpan<char> content, int maxRecords)
    {
        LineNumber = 0;
        TotalRecords = 0;
        HeaderText = null;
        FooterText = null;
        ErrorManager.ClearErrors();

        var opts = _descriptor.Options;

        if (opts.MultilineMode == MultilineMode.AllowForRead)
        {
            return ReadCore(content.ToString(), maxRecords);
        }

        int contentLength = content.Length;
        int lineCount = CountLinesSpan(content);

        if (lineCount == 0)
        {
            TotalRecords = 0;
            return [];
        }

        var lineStarts = new int[lineCount];
        var lineLengths = new int[lineCount];
        FillLineBoundariesSpan(content, contentLength, lineStarts, lineLengths);

        int headerLineCount = 0;
        if (opts.IgnoreFirst > 0 && lineCount > 0)
        {
            headerLineCount = Math.Min(opts.IgnoreFirst, lineCount);
            HeaderText = BuildJoinedStringSpan(content, lineStarts, lineLengths, 0, headerLineCount, NewLineForWrite);
        }

        int footerLineCount = 0;
        if (opts.IgnoreLast > 0 && lineCount > 0)
        {
            int remainingAfterHeader = lineCount - headerLineCount;
            footerLineCount = Math.Min(opts.IgnoreLast, remainingAfterHeader);
            int footerStartIdx = lineCount - footerLineCount;
            FooterText = BuildJoinedStringSpan(content, lineStarts, lineLengths, footerStartIdx, footerLineCount, NewLineForWrite);
        }

        int dataStartIdx = headerLineCount;
        int dataEndIdx = lineCount - footerLineCount;
        int dataLineCount = Math.Max(0, dataEndIdx - dataStartIdx);

        T[]? resultBuffer = dataLineCount > 0 ? new T[dataLineCount] : [];
        int resultIndex = 0;
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);

        bool hasBeforeRead = BeforeReadRecord is not null || s_hasNotifyRead;
        bool hasAfterRead = AfterReadRecord is not null || s_hasNotifyRead;

        for (int li = dataStartIdx; li < dataEndIdx;)
        {
            int available = dataEndIdx - li;
            bool incomplete = available < linesPerRecord;
            int physicalLinesInGroup = incomplete ? available : linesPerRecord;

            try
            {
                int firstStart = lineStarts[li];
                int firstLen = lineLengths[li];
                ReadOnlySpan<char> firstLineSpan = content[firstStart..(firstStart + firstLen)];

                ReadOnlySpan<char> activeLineSpan;
                string? activeLineString = null;

                if (physicalLinesInGroup == 1)
                {
                    activeLineSpan = firstLineSpan;
                }
                else
                {
                    activeLineString = BuildJoinedStringSpan(content, lineStarts, lineLengths, li, physicalLinesInGroup, "\n");
                    activeLineSpan = activeLineString.AsSpan();
                }

                int startLineOneBased = li + 1;
                int endLineOneBased = li + physicalLinesInGroup;

                // D-08: IgnoreEmptyLines
                if (opts.IgnoreEmptyLines && !incomplete)
                {
                    bool allEmpty = true;
                    for (int j = 0; j < physicalLinesInGroup; j++)
                    {
                        if (lineLengths[li + j] != 0)
                        {
                            allEmpty = false;
                            break;
                        }
                    }
                    if (allEmpty)
                        continue;
                }

                // D-08: CommentMarker
                if (!incomplete && opts.CommentMarker != '\0' && firstLen > 0 && content[firstStart] == opts.CommentMarker)
                    continue;

                // CREC-10: ConditionalRecord filtering
                if (!incomplete && _conditionMatcher is not null && !_conditionMatcher.ShouldInclude(firstLineSpan))
                    continue;

                // BeforeRead event and interface — requires string materialization
                if (!incomplete && hasBeforeRead)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    var beforeArgs = new BeforeReadEventArgs<T>(this, activeLineString, startLineOneBased);

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
                        break;
                    if (beforeArgs.SkipThisRecord)
                        continue;
                    if (beforeArgs.RecordLineChanged)
                    {
                        activeLineString = beforeArgs.RecordLine;
                        activeLineSpan = activeLineString.AsSpan();
                    }
                }

                T record;
                try
                {
                    if (incomplete)
                    {
                        activeLineString ??= activeLineSpan.ToString();
                        throw new FormatException(
                            $"Expected {linesPerRecord} physical line(s) per logical record; only {available} line(s) remain after line {li + 1}.");
                    }

                    ValidateDelimitedFieldCount(activeLineSpan, opts.Delimiter, _descriptor.Fields.Length, _descriptor.Fields);
                    record = ParseOrRecycle(activeLineSpan, linesPerRecord);
                }
                catch (Exception ex)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    switch (ErrorManager.ErrorMode)
                    {
                        case ErrorMode.ThrowException:
                            throw CreateParseContextException(startLineOneBased, activeLineString, ex);
                        case ErrorMode.IgnoreAndContinue:
                            continue;
                        case ErrorMode.SaveAndContinue:
                            ErrorManager.AddError(new ErrorInfo(startLineOneBased, activeLineString, ex));
                            continue;
                    }
                    break;
                }

                // AfterRead event
                if (!incomplete && hasAfterRead)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    var afterArgs = new AfterReadEventArgs<T>(this, record, activeLineString, endLineOneBased);

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

                resultBuffer![resultIndex] = record;
                resultIndex++;

                LineNumber = endLineOneBased;
                if (Progress is not null)
                    Progress.Invoke(this, new ProgressEventArgs(resultIndex, -1, 0L, 0L));

                if (maxRecords > 0 && resultIndex >= maxRecords)
                    break;
            }
            finally
            {
                li += physicalLinesInGroup;
            }
        }

        TotalRecords = resultIndex;

        if (dataLineCount == 0)
            return [];

        if (resultIndex == resultBuffer!.Length)
            return resultBuffer;

        var exactResult = new T[resultIndex];
        resultBuffer.AsSpan(0, resultIndex).CopyTo(exactResult);
        return exactResult;
    }

    /// <summary>
    /// OPT-11: Callback-based span read pipeline. Zero result array allocation —
    /// invokes callback for each record. Shares line boundary logic with ReadCoreSpan.
    /// </summary>
    private int ReadCoreSpanCallback(ReadOnlySpan<char> content, Action<T> callback, int maxRecords)
    {
        LineNumber = 0;
        TotalRecords = 0;
        HeaderText = null;
        FooterText = null;
        ErrorManager.ClearErrors();

        var opts = _descriptor.Options;

        if (opts.MultilineMode == MultilineMode.AllowForRead)
        {
            return ReadCoreCallback(content.ToString(), callback, maxRecords);
        }

        int contentLength = content.Length;
        int lineCount = CountLinesSpan(content);

        if (lineCount == 0)
        {
            TotalRecords = 0;
            return 0;
        }

        var lineStarts = new int[lineCount];
        var lineLengths = new int[lineCount];
        FillLineBoundariesSpan(content, contentLength, lineStarts, lineLengths);

        int headerLineCount = 0;
        if (opts.IgnoreFirst > 0 && lineCount > 0)
        {
            headerLineCount = Math.Min(opts.IgnoreFirst, lineCount);
            HeaderText = BuildJoinedStringSpan(content, lineStarts, lineLengths, 0, headerLineCount, NewLineForWrite);
        }

        int footerLineCount = 0;
        if (opts.IgnoreLast > 0 && lineCount > 0)
        {
            int remainingAfterHeader = lineCount - headerLineCount;
            footerLineCount = Math.Min(opts.IgnoreLast, remainingAfterHeader);
            int footerStartIdx = lineCount - footerLineCount;
            FooterText = BuildJoinedStringSpan(content, lineStarts, lineLengths, footerStartIdx, footerLineCount, NewLineForWrite);
        }

        int dataStartIdx = headerLineCount;
        int dataEndIdx = lineCount - footerLineCount;
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);

        bool hasBeforeRead = BeforeReadRecord is not null || s_hasNotifyRead;
        bool hasAfterRead = AfterReadRecord is not null || s_hasNotifyRead;

        int resultIndex = 0;
        for (int li = dataStartIdx; li < dataEndIdx;)
        {
            int available = dataEndIdx - li;
            bool incomplete = available < linesPerRecord;
            int physicalLinesInGroup = incomplete ? available : linesPerRecord;

            try
            {
                int firstStart = lineStarts[li];
                int firstLen = lineLengths[li];
                ReadOnlySpan<char> firstLineSpan = content[firstStart..(firstStart + firstLen)];

                ReadOnlySpan<char> activeLineSpan;
                string? activeLineString = null;

                if (physicalLinesInGroup == 1)
                {
                    activeLineSpan = firstLineSpan;
                }
                else
                {
                    activeLineString = BuildJoinedStringSpan(content, lineStarts, lineLengths, li, physicalLinesInGroup, "\n");
                    activeLineSpan = activeLineString.AsSpan();
                }

                int startLineOneBased = li + 1;
                int endLineOneBased = li + physicalLinesInGroup;

                if (opts.IgnoreEmptyLines && !incomplete)
                {
                    bool allEmpty = true;
                    for (int j = 0; j < physicalLinesInGroup; j++)
                    {
                        if (lineLengths[li + j] != 0)
                        {
                            allEmpty = false;
                            break;
                        }
                    }
                    if (allEmpty)
                        continue;
                }

                if (!incomplete && opts.CommentMarker != '\0' && firstLen > 0 && content[firstStart] == opts.CommentMarker)
                    continue;

                if (!incomplete && _conditionMatcher is not null && !_conditionMatcher.ShouldInclude(firstLineSpan))
                    continue;

                if (!incomplete && hasBeforeRead)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    var beforeArgs = new BeforeReadEventArgs<T>(this, activeLineString, startLineOneBased);
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
                        break;
                    if (beforeArgs.SkipThisRecord)
                        continue;
                    if (beforeArgs.RecordLineChanged)
                    {
                        activeLineString = beforeArgs.RecordLine;
                        activeLineSpan = activeLineString.AsSpan();
                    }
                }


                T record;
                try
                {
                    if (incomplete)
                    {
                        activeLineString ??= activeLineSpan.ToString();
                        throw new FormatException(
                            $"Expected {linesPerRecord} physical line(s) per logical record; only {available} line(s) remain after line {li + 1}.");
                    }

                    ValidateDelimitedFieldCount(activeLineSpan, opts.Delimiter, _descriptor.Fields.Length, _descriptor.Fields);
                    record = ParseOrRecycle(activeLineSpan, linesPerRecord);
                }
                catch (Exception ex)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    switch (ErrorManager.ErrorMode)
                    {
                        case ErrorMode.ThrowException:
                            throw CreateParseContextException(startLineOneBased, activeLineString, ex);
                        case ErrorMode.IgnoreAndContinue:
                            continue;
                        case ErrorMode.SaveAndContinue:
                            ErrorManager.AddError(new ErrorInfo(startLineOneBased, activeLineString, ex));
                            continue;
                    }
                    break;
                }

                if (!incomplete && hasAfterRead)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    var afterArgs = new AfterReadEventArgs<T>(this, record, activeLineString, endLineOneBased);

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

                callback(record);
                resultIndex++;

                LineNumber = endLineOneBased;
                if (Progress is not null)
                    Progress.Invoke(this, new ProgressEventArgs(resultIndex, -1, 0L, 0L));

                if (maxRecords > 0 && resultIndex >= maxRecords)
                    break;
            }
            finally
            {
                li += physicalLinesInGroup;
            }
        }

        TotalRecords = resultIndex;
        return resultIndex;
    }

    /// <summary>
    /// OPT-11: Callback-based string read pipeline. Zero result array allocation.
    /// </summary>
    private int ReadCoreCallback(string content, Action<T> callback, int maxRecords)
    {
        LineNumber = 0;
        TotalRecords = 0;
        HeaderText = null;
        FooterText = null;
        ErrorManager.ClearErrors();

        var opts = _descriptor.Options;

        if (opts.MultilineMode != MultilineMode.AllowForRead)
        {
            return ReadCoreFastPathCallback(content, maxRecords, opts, callback);
        }

        return ReadCoreMultilinePathCallback(content, maxRecords, opts, callback);
    }

    /// <summary>
    /// OPT-11: Callback-based fast path for string content.
    /// </summary>
    private int ReadCoreFastPathCallback(string content, int maxRecords, RecordOptions opts, Action<T> callback)
    {
        int contentLength = content.Length;
        int lineCount = CountLines(content);

        if (lineCount == 0)
        {
            TotalRecords = 0;
            return 0;
        }

        var lineStarts = new int[lineCount];
        var lineLengths = new int[lineCount];
        FillLineBoundaries(content, contentLength, lineStarts, lineLengths);

        int headerLineCount = 0;
        if (opts.IgnoreFirst > 0 && lineCount > 0)
        {
            headerLineCount = Math.Min(opts.IgnoreFirst, lineCount);
            HeaderText = BuildJoinedString(content, lineStarts, lineLengths, 0, headerLineCount, NewLineForWrite);
        }

        int footerLineCount = 0;
        if (opts.IgnoreLast > 0 && lineCount > 0)
        {
            int remainingAfterHeader = lineCount - headerLineCount;
            footerLineCount = Math.Min(opts.IgnoreLast, remainingAfterHeader);
            int footerStartIdx = lineCount - footerLineCount;
            FooterText = BuildJoinedString(content, lineStarts, lineLengths, footerStartIdx, footerLineCount, NewLineForWrite);
        }

        int dataStartIdx = headerLineCount;
        int dataEndIdx = lineCount - footerLineCount;
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);

        bool hasBeforeRead = BeforeReadRecord is not null || s_hasNotifyRead;
        bool hasAfterRead = AfterReadRecord is not null || s_hasNotifyRead;

        int resultIndex = 0;
        for (int li = dataStartIdx; li < dataEndIdx;)
        {
            int available = dataEndIdx - li;
            bool incomplete = available < linesPerRecord;
            int physicalLinesInGroup = incomplete ? available : linesPerRecord;

            try
            {
                int firstStart = lineStarts[li];
                int firstLen = lineLengths[li];
                ReadOnlySpan<char> firstLineSpan = content.AsSpan(firstStart, firstLen);

                ReadOnlySpan<char> activeLineSpan;
                string? activeLineString = null;

                if (physicalLinesInGroup == 1)
                {
                    activeLineSpan = firstLineSpan;
                }
                else
                {
                    activeLineString = BuildJoinedString(content, lineStarts, lineLengths, li, physicalLinesInGroup, "\n");
                    activeLineSpan = activeLineString.AsSpan();
                }

                int startLineOneBased = li + 1;
                int endLineOneBased = li + physicalLinesInGroup;

                if (opts.IgnoreEmptyLines && !incomplete)
                {
                    bool allEmpty = true;
                    for (int j = 0; j < physicalLinesInGroup; j++)
                    {
                        if (lineLengths[li + j] != 0)
                        {
                            allEmpty = false;
                            break;
                        }
                    }
                    if (allEmpty)
                        continue;
                }

                if (!incomplete && opts.CommentMarker != '\0' && firstLen > 0 && content[firstStart] == opts.CommentMarker)
                    continue;

                if (!incomplete && _conditionMatcher is not null && !_conditionMatcher.ShouldInclude(firstLineSpan))
                    continue;

                if (!incomplete && hasBeforeRead)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    var beforeArgs = new BeforeReadEventArgs<T>(this, activeLineString, startLineOneBased);
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
                        break;
                    if (beforeArgs.SkipThisRecord)
                        continue;
                    if (beforeArgs.RecordLineChanged)
                    {
                        activeLineString = beforeArgs.RecordLine;
                        activeLineSpan = activeLineString.AsSpan();
                    }
                }


                T record;
                try
                {
                    if (incomplete)
                    {
                        activeLineString ??= activeLineSpan.ToString();
                        throw new FormatException(
                            $"Expected {linesPerRecord} physical line(s) per logical record; only {available} line(s) remain after line {li + 1}.");
                    }

                    ValidateDelimitedFieldCount(activeLineSpan, opts.Delimiter, _descriptor.Fields.Length, _descriptor.Fields);
                    record = ParseOrRecycle(activeLineSpan, linesPerRecord);
                }
                catch (Exception ex)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    switch (ErrorManager.ErrorMode)
                    {
                        case ErrorMode.ThrowException:
                            throw CreateParseContextException(startLineOneBased, activeLineString, ex);
                        case ErrorMode.IgnoreAndContinue:
                            continue;
                        case ErrorMode.SaveAndContinue:
                            ErrorManager.AddError(new ErrorInfo(startLineOneBased, activeLineString, ex));
                            continue;
                    }
                    break;
                }

                if (!incomplete && hasAfterRead)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    var afterArgs = new AfterReadEventArgs<T>(this, record, activeLineString, endLineOneBased);

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

                callback(record);
                resultIndex++;

                LineNumber = endLineOneBased;
                if (Progress is not null)
                    Progress.Invoke(this, new ProgressEventArgs(resultIndex, -1, 0L, 0L));

                if (maxRecords > 0 && resultIndex >= maxRecords)
                    break;
            }
            finally
            {
                li += physicalLinesInGroup;
            }
        }

        TotalRecords = resultIndex;
        return resultIndex;
    }

    /// <summary>
    /// OPT-11: Callback-based multiline path for string content.
    /// </summary>
    private int ReadCoreMultilinePathCallback(string content, int maxRecords, RecordOptions opts, Action<T> callback)
    {
        var lines = new List<string>();
        var reader = new SpanLineReader(content.AsSpan());
        while (reader.TryReadLine(out var lineSpan))
        {
            lines.Add(new string(lineSpan));
        }

        if (opts.IgnoreFirst > 0 && lines.Count > 0)
        {
            int take = Math.Min(opts.IgnoreFirst, lines.Count);
            HeaderText = string.Join(NewLineForWrite, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lines)[..take]);
            lines.RemoveRange(0, take);
        }

        if (opts.IgnoreLast > 0 && lines.Count > 0)
        {
            int take = Math.Min(opts.IgnoreLast, lines.Count);
            FooterText = string.Join(NewLineForWrite, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lines)[(lines.Count - take)..]);
            lines.RemoveRange(lines.Count - take, take);
        }

        var multilineJoin = Parsing.MultilineLineJoin.Detect(content.AsSpan());
        var (assembledLines, logicalStartPhysicalIndices, logicalEndPhysicalIndices) =
            AssembleMultilineRecords(lines, multilineJoin);

        int assembledCount = assembledLines.Count;
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);

        bool hasBeforeRead = BeforeReadRecord is not null || s_hasNotifyRead;
        bool hasAfterRead = AfterReadRecord is not null || s_hasNotifyRead;

        int resultIndex = 0;
        for (int li = 0; li < assembledCount;)
        {
            int available = assembledLines.Count - li;
            bool incomplete = available < linesPerRecord;
            int physicalLinesInGroup = incomplete ? available : linesPerRecord;

            try
            {
                string firstLine = assembledLines[li];
                string joinedLogicalLine;
                if (physicalLinesInGroup == 1)
                {
                    joinedLogicalLine = firstLine;
                }
                else
                {
                    if (physicalLinesInGroup == 2)
                    {
                        joinedLogicalLine = string.Concat(assembledLines[li], "\n", assembledLines[li + 1]);
                    }
                    else
                    {
                        var sb = new StringBuilder();
                        for (int j = 0; j < physicalLinesInGroup; j++)
                        {
                            if (j > 0) sb.Append('\n');
                            sb.Append(assembledLines[li + j]);
                        }
                        joinedLogicalLine = sb.ToString();
                    }
                }

                int startLineOneBased = opts.IgnoreFirst + 1 + logicalStartPhysicalIndices[li];
                int endLineOneBased = opts.IgnoreFirst + 1 + logicalEndPhysicalIndices[li + physicalLinesInGroup - 1];

                if (opts.IgnoreEmptyLines && !incomplete)
                {
                    bool allEmpty = true;
                    for (int j = 0; j < linesPerRecord; j++)
                    {
                        if (assembledLines[li + j].Length != 0) { allEmpty = false; break; }
                    }
                    if (allEmpty) continue;
                }

                if (!incomplete && opts.CommentMarker != '\0' && firstLine.Length > 0 && firstLine[0] == opts.CommentMarker)
                    continue;

                if (!incomplete && _conditionMatcher is not null && !_conditionMatcher.ShouldInclude(firstLine.AsSpan()))
                    continue;

                string activeLine = joinedLogicalLine;
                if (!incomplete && hasBeforeRead)
                {
                    var beforeArgs = new BeforeReadEventArgs<T>(this, activeLine, startLineOneBased);
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
                    if (beforeArgs.StopReading) break;
                    if (beforeArgs.SkipThisRecord) continue;
                    if (beforeArgs.RecordLineChanged) activeLine = beforeArgs.RecordLine;
                }

                T record;
                try
                {
                    if (incomplete)
                    {
                        throw new FormatException(
                            $"Expected {linesPerRecord} physical line(s) per logical record; only {available} line(s) remain after line {opts.IgnoreFirst + li + 1}.");
                    }

                    if (DelimitedFieldEnumerator.HasUnclosedQuote(activeLine.AsSpan()))
                    {
                        throw new FormatException($"Unclosed quoted field starting at line {startLineOneBased}. Check that all opening quotes have matching closing quotes (use \"\" for escaped quotes within quoted fields).");
                    }

                    record = ParseOrRecycle(activeLine.AsSpan(), linesPerRecord);
                }
                catch (Exception ex)
                {
                    switch (ErrorManager.ErrorMode)
                    {
                        case ErrorMode.ThrowException:
                            throw CreateParseContextException(startLineOneBased, activeLine, ex);
                        case ErrorMode.IgnoreAndContinue: continue;
                        case ErrorMode.SaveAndContinue:
                            ErrorManager.AddError(new ErrorInfo(startLineOneBased, activeLine, ex));
                            continue;
                    }
                    break;
                }

                if (!incomplete && hasAfterRead)
                {
                    var afterArgs = new AfterReadEventArgs<T>(this, record, activeLine, endLineOneBased);
                    if (AfterReadRecord is not null)
                    {
                        AfterReadRecord.Invoke(this, afterArgs);
                        if (afterArgs.SkipThisRecord) continue;
                    }
                    if (s_hasNotifyRead && record is INotifyRead<T> notifyRead)
                    {
                        notifyRead.AfterRead(afterArgs);
                        if (afterArgs.SkipThisRecord) continue;
                    }
                }

                callback(record);
                resultIndex++;
                LineNumber = endLineOneBased;
                if (Progress is not null)
                    Progress.Invoke(this, new ProgressEventArgs(resultIndex, -1, 0L, 0L));

                if (maxRecords > 0 && resultIndex >= maxRecords) break;
            }
            finally
            {
                li += physicalLinesInGroup;
            }
        }

        TotalRecords = resultIndex;
        return resultIndex;
    }

    /// <summary>
    /// REF-01: Core ref-callback-based span read pipeline. Passes records by reference
    /// to avoid struct copying.
    /// </summary>
    private int ReadCoreSpanRefCallback(ReadOnlySpan<char> content, SpanRecordCallback<T> callback, int maxRecords)
    {
        LineNumber = 0;
        TotalRecords = 0;
        HeaderText = null;
        FooterText = null;
        ErrorManager.ClearErrors();

        var opts = _descriptor.Options;

        if (opts.MultilineMode == MultilineMode.AllowForRead)
        {
            return ReadCoreRefCallback(content.ToString(), callback, maxRecords);
        }

        int contentLength = content.Length;
        int lineCount = CountLinesSpan(content);

        if (lineCount == 0)
        {
            TotalRecords = 0;
            return 0;
        }

        var lineStarts = new int[lineCount];
        var lineLengths = new int[lineCount];
        FillLineBoundariesSpan(content, contentLength, lineStarts, lineLengths);

        int headerLineCount = 0;
        if (opts.IgnoreFirst > 0 && lineCount > 0)
        {
            headerLineCount = Math.Min(opts.IgnoreFirst, lineCount);
            HeaderText = BuildJoinedStringSpan(content, lineStarts, lineLengths, 0, headerLineCount, NewLineForWrite);
        }

        int footerLineCount = 0;
        if (opts.IgnoreLast > 0 && lineCount > 0)
        {
            int remainingAfterHeader = lineCount - headerLineCount;
            footerLineCount = Math.Min(opts.IgnoreLast, remainingAfterHeader);
            int footerStartIdx = lineCount - footerLineCount;
            FooterText = BuildJoinedStringSpan(content, lineStarts, lineLengths, footerStartIdx, footerLineCount, NewLineForWrite);
        }

        int dataStartIdx = headerLineCount;
        int dataEndIdx = lineCount - footerLineCount;
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);

        bool hasBeforeRead = BeforeReadRecord is not null || s_hasNotifyRead;
        bool hasAfterRead = AfterReadRecord is not null || s_hasNotifyRead;

        int resultIndex = 0;
        for (int li = dataStartIdx; li < dataEndIdx;)
        {
            int available = dataEndIdx - li;
            bool incomplete = available < linesPerRecord;
            int physicalLinesInGroup = incomplete ? available : linesPerRecord;

            try
            {
                int firstStart = lineStarts[li];
                int firstLen = lineLengths[li];
                ReadOnlySpan<char> firstLineSpan = content[firstStart..(firstStart + firstLen)];

                ReadOnlySpan<char> activeLineSpan;
                string? activeLineString = null;

                if (physicalLinesInGroup == 1)
                {
                    activeLineSpan = firstLineSpan;
                }
                else
                {
                    activeLineString = BuildJoinedStringSpan(content, lineStarts, lineLengths, li, physicalLinesInGroup, "\n");
                    activeLineSpan = activeLineString.AsSpan();
                }

                int startLineOneBased = li + 1;
                int endLineOneBased = li + physicalLinesInGroup;

                if (opts.IgnoreEmptyLines && !incomplete)
                {
                    bool allEmpty = true;
                    for (int j = 0; j < physicalLinesInGroup; j++)
                    {
                        if (lineLengths[li + j] != 0)
                        {
                            allEmpty = false;
                            break;
                        }
                    }
                    if (allEmpty)
                        continue;
                }

                if (!incomplete && opts.CommentMarker != '\0' && firstLen > 0 && content[firstStart] == opts.CommentMarker)
                    continue;

                if (!incomplete && _conditionMatcher is not null && !_conditionMatcher.ShouldInclude(firstLineSpan))
                    continue;

                if (!incomplete && hasBeforeRead)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    var beforeArgs = new BeforeReadEventArgs<T>(this, activeLineString, startLineOneBased);
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
                        break;
                    if (beforeArgs.SkipThisRecord)
                        continue;
                    if (beforeArgs.RecordLineChanged)
                    {
                        activeLineString = beforeArgs.RecordLine;
                        activeLineSpan = activeLineString.AsSpan();
                    }
                }


                T record;
                try
                {
                    if (incomplete)
                    {
                        activeLineString ??= activeLineSpan.ToString();
                        throw new FormatException(
                            $"Expected {linesPerRecord} physical line(s) per logical record; only {available} line(s) remain after line {li + 1}.");
                    }

                    ValidateDelimitedFieldCount(activeLineSpan, opts.Delimiter, _descriptor.Fields.Length, _descriptor.Fields);
                    record = ParseOrRecycle(activeLineSpan, linesPerRecord);
                }
                catch (Exception ex)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    switch (ErrorManager.ErrorMode)
                    {
                        case ErrorMode.ThrowException:
                            throw CreateParseContextException(startLineOneBased, activeLineString, ex);
                        case ErrorMode.IgnoreAndContinue:
                            continue;
                        case ErrorMode.SaveAndContinue:
                            ErrorManager.AddError(new ErrorInfo(startLineOneBased, activeLineString, ex));
                            continue;
                    }
                    break;
                }

                if (!incomplete && hasAfterRead)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    var afterArgs = new AfterReadEventArgs<T>(this, record, activeLineString, endLineOneBased);

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

                callback(ref record);
                resultIndex++;

                LineNumber = endLineOneBased;
                if (Progress is not null)
                    Progress.Invoke(this, new ProgressEventArgs(resultIndex, -1, 0L, 0L));

                if (maxRecords > 0 && resultIndex >= maxRecords)
                    break;
            }
            finally
            {
                li += physicalLinesInGroup;
            }
        }

        TotalRecords = resultIndex;
        return resultIndex;
    }

    /// <summary>
    /// REF-01: Core ref-callback-based string read pipeline. Passes records by reference.
    /// </summary>
    private int ReadCoreRefCallback(string content, SpanRecordCallback<T> callback, int maxRecords)
    {
        LineNumber = 0;
        TotalRecords = 0;
        HeaderText = null;
        FooterText = null;
        ErrorManager.ClearErrors();

        var opts = _descriptor.Options;

        if (opts.MultilineMode != MultilineMode.AllowForRead)
        {
            return ReadCoreFastPathRefCallback(content, maxRecords, opts, callback);
        }

        return ReadCoreMultilinePathRefCallback(content, maxRecords, opts, callback);
    }

    /// <summary>REF-01: Ref-callback-based fast path for string content.</summary>
    private int ReadCoreFastPathRefCallback(string content, int maxRecords, RecordOptions opts, SpanRecordCallback<T> callback)
    {
        int contentLength = content.Length;
        int lineCount = CountLines(content);

        if (lineCount == 0)
        {
            TotalRecords = 0;
            return 0;
        }

        var lineStarts = new int[lineCount];
        var lineLengths = new int[lineCount];
        FillLineBoundaries(content, contentLength, lineStarts, lineLengths);

        int headerLineCount = 0;
        if (opts.IgnoreFirst > 0 && lineCount > 0)
        {
            headerLineCount = Math.Min(opts.IgnoreFirst, lineCount);
            HeaderText = BuildJoinedString(content, lineStarts, lineLengths, 0, headerLineCount, NewLineForWrite);
        }

        int footerLineCount = 0;
        if (opts.IgnoreLast > 0 && lineCount > 0)
        {
            int remainingAfterHeader = lineCount - headerLineCount;
            footerLineCount = Math.Min(opts.IgnoreLast, remainingAfterHeader);
            int footerStartIdx = lineCount - footerLineCount;
            FooterText = BuildJoinedString(content, lineStarts, lineLengths, footerStartIdx, footerLineCount, NewLineForWrite);
        }

        int dataStartIdx = headerLineCount;
        int dataEndIdx = lineCount - footerLineCount;
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);

        bool hasBeforeRead = BeforeReadRecord is not null || s_hasNotifyRead;
        bool hasAfterRead = AfterReadRecord is not null || s_hasNotifyRead;

        int resultIndex = 0;
        for (int li = dataStartIdx; li < dataEndIdx;)
        {
            int available = dataEndIdx - li;
            bool incomplete = available < linesPerRecord;
            int physicalLinesInGroup = incomplete ? available : linesPerRecord;

            try
            {
                int firstStart = lineStarts[li];
                int firstLen = lineLengths[li];
                ReadOnlySpan<char> firstLineSpan = content.AsSpan(firstStart, firstLen);

                ReadOnlySpan<char> activeLineSpan;
                string? activeLineString = null;

                if (physicalLinesInGroup == 1)
                {
                    activeLineSpan = firstLineSpan;
                }
                else
                {
                    activeLineString = BuildJoinedString(content, lineStarts, lineLengths, li, physicalLinesInGroup, "\n");
                    activeLineSpan = activeLineString.AsSpan();
                }

                int startLineOneBased = li + 1;
                int endLineOneBased = li + physicalLinesInGroup;

                if (opts.IgnoreEmptyLines && !incomplete)
                {
                    bool allEmpty = true;
                    for (int j = 0; j < physicalLinesInGroup; j++)
                    {
                        if (lineLengths[li + j] != 0)
                        {
                            allEmpty = false;
                            break;
                        }
                    }
                    if (allEmpty)
                        continue;
                }

                if (!incomplete && opts.CommentMarker != '\0' && firstLen > 0 && content[firstStart] == opts.CommentMarker)
                    continue;

                if (!incomplete && _conditionMatcher is not null && !_conditionMatcher.ShouldInclude(firstLineSpan))
                    continue;

                if (!incomplete && hasBeforeRead)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    var beforeArgs = new BeforeReadEventArgs<T>(this, activeLineString, startLineOneBased);
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
                        break;
                    if (beforeArgs.SkipThisRecord)
                        continue;
                    if (beforeArgs.RecordLineChanged)
                    {
                        activeLineString = beforeArgs.RecordLine;
                        activeLineSpan = activeLineString.AsSpan();
                    }
                }


                T record;
                try
                {
                    if (incomplete)
                    {
                        activeLineString ??= activeLineSpan.ToString();
                        throw new FormatException(
                            $"Expected {linesPerRecord} physical line(s) per logical record; only {available} line(s) remain after line {li + 1}.");
                    }

                    ValidateDelimitedFieldCount(activeLineSpan, opts.Delimiter, _descriptor.Fields.Length, _descriptor.Fields);
                    record = ParseOrRecycle(activeLineSpan, linesPerRecord);
                }
                catch (Exception ex)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    switch (ErrorManager.ErrorMode)
                    {
                        case ErrorMode.ThrowException:
                            throw CreateParseContextException(startLineOneBased, activeLineString, ex);
                        case ErrorMode.IgnoreAndContinue:
                            continue;
                        case ErrorMode.SaveAndContinue:
                            ErrorManager.AddError(new ErrorInfo(startLineOneBased, activeLineString, ex));
                            continue;
                    }
                    break;
                }

                if (!incomplete && hasAfterRead)
                {
                    activeLineString ??= activeLineSpan.ToString();
                    var afterArgs = new AfterReadEventArgs<T>(this, record, activeLineString, endLineOneBased);

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

                callback(ref record);
                resultIndex++;

                LineNumber = endLineOneBased;
                if (Progress is not null)
                    Progress.Invoke(this, new ProgressEventArgs(resultIndex, -1, 0L, 0L));

                if (maxRecords > 0 && resultIndex >= maxRecords)
                    break;
            }
            finally
            {
                li += physicalLinesInGroup;
            }
        }

        TotalRecords = resultIndex;
        return resultIndex;
    }

    /// <summary>REF-01: Ref-callback-based multiline path for string content.</summary>
    private int ReadCoreMultilinePathRefCallback(string content, int maxRecords, RecordOptions opts, SpanRecordCallback<T> callback)
    {
        var lines = new List<string>();
        var reader = new SpanLineReader(content.AsSpan());
        while (reader.TryReadLine(out var lineSpan))
        {
            lines.Add(new string(lineSpan));
        }

        if (opts.IgnoreFirst > 0 && lines.Count > 0)
        {
            int take = Math.Min(opts.IgnoreFirst, lines.Count);
            HeaderText = string.Join(NewLineForWrite, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lines)[..take]);
            lines.RemoveRange(0, take);
        }

        if (opts.IgnoreLast > 0 && lines.Count > 0)
        {
            int take = Math.Min(opts.IgnoreLast, lines.Count);
            FooterText = string.Join(NewLineForWrite, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lines)[(lines.Count - take)..]);
            lines.RemoveRange(lines.Count - take, take);
        }

        var multilineJoin = Parsing.MultilineLineJoin.Detect(content.AsSpan());
        var (assembledLines, logicalStartPhysicalIndices, logicalEndPhysicalIndices) =
            AssembleMultilineRecords(lines, multilineJoin);

        int assembledCount = assembledLines.Count;
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);

        bool hasBeforeRead = BeforeReadRecord is not null || s_hasNotifyRead;
        bool hasAfterRead = AfterReadRecord is not null || s_hasNotifyRead;

        int resultIndex = 0;
        for (int li = 0; li < assembledCount;)
        {
            int available = assembledLines.Count - li;
            bool incomplete = available < linesPerRecord;
            int physicalLinesInGroup = incomplete ? available : linesPerRecord;

            try
            {
                string firstLine = assembledLines[li];
                string joinedLogicalLine;
                if (physicalLinesInGroup == 1)
                {
                    joinedLogicalLine = firstLine;
                }
                else
                {
                    if (physicalLinesInGroup == 2)
                    {
                        joinedLogicalLine = string.Concat(assembledLines[li], "\n", assembledLines[li + 1]);
                    }
                    else
                    {
                        var sb = new StringBuilder();
                        for (int j = 0; j < physicalLinesInGroup; j++)
                        {
                            if (j > 0) sb.Append('\n');
                            sb.Append(assembledLines[li + j]);
                        }
                        joinedLogicalLine = sb.ToString();
                    }
                }

                int startLineOneBased = opts.IgnoreFirst + 1 + logicalStartPhysicalIndices[li];
                int endLineOneBased = opts.IgnoreFirst + 1 + logicalEndPhysicalIndices[li + physicalLinesInGroup - 1];

                if (opts.IgnoreEmptyLines && !incomplete)
                {
                    bool allEmpty = true;
                    for (int j = 0; j < linesPerRecord; j++)
                    {
                        if (assembledLines[li + j].Length != 0) { allEmpty = false; break; }
                    }
                    if (allEmpty) continue;
                }

                if (!incomplete && opts.CommentMarker != '\0' && firstLine.Length > 0 && firstLine[0] == opts.CommentMarker)
                    continue;

                if (!incomplete && _conditionMatcher is not null && !_conditionMatcher.ShouldInclude(firstLine.AsSpan()))
                    continue;

                string activeLine = joinedLogicalLine;
                if (!incomplete && hasBeforeRead)
                {
                    var beforeArgs = new BeforeReadEventArgs<T>(this, activeLine, startLineOneBased);
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
                    if (beforeArgs.StopReading) break;
                    if (beforeArgs.SkipThisRecord) continue;
                    if (beforeArgs.RecordLineChanged) activeLine = beforeArgs.RecordLine;
                }

                T record;
                try
                {
                    if (incomplete)
                    {
                        throw new FormatException(
                            $"Expected {linesPerRecord} physical line(s) per logical record; only {available} line(s) remain after line {opts.IgnoreFirst + li + 1}.");
                    }

                    if (DelimitedFieldEnumerator.HasUnclosedQuote(activeLine.AsSpan()))
                    {
                        throw new FormatException($"Unclosed quoted field starting at line {startLineOneBased}. Check that all opening quotes have matching closing quotes (use \"\" for escaped quotes within quoted fields).");
                    }

                    record = ParseOrRecycle(activeLine.AsSpan(), linesPerRecord);
                }
                catch (Exception ex)
                {
                    switch (ErrorManager.ErrorMode)
                    {
                        case ErrorMode.ThrowException:
                            throw CreateParseContextException(startLineOneBased, activeLine, ex);
                        case ErrorMode.IgnoreAndContinue: continue;
                        case ErrorMode.SaveAndContinue:
                            ErrorManager.AddError(new ErrorInfo(startLineOneBased, activeLine, ex));
                            continue;
                    }
                    break;
                }

                if (!incomplete && hasAfterRead)
                {
                    var afterArgs = new AfterReadEventArgs<T>(this, record, activeLine, endLineOneBased);
                    if (AfterReadRecord is not null)
                    {
                        AfterReadRecord.Invoke(this, afterArgs);
                        if (afterArgs.SkipThisRecord) continue;
                    }
                    if (s_hasNotifyRead && record is INotifyRead<T> notifyRead)
                    {
                        notifyRead.AfterRead(afterArgs);
                        if (afterArgs.SkipThisRecord) continue;
                    }
                }

                callback(ref record);
                resultIndex++;
                LineNumber = endLineOneBased;
                if (Progress is not null)
                    Progress.Invoke(this, new ProgressEventArgs(resultIndex, -1, 0L, 0L));

                if (maxRecords > 0 && resultIndex >= maxRecords) break;
            }
            finally
            {
                li += physicalLinesInGroup;
            }
        }

        TotalRecords = resultIndex;
        return resultIndex;
    }

    /// <summary>
    /// OPT-08: Counts lines in a span using SIMD-accelerated IndexOfAny.
    /// </summary>
    private static int CountLinesSpan(ReadOnlySpan<char> content)
    {
        int count = 0;
        ReadOnlySpan<char> span = content;
        while (!span.IsEmpty)
        {
            int idx = span.IndexOfAny(s_newlineChars);
            if (idx < 0)
            {
                if (span.Length > 0 || count == 0)
                    count++;
                break;
            }
            count++;
            char terminator = span[idx];
            span = span[(idx + 1)..];
            if (terminator == '\r' && !span.IsEmpty && span[0] == '\n')
                span = span[1..];
        }
        return count;
    }

    /// <summary>
    /// OPT-08: Fills line boundary arrays from a span.
    /// </summary>
    private static void FillLineBoundariesSpan(ReadOnlySpan<char> content, int contentLength, int[] lineStarts, int[] lineLengths)
    {
        int pos = 0;
        int lineIndex = 0;
        while (pos < contentLength && lineIndex < lineStarts.Length)
        {
            int lineStart = pos;
            ReadOnlySpan<char> remaining = content[pos..];
            int idx = remaining.IndexOfAny(s_newlineChars);
            int lineEnd;
            if (idx < 0)
            {
                lineEnd = contentLength;
                pos = contentLength;
            }
            else
            {
                lineEnd = pos + idx;
                pos = lineEnd;
                if (pos < contentLength && content[pos] == '\r')
                    pos++;
                if (pos < contentLength && content[pos] == '\n')
                    pos++;
            }

            lineStarts[lineIndex] = lineStart;
            lineLengths[lineIndex] = lineEnd - lineStart;
            lineIndex++;
        }
    }

    /// <summary>
    /// OPT-06: Builds a joined string from line boundaries within a span.
    /// Materializes strings only for header/footer capture or multi-line records.
    /// Uses pre-sized buffer to avoid StringBuilder reallocations.
    /// </summary>
    private static string BuildJoinedStringSpan(
        ReadOnlySpan<char> content,
        int[] lineStarts,
        int[] lineLengths,
        int startIndex,
        int count,
        string separator)
    {
        if (count == 0) return string.Empty;
        if (count == 1)
        {
            return content[lineStarts[startIndex]..(lineStarts[startIndex] + lineLengths[startIndex])].ToString();
        }

        int totalLength = 0;
        for (int i = 0; i < count; i++)
        {
            totalLength += lineLengths[startIndex + i];
            if (i > 0) totalLength += separator.Length;
        }

        var buffer = new char[totalLength];
        int pos = 0;
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                separator.AsSpan().CopyTo(buffer.AsSpan(pos));
                pos += separator.Length;
            }
            int s = lineStarts[startIndex + i];
            int l = lineLengths[startIndex + i];
            content.Slice(s, l).CopyTo(buffer.AsSpan(pos));
            pos += l;
        }
        return new string(buffer);
    }

    /// <summary>
    /// Slow path for multiline records — materializes strings as before.
    /// Multiline is a rare, complex case; the fast path handles the common scenario.
    /// </summary>
    private T[] ReadCoreMultilinePath(string content, int maxRecords, RecordOptions opts)
    {
        var lines = new List<string>();
        var reader = new SpanLineReader(content.AsSpan());
        while (reader.TryReadLine(out var lineSpan))
        {
            lines.Add(new string(lineSpan));
        }

        if (opts.IgnoreFirst > 0 && lines.Count > 0)
        {
            int take = Math.Min(opts.IgnoreFirst, lines.Count);
            HeaderText = string.Join(NewLineForWrite, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lines)[..take]);
            lines.RemoveRange(0, take);
        }

        if (opts.IgnoreLast > 0 && lines.Count > 0)
        {
            int take = Math.Min(opts.IgnoreLast, lines.Count);
            FooterText = string.Join(NewLineForWrite, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lines)[(lines.Count - take)..]);
            lines.RemoveRange(lines.Count - take, take);
        }

        var multilineJoin = Parsing.MultilineLineJoin.Detect(content.AsSpan());
        var (assembledLines, logicalStartPhysicalIndices, logicalEndPhysicalIndices) =
            AssembleMultilineRecords(lines, multilineJoin);

        // OPT-05: Use pre-allocated T[] for result buffer in multiline path too.
        int assembledCount = assembledLines.Count;
        T[]? resultBuffer = assembledCount > 0 ? new T[assembledCount] : [];
        int resultIndex = 0;
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);
        for (int li = 0; li < assembledCount;)
        {
            int available = assembledLines.Count - li;
            bool incomplete = available < linesPerRecord;
            int physicalLinesInGroup = incomplete ? available : linesPerRecord;

            try
            {
                string firstLine = assembledLines[li];
                string joinedLogicalLine;
                if (physicalLinesInGroup == 1)
                {
                    joinedLogicalLine = firstLine;
                }
                else
                {
                    if (physicalLinesInGroup == 2)
                    {
                        joinedLogicalLine = string.Concat(assembledLines[li], "\n", assembledLines[li + 1]);
                    }
                    else
                    {
                        var sb = new StringBuilder();
                        for (int j = 0; j < physicalLinesInGroup; j++)
                        {
                            if (j > 0) sb.Append('\n');
                            sb.Append(assembledLines[li + j]);
                        }
                        joinedLogicalLine = sb.ToString();
                    }
                }

                int startLineOneBased = opts.IgnoreFirst + 1 + logicalStartPhysicalIndices[li];
                int endLineOneBased = opts.IgnoreFirst + 1 + logicalEndPhysicalIndices[li + physicalLinesInGroup - 1];

                if (opts.IgnoreEmptyLines && !incomplete)
                {
                    bool allEmpty = true;
                    for (int j = 0; j < linesPerRecord; j++)
                    {
                        if (assembledLines[li + j].Length != 0) { allEmpty = false; break; }
                    }
                    if (allEmpty) continue;
                }

                if (!incomplete && opts.CommentMarker != '\0' && firstLine.Length > 0 && firstLine[0] == opts.CommentMarker)
                    continue;

                if (!incomplete && _conditionMatcher is not null && !_conditionMatcher.ShouldInclude(firstLine.AsSpan()))
                    continue;

                string activeLine = joinedLogicalLine;
                if (!incomplete && BeforeReadRecord is not null)
                {
                    var beforeArgs = new BeforeReadEventArgs<T>(this, activeLine, startLineOneBased);
                    BeforeReadRecord.Invoke(this, beforeArgs);
                    if (beforeArgs.StopReading) break;
                    if (beforeArgs.SkipThisRecord) continue;
                    if (beforeArgs.RecordLineChanged) activeLine = beforeArgs.RecordLine;
                }

                T record;
                try
                {
                    if (incomplete)
                    {
                        throw new FormatException(
                            $"Expected {linesPerRecord} physical line(s) per logical record; only {available} line(s) remain after line {opts.IgnoreFirst + li + 1}.");
                    }

                    // MULT-04: Validate that multiline assembled lines have balanced quotes.
                    // The generated Parse code doesn't check quote balance, so we must validate here.
                    if (DelimitedFieldEnumerator.HasUnclosedQuote(activeLine.AsSpan()))
                    {
                        throw new FormatException($"Unclosed quoted field starting at line {startLineOneBased}. Check that all opening quotes have matching closing quotes (use \"\" for escaped quotes within quoted fields).");
                    }

                    record = ParseOrRecycle(activeLine.AsSpan(), linesPerRecord);
                }
                catch (Exception ex)
                {
                    switch (ErrorManager.ErrorMode)
                    {
                        case ErrorMode.ThrowException:
                            throw CreateParseContextException(startLineOneBased, activeLine, ex);
                        case ErrorMode.IgnoreAndContinue: continue;
                        case ErrorMode.SaveAndContinue:
                            ErrorManager.AddError(new ErrorInfo(startLineOneBased, activeLine, ex));
                            continue;
                    }
                    break;
                }

                if (AfterReadRecord is not null || s_hasNotifyRead)
                {
                    var afterArgs = new AfterReadEventArgs<T>(this, record, activeLine, endLineOneBased);
                    if (AfterReadRecord is not null)
                    {
                        AfterReadRecord.Invoke(this, afterArgs);
                        if (afterArgs.SkipThisRecord) continue;
                    }
                    if (s_hasNotifyRead && record is INotifyRead<T> notifyRead)
                    {
                        notifyRead.AfterRead(afterArgs);
                        if (afterArgs.SkipThisRecord) continue;
                    }
                }

                // OPT-05: Write directly into rented ArrayPool buffer.
                resultBuffer![resultIndex] = record;
                resultIndex++;
                LineNumber = endLineOneBased;
                if (Progress is not null)
                    Progress.Invoke(this, new ProgressEventArgs(resultIndex, -1, 0L, 0L));

                if (maxRecords > 0 && resultIndex >= maxRecords) break;
            }
            finally
            {
                li += physicalLinesInGroup;
            }
        }

        TotalRecords = resultIndex;

        // OPT-05: Return exact-size array from pre-allocated buffer.
        if (assembledCount == 0)
            return [];

        if (resultIndex == resultBuffer!.Length)
            return resultBuffer;

        var exactResult = new T[resultIndex];
        resultBuffer.AsSpan(0, resultIndex).CopyTo(exactResult);
        return exactResult;
    }

    /// <summary>
    /// OPT-01: Builds a joined string from line boundaries within the content string.
    /// Only materializes strings when needed (header/footer/multiline).
    /// </summary>
    private static string BuildJoinedString(
        string content,
        int[] lineStarts,
        int[] lineLengths,
        int startIndex,
        int count,
        string separator)
    {
        if (count == 0) return string.Empty;
        if (count == 1)
        {
            return content.Substring(lineStarts[startIndex], lineLengths[startIndex]);
        }

        int totalLen = 0;
        int sepLen = separator.Length;
        for (int i = 0; i < count; i++)
        {
            totalLen += lineLengths[startIndex + i];
            if (i > 0) totalLen += sepLen;
        }

        return string.Create(totalLen, (content, lineStarts, lineLengths, startIndex, count, separator), (span, state) =>
        {
            var (ctxContent, ctxStarts, ctxLengths, ctxStart, ctxCount, ctxSep) = state;
            int pos = 0;
            for (int i = 0; i < ctxCount; i++)
            {
                if (i > 0)
                {
                    ctxSep.AsSpan().CopyTo(span[pos..]);
                    pos += ctxSep.Length;
                }
                int s = ctxStarts[ctxStart + i];
                int l = ctxLengths[ctxStart + i];
                ctxContent.AsSpan(s, l).CopyTo(span[pos..]);
                pos += l;
            }
        });
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

    /// <summary>
    /// OPT-02: Validates delimited field count with quote-aware scanning.
    /// Skips validation when fields have per-field delimiter overrides — the generated
    /// Parse code handles those correctly and we cannot reliably count fields with mixed delimiters.
    /// </summary>
    private static void ValidateDelimitedFieldCount(
        ReadOnlySpan<char> line,
        string delimiter,
        int expectedFieldCount,
        ReadOnlySpan<FieldDescriptor> fields)
    {
        if (string.IsNullOrEmpty(delimiter) || expectedFieldCount <= 0)
            return;

        // Skip validation when any field has a per-field delimiter override.
        // The generated Parse code handles mixed delimiters correctly; we cannot
        // reliably count fields when different delimiters are used per field.
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].Delimiter is not null)
                return;
        }

        // Single-char delimiter: use quote-aware scan with IndexOfAny SIMD jumps (OPT-10)
        if (delimiter.Length == 1)
        {
            char delim = delimiter[0];
            int count = CountDelimitedFields(line, delim);

            if (count != expectedFieldCount)
                throw new FormatException($"Expected {expectedFieldCount} fields but found {count} in line: '{line}'. Check that all delimiters are present and no fields contain unescaped delimiter characters.");
            return;
        }

        // Multi-char delimiter: use quote-aware scan
        ValidateDelimitedFieldCountQuoted(line.ToString(), delimiter, expectedFieldCount);
    }

    /// <summary>
    /// Quote-aware character-by-character field count validation for multi-char delimiters
    /// or records with per-field delimiter overrides.
    /// </summary>
    private static void ValidateDelimitedFieldCountQuoted(
        string line,
        string delimiter,
        int expectedFieldCount)
    {
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
    /// OPT-10: Counts delimited fields using IndexOfAny SIMD jumps to skip
    /// non-quote/non-delimiter characters in bulk.
    /// </summary>
    private static int CountDelimitedFields(ReadOnlySpan<char> line, char delimiter)
    {
        int count = 1;
        bool inQuotes = false;
        int pos = 0;
        int len = line.Length;

        while (pos < len)
        {
            // Use IndexOfAny to jump to next quote or delimiter
            int jump = line[pos..].IndexOfAny('"', delimiter);
            if (jump < 0)
                break; // No more interesting characters

            pos += jump;
            char ch = line[pos];

            if (ch == '"')
            {
                // Check for escaped quote ("")
                if (inQuotes && pos + 1 < len && line[pos + 1] == '"')
                {
                    pos += 2;
                    continue;
                }
                inQuotes = !inQuotes;
                pos++;
                continue;
            }

            // delimiter found
            if (!inQuotes)
            {
                count++;
            }
            pos++;
        }

        return count;
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

    private static FormatException CreateParseContextException(int lineNumber, string line, Exception inner, int columnIndex = -1, string? fieldName = null)
    {
        var columnInfo = columnIndex >= 0 ? $", column {columnIndex + 1}" : "";
        var fieldInfo = fieldName != null ? $", field '{fieldName}'" : "";
        return new FormatException($"Error parsing line {lineNumber}{columnInfo}{fieldInfo}: '{line}'. {inner.Message}", inner);
    }

    /// <summary>
    /// OPT-12: Streaming callback-based read from a TextReader.
    /// Reads line-by-line (no full-file string allocation), accumulates logical records,
    /// and invokes <paramref name="callback"/> for each parsed record.
    /// </summary>
    private int ReadCoreStreamCallback(TextReader reader, Action<T> callback, int maxRecords)
    {
        LineNumber = 0;
        TotalRecords = 0;
        HeaderText = null;
        FooterText = null;
        ErrorManager.ClearErrors();

        var opts = _descriptor.Options;
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);
        bool hasBeforeRead = BeforeReadRecord is not null || s_hasNotifyRead;
        bool hasAfterRead = AfterReadRecord is not null || s_hasNotifyRead;

        // Phase 1: Read header lines
        int headerLinesRead = 0;
        if (opts.IgnoreFirst > 0)
        {
            var headerBuffer = new List<string>(opts.IgnoreFirst);
            for (int i = 0; i < opts.IgnoreFirst; i++)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    TotalRecords = 0;
                    return 0;
                }
                headerBuffer.Add(line);
                headerLinesRead++;
                LineNumber = headerLinesRead;
            }
            HeaderText = string.Join(NewLineForWrite, headerBuffer);
        }

        // Phase 2: Stream data records
        int resultIndex = 0;
        var lineBuffer = new List<string>(linesPerRecord);

        while (maxRecords <= 0 || resultIndex < maxRecords)
        {
            lineBuffer.Clear();
            int startLine = LineNumber + 1;

            // Accumulate lines for one logical record
            for (int i = 0; i < linesPerRecord; i++)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    if (i == 0)
                    {
                        // Clean EOF — no partial record
                        TotalRecords = resultIndex;
                        return resultIndex;
                    }
                    // Partial record at EOF — parse what we have
                    break;
                }
                lineBuffer.Add(line);
            }

            string joinedLine = linesPerRecord == 1 ? lineBuffer[0] : string.Join("\n", lineBuffer);

            // Skip empty lines
            if (opts.IgnoreEmptyLines && string.IsNullOrWhiteSpace(joinedLine))
                continue;

            // Skip comment lines
            if (opts.CommentMarker != '\0' && joinedLine.Length > 0 && joinedLine[0] == opts.CommentMarker)
                continue;

            // Skip conditional records
            if (_conditionMatcher is not null && !_conditionMatcher.ShouldInclude(joinedLine.AsSpan()))
                continue;

            // BeforeRead event
            if (hasBeforeRead)
            {
                var beforeArgs = new BeforeReadEventArgs<T>(this, joinedLine, startLine);
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
                    break;
                if (beforeArgs.SkipThisRecord)
                    continue;
                if (beforeArgs.RecordLineChanged)
                    joinedLine = beforeArgs.RecordLine;
            }

            T record;
            int endLine = LineNumber + lineBuffer.Count;
            try
            {
                ValidateDelimitedFieldCount(joinedLine.AsSpan(), opts.Delimiter, _descriptor.Fields.Length, _descriptor.Fields);
                record = ParseOrRecycle(joinedLine.AsSpan(), lineBuffer.Count);
            }
            catch (Exception ex)
            {
                switch (ErrorManager.ErrorMode)
                {
                    case ErrorMode.ThrowException:
                        throw CreateParseContextException(startLine, joinedLine, ex);
                    case ErrorMode.IgnoreAndContinue:
                        continue;
                    case ErrorMode.SaveAndContinue:
                        ErrorManager.AddError(new ErrorInfo(startLine, joinedLine, ex));
                        continue;
                }
                break;
            }

            // AfterRead event
            if (hasAfterRead)
            {
                var afterArgs = new AfterReadEventArgs<T>(this, record, joinedLine, endLine);
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

            callback(record);
            resultIndex++;
            LineNumber = endLine;

            if (Progress is not null)
                Progress.Invoke(this, new ProgressEventArgs(resultIndex, -1, 0L, 0L));
        }

        // Phase 3: Read footer lines
        if (opts.IgnoreLast > 0)
        {
            var footerBuffer = new List<string>(opts.IgnoreLast);
            for (int i = 0; i < opts.IgnoreLast; i++)
            {
                var line = reader.ReadLine();
                if (line == null) break;
                footerBuffer.Add(line);
                LineNumber++;
            }
            if (footerBuffer.Count > 0)
                FooterText = string.Join(NewLineForWrite, footerBuffer);
        }

        TotalRecords = resultIndex;
        return resultIndex;
    }

    /// <summary>
    /// OPT-12: Async streaming callback-based read from a TextReader.
    /// </summary>
    private static async Task<int> ReadCoreStreamCallbackAsync(TextReader reader, Action<T> callback, int maxRecords, CancellationToken cancellationToken)
    {
        var descriptor = FileHelperRegistry<T>.Descriptor;
        var opts = descriptor.Options;
        int linesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);

        int resultIndex = 0;
        var lineBuffer = new List<string>(linesPerRecord);

        while (!cancellationToken.IsCancellationRequested && (maxRecords <= 0 || resultIndex < maxRecords))
        {
            lineBuffer.Clear();

            for (int i = 0; i < linesPerRecord; i++)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null)
                {
                    if (i == 0)
                        return resultIndex;
                    break;
                }
                lineBuffer.Add(line);
            }

            string joinedLine = linesPerRecord == 1 ? lineBuffer[0] : string.Join("\n", lineBuffer);

            if (opts.IgnoreEmptyLines && string.IsNullOrWhiteSpace(joinedLine))
                continue;

            if (opts.CommentMarker != '\0' && joinedLine.Length > 0 && joinedLine[0] == opts.CommentMarker)
                continue;

            T record;
            try
            {
                ValidateDelimitedFieldCount(joinedLine.AsSpan(), opts.Delimiter, descriptor.Fields.Length, descriptor.Fields);
                record = descriptor.Parse(joinedLine.AsSpan(), lineCount: lineBuffer.Count);
            }
            catch
            {
                throw;
            }

            callback(record);
            resultIndex++;
        }

        return resultIndex;
    }

    /// <summary>
    /// CREC-04: Parses a record with optional recycling for class types.
    /// When RecycleRecords is enabled and _recyclable is available, reuses _recycled
    /// instance instead of allocating a new one. First call creates the instance.
    /// </summary>
    private T ParseOrRecycle(ReadOnlySpan<char> lineSpan, int lineCount)
    {
        if (RecycleRecords && _recyclable is not null)
        {
            if (_recycled is null)
            {
                _recycled = _descriptor.Parse(lineSpan, lineCount);
            }
            else
            {
                _recyclable.ParseInto(lineSpan, lineCount, _recycled);
            }
            return _recycled;
        }
        return _descriptor.Parse(lineSpan, lineCount);
    }

}
