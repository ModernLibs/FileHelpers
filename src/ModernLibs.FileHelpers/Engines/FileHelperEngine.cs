// CA1000: Nested generics are intentional — FileHelperEngine<T> is the public entry type.
#pragma warning disable CA1000
// CA1710/MA0046: Delegate names and signatures intentionally match FileHelpers compatibility surface.
#pragma warning disable CA1710, MA0046

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Batch-mode engine for reading and writing records of type <typeparamref name="T"/>.
/// FileHelpers 3.x-compatible API.
///
/// <para>Every read method — batch (<see cref="ReadString(string)"/>, <see cref="ReadSpan(ReadOnlySpan{char})"/>,
/// <see cref="ReadFile(string)"/>, <see cref="ReadStream(TextReader)"/>), callback, streaming
/// (<see cref="ReadSync(TextReader)"/>, <see cref="ReadFile(string, Action{T})"/>) and asynchronous
/// (<c>ReadAsync</c>, <see cref="ReadFileAsync(string, Action{T}, CancellationToken)"/>) — runs the same
/// record pipeline, so the same input and options yield the same records, <see cref="HeaderText"/>,
/// <see cref="FooterText"/>, errors and line numbers whichever method is used:
/// header (<see cref="IgnoreFirstAttribute"/>) and footer (<see cref="IgnoreLastAttribute"/>) are the original
/// first/last physical lines; multiline quoted fields are assembled before the empty-line, comment and
/// <see cref="ConditionalRecordAttribute"/> filters; failures are routed through <see cref="ErrorMode"/> with the
/// record's first line number; <see cref="BeforeReadRecord"/>/<see cref="AfterReadRecord"/> fire for every record.</para>
///
/// <para>Write pipeline: a single pooled <c>char[]</c> buffer per call; the descriptor writes field content
/// only and the engine appends <see cref="NewLineForWrite"/> after each record and the optional
/// <see cref="HeaderText"/>/<see cref="FooterText"/>.</para>
/// </summary>
/// <typeparam name="T">
/// A record type annotated with <see cref="DelimitedRecordAttribute"/> or
/// <see cref="FixedLengthRecordAttribute"/>. The source generator must have produced an
/// <see cref="IRecordDescriptor{T}"/> that is registered in <see cref="FileHelperRegistry{T}"/>
/// via a <c>[ModuleInitializer]</c> before the engine is constructed.
/// </typeparam>
public sealed class FileHelperEngine<T> : IFileHelperEngine<T>, IRecordReadHost<T>
{
    private readonly IRecordDescriptor<T> _descriptor;
    private readonly RecordReadCore<T> _core;
    private readonly bool _implementsNotifyWrite;   // T implements INotifyWrite<T>

    /// <summary>
    /// Creates an engine instance backed by the source-generated descriptor for <typeparamref name="T"/>.
    /// Uses <see cref="Encoding.UTF8"/> for file I/O operations.
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
        _core = new RecordReadCore<T>(_descriptor);

        // Detect the INotifyWrite<T> implementation once at construction.
        _implementsNotifyWrite = typeof(INotifyWrite<T>).IsAssignableFrom(typeof(T));
    }

    /// <summary>
    /// 1-based number of the last physical line of the last record successfully read by the most recent
    /// read (original file position, counting header, empty and comment lines). <c>0</c> after construction
    /// and after a read that produced no records.
    /// </summary>
    public int LineNumber { get; private set; }

    /// <summary>
    /// Number of records returned from the most recent read, or written by the most recent write.
    /// Reset to <c>0</c> at the start of each call.
    /// </summary>
    public int TotalRecords { get; private set; }

    /// <summary>
    /// Header captured from the leading <see cref="IgnoreFirstAttribute"/> lines during the most recent read, as in
    /// FileHelpers 3.x: each line (original text) followed by <see cref="Environment.NewLine"/>, the last one
    /// included. Empty (<c>""</c>) after construction, at the start of each read, and when
    /// <see cref="IgnoreFirstAttribute"/> is not set or the input had no lines.
    /// Setting this property emits it before the records in <see cref="WriteString"/>, <see cref="WriteStream"/> and
    /// <see cref="WriteFile"/> (when non-empty; <see cref="NewLineForWrite"/> is added unless it already ends with
    /// it or with <see cref="Environment.NewLine"/>); ignored by <c>AppendToFile</c>.
    /// </summary>
    public string? HeaderText { get; set; } = string.Empty;

    /// <summary>
    /// Builds the header line FileHelpers 3.x <c>GetFileHeader()</c> returns: the caption (<c>[FieldCaption]</c>,
    /// else the member name) of every field joined with one separator, without quoting or a line terminator.
    /// The separator is the delimiter of the first field for a delimited record, a tab for a fixed-length record.
    /// Does not read or set <see cref="HeaderText"/>; assign it yourself to write the header with the records.
    /// </summary>
    public string GetFileHeader() => FileHeaderBuilder.Build(_descriptor);

    /// <summary>
    /// Footer captured from the trailing <see cref="IgnoreLastAttribute"/> lines during the most recent read, as in
    /// FileHelpers 3.x: each line (original text) followed by <see cref="Environment.NewLine"/>, the last one
    /// included. Empty (<c>""</c>) after construction, at the start of each read, when
    /// <see cref="IgnoreLastAttribute"/> is not set or the input had no footer lines, and when a streaming read
    /// stopped before the end of the input.
    /// Setting this property emits it after the records in <see cref="WriteString"/>, <see cref="WriteStream"/> and
    /// <see cref="WriteFile"/> (when non-empty; <see cref="NewLineForWrite"/> is added unless it already ends with
    /// it or with <see cref="Environment.NewLine"/>); ignored by <c>AppendToFile</c>.
    /// </summary>
    public string? FooterText { get; set; } = string.Empty;

    /// <summary>
    /// Line terminator written after the header, each record and the footer, and inside records with
    /// <see cref="FieldInNewLineAttribute"/> fields. Defaults to <see cref="Environment.NewLine"/> (<c>"\n"</c> on
    /// Linux and macOS, <c>"\r\n"</c> on Windows), as in FileHelpers 3.x; 4.8 to 4.9.1 wrote <c>"\r\n"</c> on every
    /// platform, so set <c>"\r\n"</c> to keep that output. Reading does not use it.
    /// </summary>
    public string NewLineForWrite { get; set; } = Environment.NewLine;

    /// <summary>
    /// Encoding used by <see cref="ReadFile(string)"/>, <see cref="WriteFile"/>, and
    /// <see cref="AppendToFile(string, T)"/>/<see cref="AppendToFile(string, IEnumerable{T})"/>.
    /// Defaults to <see cref="Encoding.UTF8"/>. A byte order mark in a file being read wins over it.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>Shared error manager instance for this engine. Cleared at the start of every read.</summary>
    public ErrorManager ErrorManager { get; } = new ErrorManager();

    /// <summary>Shortcut to <see cref="ErrorManager.ErrorMode"/>.</summary>
    public ErrorMode ErrorMode
    {
        get => ErrorManager.ErrorMode;
        set => ErrorManager.ErrorMode = value;
    }

    /// <summary>
    /// When <see langword="true"/>, callback and streaming reads (<c>Action&lt;T&gt;</c>/<see cref="SpanRecordCallback{T}"/>
    /// overloads, <see cref="ReadSync(TextReader)"/>, <c>ReadAsync</c>, <see cref="ReadFileAsync(string, Action{T}, CancellationToken)"/>)
    /// reuse a single <typeparamref name="T"/> instance (zero-alloc for class types). Array-returning reads are
    /// never affected. Requires <c>IRecyclableDescriptor&lt;T&gt;</c> — emitted by the source generator for
    /// class record types without init-only fields. Has no effect for struct types.
    /// </summary>
    /// <remarks>
    /// <strong>Important:</strong> the callback receives the same reference each time. If the caller needs to
    /// retain record data, it must copy the values before the next record is read.
    /// </remarks>
    public bool RecycleRecords { get; set; }

    /// <summary>Raised before the text of each record is parsed; can replace the text, skip the record or stop reading.</summary>
    public event BeforeReadHandler<T>? BeforeReadRecord;

    /// <summary>Raised after each record is parsed, before it is returned; can drop the record.</summary>
    public event AfterReadHandler<T>? AfterReadRecord;

    /// <summary>Raised before each record is formatted; can leave the record out of the output.</summary>
    public event BeforeWriteHandler<T>? BeforeWriteRecord;

    /// <summary>Raised after each record is formatted, before its text is written; can replace the text.</summary>
    public event AfterWriteHandler<T>? AfterWriteRecord;

    /// <summary>Raised after each record a read returns (see <see cref="ProgressEventArgs"/>).</summary>
    public event ProgressChangeHandler? Progress;

    BeforeReadHandler<T>? IRecordReadHost<T>.BeforeReadHandler => BeforeReadRecord;

    AfterReadHandler<T>? IRecordReadHost<T>.AfterReadHandler => AfterReadRecord;

    ProgressChangeHandler? IRecordReadHost<T>.ProgressHandler => Progress;

    void IRecordReadHost<T>.OnHeaderText(string headerText) => HeaderText = headerText;

    void IRecordReadHost<T>.OnFooterText(string footerText) => FooterText = footerText;

    internal RecordReadCore<T> Core => _core;

    /// <summary>Read events that require sequential delivery (parallel reads fall back when present).</summary>
    internal bool HasReadEvents =>
        BeforeReadRecord is not null || AfterReadRecord is not null || Progress is not null || RecordReadCore<T>.HasNotifyRead;

    /// <summary>Publishes the outcome of a read performed outside a single session (parallel chunks).</summary>
    internal void ApplyReadResult(int lineNumber, int totalRecords, string headerText, string footerText, IReadOnlyList<ErrorInfo> errors)
    {
        LineNumber = lineNumber;
        TotalRecords = totalRecords;
        HeaderText = headerText;
        FooterText = footerText;
        ErrorManager.ClearErrors();
        foreach (var error in errors)
            ErrorManager.AddError(error);
    }

    // =========================================================================
    // Batch reads (whole input in memory)
    // =========================================================================

    /// <summary>
    /// Parses all records from <paramref name="content"/>. The input is scanned in place; besides the
    /// result array, no per-line or per-record allocation is made for struct records.
    /// </summary>
    public T[] ReadString(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ReadToArray(content.AsSpan(), maxRecords: 0);
    }

    /// <summary>
    /// Parses up to <paramref name="maxRecords"/> records from <paramref name="content"/>.
    /// <c>maxRecords &lt;= 0</c> means "no limit".
    /// </summary>
    public T[] ReadString(string content, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ReadToArray(content.AsSpan(), maxRecords);
    }

    /// <summary>
    /// Parses all records from a <see cref="ReadOnlySpan{T}"/> of characters (MemoryMappedFile,
    /// pooled buffers, pipeline readers) without copying the content.
    /// </summary>
    public T[] ReadSpan(ReadOnlySpan<char> content) => ReadToArray(content, maxRecords: 0);

    /// <summary>
    /// Parses up to <paramref name="maxRecords"/> records from a <see cref="ReadOnlySpan{T}"/>.
    /// <c>maxRecords &lt;= 0</c> means "no limit".
    /// </summary>
    public T[] ReadSpan(ReadOnlySpan<char> content, int maxRecords) => ReadToArray(content, maxRecords);

    /// <summary>
    /// Zero-allocation callback-based read from a <see cref="ReadOnlySpan{T}"/>.
    /// Invokes <paramref name="callback"/> for each parsed record without building a result array.
    /// </summary>
    /// <param name="content">The input content as a character span.</param>
    /// <param name="callback">Called once per successfully parsed record.</param>
    /// <returns>The number of records that were parsed and passed to the callback.</returns>
    public int ReadSpan(ReadOnlySpan<char> content, Action<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var sink = new ActionSink(callback);
        return ReadBatch(content, maxRecords: 0, allowRecycle: true, ref sink);
    }

    /// <summary>Zero-allocation callback-based read with a record-count cap.</summary>
    public int ReadSpan(ReadOnlySpan<char> content, Action<T> callback, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var sink = new ActionSink(callback);
        return ReadBatch(content, maxRecords, allowRecycle: true, ref sink);
    }

    /// <summary>
    /// Zero-allocation callback-based read from a string.
    /// Invokes <paramref name="callback"/> for each parsed record without building a result array.
    /// </summary>
    public int ReadString(string content, Action<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(content);
        var sink = new ActionSink(callback);
        return ReadBatch(content.AsSpan(), maxRecords: 0, allowRecycle: true, ref sink);
    }

    /// <summary>Zero-allocation callback-based read from a string with a record-count cap.</summary>
    public int ReadString(string content, Action<T> callback, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(content);
        var sink = new ActionSink(callback);
        return ReadBatch(content.AsSpan(), maxRecords, allowRecycle: true, ref sink);
    }

    /// <summary>
    /// Zero-allocation callback-based read from a <see cref="ReadOnlySpan{T}"/>
    /// that passes each record by reference to avoid struct copying.
    /// </summary>
    /// <param name="content">The input content as a character span.</param>
    /// <param name="callback">Called once per successfully parsed record, passed by reference.</param>
    /// <returns>The number of records that were parsed and passed to the callback.</returns>
    public int ReadSpan(ReadOnlySpan<char> content, SpanRecordCallback<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var sink = new RefSink(callback);
        return ReadBatch(content, maxRecords: 0, allowRecycle: true, ref sink);
    }

    /// <summary><see cref="ReadSpan(ReadOnlySpan{char}, SpanRecordCallback{T})"/> with a record-count cap.</summary>
    public int ReadSpan(ReadOnlySpan<char> content, SpanRecordCallback<T> callback, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var sink = new RefSink(callback);
        return ReadBatch(content, maxRecords, allowRecycle: true, ref sink);
    }

    /// <summary>
    /// Zero-allocation callback-based read from a string that passes
    /// each record by reference to avoid struct copying.
    /// </summary>
    public int ReadString(string content, SpanRecordCallback<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(content);
        var sink = new RefSink(callback);
        return ReadBatch(content.AsSpan(), maxRecords: 0, allowRecycle: true, ref sink);
    }

    /// <summary><see cref="ReadString(string, SpanRecordCallback{T})"/> with a record-count cap.</summary>
    public int ReadString(string content, SpanRecordCallback<T> callback, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(content);
        var sink = new RefSink(callback);
        return ReadBatch(content.AsSpan(), maxRecords, allowRecycle: true, ref sink);
    }

    /// <summary>
    /// Reads <paramref name="reader"/> to its end and parses it; the records, errors, header and footer are
    /// those of <see cref="ReadString(string)"/> for the same text. <paramref name="reader"/> is NOT disposed —
    /// the caller owns its lifetime (matches FileHelpers 3.x).
    /// </summary>
    public T[] ReadStream(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadToArray(reader, maxRecords: 0, readToEnd: true);
    }

    /// <summary>
    /// <see cref="ReadStream(TextReader)"/> with a record-count cap. The reader is still read to its end, so
    /// <see cref="FooterText"/> holds the last lines of the input.
    /// </summary>
    public T[] ReadStream(TextReader reader, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadToArray(reader, maxRecords, readToEnd: true);
    }

    /// <summary>
    /// Reads <paramref name="stream"/> from its current position to its end, decoding it with
    /// <see cref="Encoding"/> (a byte order mark wins), and parses it; the records, errors, header and footer are
    /// those of <see cref="ReadString(string)"/> for the same text. <paramref name="stream"/> is NOT disposed.
    /// </summary>
    public T[] ReadStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = FileBuffers.OpenReader(stream, Encoding);
        return ReadToArray(reader, maxRecords: 0, readToEnd: true);
    }

    /// <summary>
    /// <see cref="ReadStream(Stream)"/> with a record-count cap. The stream is still read to its end, so
    /// <see cref="FooterText"/> holds the last lines of the input.
    /// </summary>
    public T[] ReadStream(Stream stream, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = FileBuffers.OpenReader(stream, Encoding);
        return ReadToArray(reader, maxRecords, readToEnd: true);
    }

    /// <summary>
    /// Reads <paramref name="filePath"/> with the current <see cref="Encoding"/> (a byte order
    /// mark wins) and parses it as it is read; the records, errors, header and footer are those of
    /// <see cref="ReadString(string)"/> for the same text.
    /// </summary>
    public T[] ReadFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        using var reader = FileBuffers.OpenReader(filePath, Encoding);
        return ReadToArray(reader, maxRecords: 0, readToEnd: false);
    }

    /// <summary><see cref="ReadFile(string)"/> with a record-count cap.</summary>
    public T[] ReadFile(string filePath, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        using var reader = FileBuffers.OpenReader(filePath, Encoding);
        return ReadToArray(reader, maxRecords, readToEnd: false);
    }

    // =========================================================================
    // List results
    // =========================================================================

    /// <summary><see cref="ReadFile(string)"/> returning a <see cref="List{T}"/>.</summary>
    public List<T> ReadFileAsList(string filePath) => ReadFileAsList(filePath, maxRecords: 0);

    /// <summary><see cref="ReadFile(string, int)"/> returning a <see cref="List{T}"/>.</summary>
    public List<T> ReadFileAsList(string filePath, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        using var reader = FileBuffers.OpenReader(filePath, Encoding);
        return ReadToList(reader, maxRecords, readToEnd: false);
    }

    /// <summary><see cref="ReadString(string)"/> returning a <see cref="List{T}"/>.</summary>
    public List<T> ReadStringAsList(string content) => ReadStringAsList(content, maxRecords: 0);

    /// <summary><see cref="ReadString(string, int)"/> returning a <see cref="List{T}"/>.</summary>
    public List<T> ReadStringAsList(string content, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(content);
        var sink = new ArraySink();
        ReadBatch(content.AsSpan(), maxRecords, allowRecycle: false, ref sink);
        return sink.ToList();
    }

    /// <summary>
    /// <see cref="ReadStream(TextReader)"/> returning a <see cref="List{T}"/>; the reader is NOT disposed and is
    /// left at its end.
    /// </summary>
    public List<T> ReadStreamAsList(TextReader reader) => ReadStreamAsList(reader, maxRecords: 0);

    /// <summary><see cref="ReadStream(TextReader, int)"/> returning a <see cref="List{T}"/>.</summary>
    public List<T> ReadStreamAsList(TextReader reader, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadToList(reader, maxRecords, readToEnd: true);
    }

    // =========================================================================
    // Streaming reads
    // =========================================================================

    /// <summary>
    /// Streaming file read with callback — the file is read in chunks into a pooled buffer and no
    /// result array is built. Returns the total number of records processed.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="callback">Called once per successfully parsed record.</param>
    /// <returns>The number of records that were parsed and passed to the callback.</returns>
    public int ReadFile(string filePath, Action<T> callback)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(callback);
        using var sr = FileBuffers.OpenReader(filePath, Encoding);
        return ReadStreaming(sr, callback, maxRecords: 0);
    }

    /// <summary><see cref="ReadFile(string, Action{T})"/> with a record-count cap.</summary>
    public int ReadFile(string filePath, Action<T> callback, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(callback);
        using var sr = FileBuffers.OpenReader(filePath, Encoding);
        return ReadStreaming(sr, callback, maxRecords);
    }

    /// <summary>
    /// Async streaming file read with callback. Same record semantics as <see cref="ReadFile(string)"/>.
    /// </summary>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="callback">Called once per successfully parsed record.</param>
    /// <param name="cancellationToken">Token to cancel the read operation.</param>
    /// <returns>The number of records that were parsed and passed to the callback.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public Task<int> ReadFileAsync(string filePath, Action<T> callback, CancellationToken cancellationToken = default)
        => ReadFileAsync(filePath, callback, maxRecords: 0, cancellationToken);

    /// <summary><see cref="ReadFileAsync(string, Action{T}, CancellationToken)"/> with a record-count cap.</summary>
    public async Task<int> ReadFileAsync(string filePath, Action<T> callback, int maxRecords, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(callback);
        using var sr = FileBuffers.OpenAsyncReader(filePath, Encoding);
        using var source = new TextReaderLineSource(sr);
        using var session = BeginSession(maxRecords, allowRecycle: true);
        try
        {
            while (!session.Stopped && await source.ReadLineAsync(cancellationToken).ConfigureAwait(false))
            {
                if (session.Push(source.CurrentLine.Span, source.CurrentEnding))
                {
                    SyncPosition(session);
                    callback(session.Current);
                }
            }

            session.Complete();
        }
        finally
        {
            SyncPosition(session);
        }

        return session.RecordCount;
    }

    /// <summary>
    /// Asynchronous streaming read that yields records one at a time via
    /// <see cref="IAsyncEnumerable{T}"/>, reading <paramref name="filePath"/> through
    /// <see cref="PipelineLineReader"/>. Same record semantics as <see cref="ReadFile(string)"/>.
    /// </summary>
    /// <remarks>
    /// For class record types each record is a new allocation unless <see cref="RecycleRecords"/> is set.
    /// </remarks>
    /// <param name="filePath">Path to the file to read.</param>
    /// <param name="cancellationToken">Token to cancel the async enumeration.</param>
    /// <returns>An async enumerable of parsed records.</returns>
    public IAsyncEnumerable<T> ReadAsync(string filePath, CancellationToken cancellationToken = default) =>
        ReadPipelineAsync(filePath, stream: null, fromFile: true, cancellationToken);

    /// <summary>
    /// Asynchronous streaming read that yields records one at a time via <see cref="IAsyncEnumerable{T}"/>, reading
    /// <paramref name="stream"/> from its current position with asynchronous reads only (through
    /// <see cref="PipelineLineReader"/>). Decodes with <see cref="Encoding"/> (a byte order mark wins). Same record
    /// semantics as <see cref="ReadFile(string)"/>. <paramref name="stream"/> is NOT disposed, also when the
    /// enumeration ends early, fails or is cancelled.
    /// </summary>
    /// <remarks>
    /// <para>For class record types each record is a new allocation unless <see cref="RecycleRecords"/> is set.</para>
    /// <para>The stream is read ahead in blocks: after an early exit or a cancellation its position is past the
    /// last record returned, so reading on from it does not continue at the next record.</para>
    /// </remarks>
    /// <param name="stream">The stream to read; the caller owns it.</param>
    /// <param name="cancellationToken">Token to cancel the async enumeration.</param>
    /// <returns>An async enumerable of parsed records.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/> (thrown when the enumeration starts).</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public IAsyncEnumerable<T> ReadAsync(Stream stream, CancellationToken cancellationToken = default) =>
        ReadPipelineAsync(filePath: null, stream, fromFile: false, cancellationToken);

    /// <summary>
    /// Asynchronous streaming read that yields records one at a time via <see cref="IAsyncEnumerable{T}"/>, reading
    /// <paramref name="reader"/> with <see cref="TextReader.ReadAsync(Memory{char}, CancellationToken)"/>. Same record
    /// semantics as <see cref="ReadSync(TextReader)"/>. <paramref name="reader"/> is NOT disposed — the caller owns
    /// its lifetime.
    /// </summary>
    /// <remarks>
    /// <para>For class record types each record is a new allocation unless <see cref="RecycleRecords"/> is set.</para>
    /// <para>The reader is read ahead in blocks: after an early exit or a cancellation its position is past the
    /// last record returned, so reading on from it does not continue at the next record.</para>
    /// </remarks>
    /// <param name="reader">The text reader to read; the caller owns it.</param>
    /// <param name="cancellationToken">Token to cancel the async enumeration.</param>
    /// <returns>An async enumerable of parsed records.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/> (thrown when the enumeration starts).</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public IAsyncEnumerable<T> ReadAsync(TextReader reader, CancellationToken cancellationToken = default) =>
        ReadTextAsync(reader, cancellationToken);

    /// <summary>
    /// The <see cref="PipelineLineReader"/> read of <c>ReadAsync</c>: opens and owns <paramref name="filePath"/> when
    /// <paramref name="fromFile"/>, else reads the caller's <paramref name="stream"/> and leaves it open. The null
    /// check runs when the enumeration starts.
    /// </summary>
    private async IAsyncEnumerable<T> ReadPipelineAsync(
        string? filePath, Stream? stream, bool fromFile, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (fromFile)
            ArgumentNullException.ThrowIfNull(filePath);
        else
            ArgumentNullException.ThrowIfNull(stream);

        using var ownedStream = fromFile ? FileBuffers.OpenAsyncStream(filePath!) : null;
        var lineReader = new PipelineLineReader(ownedStream ?? stream!, Encoding);
        await using var lineReaderScope = lineReader.ConfigureAwait(false);
        using var session = BeginSession(maxRecords: 0, allowRecycle: true);
        try
        {
            while (!session.Stopped && await lineReader.ReadLineAsync(cancellationToken).ConfigureAwait(false))
            {
                if (session.Push(lineReader.CurrentLine.Span, lineReader.CurrentEnding))
                {
                    SyncPosition(session);
                    yield return session.Current;
                }
            }

            session.Complete();
        }
        finally
        {
            SyncPosition(session);
        }
    }

    /// <summary>The <see cref="TextReader"/> read of <c>ReadAsync</c>; the reader is not disposed.</summary>
    private async IAsyncEnumerable<T> ReadTextAsync(TextReader? reader, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);

        using var source = new TextReaderLineSource(reader);
        using var session = BeginSession(maxRecords: 0, allowRecycle: true);
        try
        {
            while (!session.Stopped && await source.ReadLineAsync(cancellationToken).ConfigureAwait(false))
            {
                if (session.Push(source.CurrentLine.Span, source.CurrentEnding))
                {
                    SyncPosition(session);
                    yield return session.Current;
                }
            }

            session.Complete();
        }
        finally
        {
            SyncPosition(session);
        }
    }

    /// <summary>
    /// Synchronous streaming read from a <see cref="TextReader"/> that yields
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

    /// <summary><see cref="ReadSync(TextReader)"/> with a record-count cap.</summary>
    public IEnumerable<T> ReadSync(TextReader reader, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadSyncCore(reader, maxRecords);
    }

    /// <summary>
    /// Synchronous streaming read from a string that yields records one at a time
    /// via <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <param name="content">The string content to stream from.</param>
    /// <returns>An enumerable of parsed records.</returns>
    public IEnumerable<T> ReadSync(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ReadSyncCore(new StringReader(content), maxRecords: 0);
    }

    /// <summary><see cref="ReadSync(string)"/> with a record-count cap.</summary>
    public IEnumerable<T> ReadSync(string content, int maxRecords)
    {
        ArgumentNullException.ThrowIfNull(content);
        return ReadSyncCore(new StringReader(content), maxRecords);
    }

    private IEnumerable<T> ReadSyncCore(TextReader reader, int maxRecords)
    {
        using var source = new TextReaderLineSource(reader);
        using var session = BeginSession(maxRecords, allowRecycle: true);
        try
        {
            while (!session.Stopped && PushNextLine(source, session, out bool produced))
            {
                if (produced)
                {
                    SyncPosition(session);
                    yield return session.Current;
                }
            }

            session.Complete();
        }
        finally
        {
            SyncPosition(session);
        }
    }

    // =========================================================================
    // Writes
    // =========================================================================

    /// <summary>
    /// Formats all <paramref name="records"/> into a single string using the descriptor's
    /// Format body, with <see cref="NewLineForWrite"/> after each record and optional
    /// <see cref="HeaderText"/>/<see cref="FooterText"/> framing.
    /// </summary>
    public string WriteString(IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        using var sw = new PooledStringWriter();
        WriteCore(sw, records, includeHeaderFooter: true);
        return sw.ToString();
    }

    /// <summary>
    /// Writes <paramref name="records"/> to <paramref name="writer"/>. The writer is NOT
    /// disposed by the engine — the caller owns its lifetime.
    /// </summary>
    public void WriteStream(TextWriter writer, IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(records);
        WriteCore(writer, records, includeHeaderFooter: true);
    }

    /// <summary>
    /// Asynchronously writes <paramref name="records"/> to <paramref name="writer"/>: the text of
    /// <see cref="WriteStream(TextWriter, IEnumerable{T})"/>, with <see cref="HeaderText"/>, <see cref="FooterText"/>
    /// and the write events. Records are formatted synchronously into a buffer that is written with one
    /// <see cref="TextWriter.WriteAsync(ReadOnlyMemory{char}, CancellationToken)"/> call each time it holds 16,384
    /// characters or more, and at the end; then <see cref="TextWriter.FlushAsync(CancellationToken)"/> is awaited.
    /// The writer is NOT disposed — the caller owns its lifetime.
    /// </summary>
    /// <remarks>
    /// <para>Each <c>WriteAsync</c> call receives whole records. What the writer does with a block is up to the writer:
    /// a <see cref="StreamWriter"/>, for example, can pass part of a block to its stream before it observes a
    /// cancellation. To get whole records in a stream after a cancellation, use
    /// <see cref="WriteStreamAsync(Stream, IEnumerable{T}, CancellationToken)"/>.</para>
    /// <para>When <paramref name="records"/>, a write event or the formatting of a record throws, the records
    /// formatted before it are written and flushed, and the exception is rethrown; a failure of that last write is
    /// discarded, so the caller always gets the original exception. When <paramref name="cancellationToken"/> is
    /// cancelled, the records formatted since the last block are dropped. <see cref="TotalRecords"/> is set when the
    /// write completes.</para>
    /// </remarks>
    /// <param name="writer">The writer to write to; the caller owns it.</param>
    /// <param name="records">The records to write.</param>
    /// <param name="cancellationToken">Token to cancel the write; checked before each record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> or <paramref name="records"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async Task WriteStreamAsync(TextWriter writer, IEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(records);
        using var target = new AsyncBlockWriter(writer);
        await WriteCoreAsync(target, records, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously writes <paramref name="records"/> to <paramref name="stream"/> with asynchronous writes only,
    /// encoded with <see cref="Encoding"/>: the bytes <see cref="WriteFile"/> writes to a new file, including the
    /// encoding's preamble (when the stream is not seekable or at position 0). Otherwise as
    /// <see cref="WriteStreamAsync(TextWriter, IEnumerable{T}, CancellationToken)"/>. <paramref name="stream"/> is NOT
    /// disposed — the caller owns its lifetime.
    /// </summary>
    /// <remarks>
    /// Each block of whole records (16,384 characters or more, and the rest at the end) is encoded by one encoder and
    /// written with a single <see cref="Stream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/> call; the token is
    /// checked before each record and each block, and nothing, not even the preamble, is written after a
    /// cancellation the engine observes. A cancellation therefore leaves whole records in the stream, unless the
    /// stream itself writes part of a buffer before it honours the cancellation: that is outside the engine's
    /// control.
    /// </remarks>
    /// <param name="stream">The writable stream to write to; the caller owns it.</param>
    /// <param name="records">The records to write.</param>
    /// <param name="cancellationToken">Token to cancel the write; checked before each record and each block.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="records"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="stream"/> is not writable.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async Task WriteStreamAsync(Stream stream, IEnumerable<T> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(records);
        if (!stream.CanWrite)
            throw new ArgumentException("The stream is not writable.", nameof(stream));

        using var target = new AsyncBlockWriter(stream, Encoding);
        await WriteCoreAsync(target, records, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates or overwrites <paramref name="filePath"/> and writes <paramref name="records"/>
    /// using the current <see cref="Encoding"/>.
    /// </summary>
    public void WriteFile(string filePath, IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(records);
        using var sw = FileBuffers.OpenWriter(filePath, append: false, Encoding);
        WriteCore(sw, records, includeHeaderFooter: true);
    }

    /// <summary>
    /// Appends a single <paramref name="record"/> to <paramref name="filePath"/> using
    /// <c>FileMode.Append</c>. Does NOT write <see cref="HeaderText"/> or <see cref="FooterText"/>. When the file
    /// does not end with a line terminator one is written first (3.x).
    /// <para>Avoids the 1-element <c>T[]</c> boxing the enumerable overload would incur by
    /// formatting the record directly into a rented buffer. Behaviour is identical to calling
    /// <see cref="AppendToFile(string, IEnumerable{T})"/> with a single-element enumerable.</para>
    /// </summary>
    public void AppendToFile(string filePath, T record)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        TotalRecords = 0;
        using var sw = WriteFraming.OpenAppender(filePath, Encoding, NewLineForWrite);
        int initialSize = Math.Max(4096, _descriptor.Fields.Length * 64);
        using var buf = new RentedBufferWriter(initialSize);

        // BeforeWriteRecord event, then INotifyWrite<T>.BeforeWrite
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

        buf.NewLine = NewLineForWrite;
        _descriptor.Format(record, buf);

        // AfterWriteRecord event, then INotifyWrite<T>.AfterWrite
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
        sw.Write(NewLineForWrite); // The engine, not the descriptor, writes the record terminator.
        TotalRecords = 1;
    }

    /// <summary>
    /// Appends all <paramref name="records"/> to <paramref name="filePath"/> using
    /// <c>FileMode.Append</c>. Does NOT write <see cref="HeaderText"/> or <see cref="FooterText"/>. When the file
    /// does not end with a line terminator one is written first (3.x).
    /// </summary>
    public void AppendToFile(string filePath, IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(records);
        using var sw = WriteFraming.OpenAppender(filePath, Encoding, NewLineForWrite);
        WriteCore(sw, records, includeHeaderFooter: false);
    }

    // =========================================================================
    // Read pipeline plumbing
    // =========================================================================

    /// <summary>Resets the read-side state and starts a session on the shared record pipeline.</summary>
    private RecordReadSession<T> BeginSession(int maxRecords, bool allowRecycle, bool keepFooterAfterStop = false)
    {
        LineNumber = 0;
        TotalRecords = 0;
        HeaderText = string.Empty;
        FooterText = string.Empty;
        ErrorManager.ClearErrors();
        return new RecordReadSession<T>(_core, this, this, ErrorManager, maxRecords)
        {
            AllowRecycle = allowRecycle && RecycleRecords,
            KeepFooterAfterStop = keepFooterAfterStop,
        };
    }

    private void SyncPosition(RecordReadSession<T> session)
    {
        LineNumber = session.LastLineNumber;
        TotalRecords = session.RecordCount;
    }

    private static bool PushNextLine(TextReaderLineSource source, RecordReadSession<T> session, out bool produced)
    {
        if (!source.TryReadLine(out var line, out var ending))
        {
            produced = false;
            return false;
        }

        produced = session.Push(line, ending);
        return true;
    }

    private T[] ReadToArray(ReadOnlySpan<char> content, int maxRecords)
    {
        var sink = new ArraySink();
        ReadBatch(content, maxRecords, allowRecycle: false, ref sink);
        return sink.ToArray();
    }

    /// <summary>
    /// Parses <paramref name="reader"/> as it is read into a result array, with the outcome of
    /// <see cref="ReadToArray(ReadOnlySpan{char}, int)"/> over its whole text: after a stop (max records or
    /// <c>StopReading</c>) the lines are still read when <c>IgnoreLast</c> needs the footer.
    /// </summary>
    /// <param name="reader">The input; not disposed.</param>
    /// <param name="maxRecords">Record cap; 0 or less reads every record.</param>
    /// <param name="readToEnd">Leave the reader at its end afterwards, as <see cref="TextReader.ReadToEnd"/> did.</param>
    private T[] ReadToArray(TextReader reader, int maxRecords, bool readToEnd)
    {
        var sink = new ChunkedArraySink();
        try
        {
            ReadToSink(reader, maxRecords, readToEnd, ref sink);
            return sink.ToArray();
        }
        finally
        {
            sink.Dispose();
        }
    }

    /// <summary><see cref="ReadToArray(TextReader, int, bool)"/> collecting into a <see cref="List{T}"/>.</summary>
    private List<T> ReadToList(TextReader reader, int maxRecords, bool readToEnd)
    {
        var sink = new ChunkedArraySink();
        try
        {
            ReadToSink(reader, maxRecords, readToEnd, ref sink);
            return sink.ToList();
        }
        finally
        {
            sink.Dispose();
        }
    }

    private void ReadToSink(TextReader reader, int maxRecords, bool readToEnd, ref ChunkedArraySink sink)
    {
        sink.Initialize(maxRecords > 0 ? Math.Min(maxRecords, ChunkedArraySink.InitialCapacity) : ChunkedArraySink.InitialCapacity);
        bool completed = false;
        try
        {
            using var source = new TextReaderLineSource(reader);
            using var session = BeginSession(maxRecords, allowRecycle: false, keepFooterAfterStop: true);
            try
            {
                while (source.TryReadLine(out var line, out var ending))
                {
                    if (session.Push(line, ending))
                    {
                        SyncPosition(session);
                        sink.Add(ref session.Current);
                    }

                    if (session.Stopped && !session.WantsFooterLines)
                        break;
                }

                session.Complete();
            }
            finally
            {
                SyncPosition(session);
            }

            completed = true;
            if (readToEnd)
                SkipToEnd(reader);
        }
        finally
        {
            if (readToEnd && !completed)
                SkipToEndAfterFault(reader);
        }
    }

    /// <summary>
    /// Leaves the reader at its end after the read failed. A fault of the reader itself while draining is
    /// discarded, so the caller gets the exception of the read.
    /// </summary>
    private static void SkipToEndAfterFault(TextReader reader)
    {
        try
        {
            SkipToEnd(reader);
        }
#pragma warning disable CA1031, RCS1075 // The read already failed; its exception is the one the caller gets.
        catch (Exception)
        {
        }
#pragma warning restore CA1031, RCS1075
    }

    private static void SkipToEnd(TextReader reader)
    {
        char[] buffer = ArrayPool<char>.Shared.Rent(4096);
        try
        {
            while (reader.Read(buffer, 0, buffer.Length) > 0)
            {
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>Runs a complete in-memory input through the pipeline, delivering records to <paramref name="sink"/>.</summary>
    private int ReadBatch<TSink>(ReadOnlySpan<char> content, int maxRecords, bool allowRecycle, ref TSink sink)
        where TSink : struct, IRecordSink
    {
        using var session = BeginSession(maxRecords, allowRecycle);
        try
        {
            // The line count sizes the result array and finds the footer without buffering lines. A callback
            // read without footer lines needs neither, so it does not pay for a pass over the whole input
            // (which a record cap would otherwise not read).
            if (typeof(TSink) == typeof(ArraySink) || session.HasFooterLines)
                session.SetKnownLineCount(LineSplitter.CountLines(content));

            // A read stops at maxRecords records, so a result never needs more slots, whatever the line count.
            int capacity = session.DataLineCapacity;
            if (maxRecords > 0 && maxRecords < capacity)
                capacity = maxRecords;
            sink.Initialize(capacity);

            var lines = new SpanLines(content);
            while (lines.TryRead(out var line, out var ending))
            {
                if (session.Push(line, ending))
                {
                    SyncPosition(session);
                    sink.Add(ref session.Current);
                }

                if (session.Stopped && !session.WantsFooterLines)
                    break;
            }

            session.Complete();
        }
        finally
        {
            SyncPosition(session);
        }

        return session.RecordCount;
    }

    private int ReadStreaming(TextReader reader, Action<T> callback, int maxRecords)
    {
        using var source = new TextReaderLineSource(reader);
        using var session = BeginSession(maxRecords, allowRecycle: true);
        try
        {
            while (!session.Stopped && source.TryReadLine(out var line, out var ending))
            {
                if (session.Push(line, ending))
                {
                    SyncPosition(session);
                    callback(session.Current);
                }
            }

            session.Complete();
        }
        finally
        {
            SyncPosition(session);
        }

        return session.RecordCount;
    }

    private interface IRecordSink
    {
        void Initialize(int capacity);

        void Add(ref T record);
    }

    private struct ArraySink : IRecordSink
    {
        private T[] _buffer;
        private int _count;

        public void Initialize(int capacity) => _buffer = capacity > 0 ? new T[capacity] : [];

        public void Add(ref T record) => _buffer[_count++] = record;

        public readonly T[] ToArray()
        {
            if (_buffer is null || _count == 0)
                return [];
            if (_count == _buffer.Length)
                return _buffer;
            return _buffer.AsSpan(0, _count).ToArray();
        }

        public readonly List<T> ToList()
        {
            var list = new List<T>();
            if (_buffer is null || _count == 0)
                return list;

            CollectionsMarshal.SetCount(list, _count);
            _buffer.AsSpan(0, _count).CopyTo(CollectionsMarshal.AsSpan(list));
            return list;
        }
    }

    /// <summary>
    /// Collects the records of an input of unknown length in chunks rented from <see cref="ArrayPool{T}"/>, so a
    /// record is never copied when the buffer grows; <see cref="ToArray"/> copies each record once into the array
    /// returned. Chunks start at the initial capacity and double up to <see cref="MaxChunkBytes"/>, below the large
    /// object heap unless one record is larger than about 1.3 KB (the 64-record first chunk then is already above
    /// it, as the 4.9.0 buffer was). <see cref="Dispose"/> returns chunks to the pool only up to <see cref="MaxPooledBytes"/> and
    /// leaves the rest to the GC, so a large read does not leave megabytes of records' worth of arrays in the
    /// shared pool after it ends.
    /// </summary>
    private struct ChunkedArraySink : IRecordSink, IDisposable
    {
        public const int InitialCapacity = 64;

        /// <summary>Largest chunk, in bytes; arrays of 85,000 bytes or more go to the large object heap.</summary>
        private const int MaxChunkBytes = 64 * 1024;

        /// <summary>Most chunk bytes given back to the pool when the read ends.</summary>
        private const int MaxPooledBytes = 1024 * 1024;

        private T[][]? _chunks;
        private int _chunkCount;
        private T[] _current;
        private int _used; // records in _current
        private int _count; // records in all chunks

        public void Initialize(int capacity)
        {
            _chunks = new T[8][];
            _current = ArrayPool<T>.Shared.Rent(Math.Max(capacity, 1));
            _chunks[0] = _current;
            _chunkCount = 1;
        }

        public void Add(ref T record)
        {
            if (_used == _current.Length)
                NextChunk();
            _current[_used++] = record;
            _count++;
        }

        public readonly T[] ToArray()
        {
            if (_count == 0)
                return [];

            var result = GC.AllocateUninitializedArray<T>(_count);
            var destination = result.AsSpan();
            for (int i = 0; i < _chunkCount; i++)
            {
                var chunk = _chunks![i];
                int length = i == _chunkCount - 1 ? _used : chunk.Length;
                chunk.AsSpan(0, length).CopyTo(destination);
                destination = destination[length..];
            }

            return result;
        }

        public readonly List<T> ToList()
        {
            var list = new List<T>();
            if (_count == 0)
                return list;

            CollectionsMarshal.SetCount(list, _count);
            var destination = CollectionsMarshal.AsSpan(list);
            for (int i = 0; i < _chunkCount; i++)
            {
                var chunk = _chunks![i];
                int length = i == _chunkCount - 1 ? _used : chunk.Length;
                chunk.AsSpan(0, length).CopyTo(destination);
                destination = destination[length..];
            }

            return list;
        }

        public void Dispose()
        {
            if (_chunks is null)
                return;

            long pooledBytes = 0;
            int elementSize = Unsafe.SizeOf<T>();
            for (int i = 0; i < _chunkCount; i++)
            {
                var chunk = _chunks[i];
                long bytes = (long)chunk.Length * elementSize;
                if (pooledBytes + bytes > MaxPooledBytes)
                    continue; // Not returned: the GC reclaims it with the records it holds.

                pooledBytes += bytes;
                // Clears the records first, so the pool does not keep them alive.
                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                    chunk.AsSpan(0, i == _chunkCount - 1 ? _used : chunk.Length).Clear();
                ArrayPool<T>.Shared.Return(chunk);
            }

            _chunks = null;
            _chunkCount = 0;
            _current = null!;
            _used = 0;
            _count = 0;
        }

        private void NextChunk()
        {
            // ArrayPool rounds every request up to a power of two, so the cap is rounded down to one: otherwise a
            // 56-byte record would ask for 1,170 elements and get 2,048 (114,688 bytes, on the large object heap).
            int maxLength = Math.Max(InitialCapacity, 1 << BitOperations.Log2((uint)Math.Max(1, MaxChunkBytes / Unsafe.SizeOf<T>())));
            int length = Math.Max(Math.Min(_current.Length * 2, maxLength), _current.Length);

            if (_chunkCount == _chunks!.Length)
                Array.Resize(ref _chunks, _chunkCount * 2);

            _current = ArrayPool<T>.Shared.Rent(length);
            _chunks[_chunkCount++] = _current;
            _used = 0;
        }
    }

    private readonly struct ActionSink(Action<T> callback) : IRecordSink
    {
        public void Initialize(int capacity)
        {
        }

        public void Add(ref T record) => callback(record);
    }

    private readonly struct RefSink(SpanRecordCallback<T> callback) : IRecordSink
    {
        public void Initialize(int capacity)
        {
        }

        public void Add(ref T record) => callback(ref record);
    }

    /// <summary>
    /// Core write pipeline. Rents one <see cref="RentedBufferWriter"/>
    /// per session, emits optional <see cref="HeaderText"/>, calls
    /// <see cref="IRecordDescriptor{T}.Format(T, IBufferWriter{char})"/> per record followed by
    /// <see cref="NewLineForWrite"/>, and emits optional <see cref="FooterText"/>.
    /// </summary>
    // MA0051: single write pipeline kept together for readability.
#pragma warning disable MA0051
    private void WriteCore(TextWriter writer, IEnumerable<T> records, bool includeHeaderFooter)
    {
        TotalRecords = 0;

        // Initial buffer size: at least 4096 chars, or 64 chars per field for wide records.
        int initialSize = Math.Max(4096, _descriptor.Fields.Length * 64);
        using var buf = new RentedBufferWriter(initialSize);

        // Header (write pipeline only).
        if (includeHeaderFooter)
            WriteFraming.WriteFrame(writer, HeaderText, NewLineForWrite);

        int count = 0;
        foreach (var record in records)
        {
            if (!WriteRecordText(writer, record, count + 1, buf))
                continue;
            writer.Write(NewLineForWrite); // The engine, not the descriptor, writes the record terminator.
            count++;
        }

        // Footer.
        if (includeHeaderFooter)
            WriteFraming.WriteFrame(writer, FooterText, NewLineForWrite);

        TotalRecords = count;
    }
#pragma warning restore MA0051

    /// <summary>Characters <see cref="WriteCoreAsync"/> buffers before it writes them as one block.</summary>
    private const int AsyncFlushThreshold = 16 * 1024;

    /// <summary>
    /// Asynchronous twin of <see cref="WriteCore"/> (header, records, footer): formats whole records into a pending
    /// buffer with <see cref="FillPending"/> and writes it to <paramref name="target"/> one block at a time.
    /// </summary>
    private async Task WriteCoreAsync(AsyncBlockWriter target, IEnumerable<T> records, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TotalRecords = 0;

        int initialSize = Math.Max(4096, _descriptor.Fields.Length * 64);
        using var buf = new RentedBufferWriter(initialSize);
        using var pending = new RentedBufferWriter(2 * AsyncFlushThreshold);

        WriteFraming.WriteFrame(pending, HeaderText, NewLineForWrite);

        int count = 0;
        using (var enumerator = records.GetEnumerator())
        {
            bool ended;
            do
            {
                try
                {
                    ended = FillPending(enumerator, buf, pending, ref count, cancellationToken);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // The records formatted before the fault reach the target, as with the synchronous WriteStream.
                    await WriteAfterFaultAsync(target, pending, cancellationToken).ConfigureAwait(false);
                    throw;
                }

                // The last block also takes the footer.
                if (!ended)
                    await WritePendingAsync(target, pending, final: false, cancellationToken).ConfigureAwait(false);
            }
            while (!ended);
        }

        WriteFraming.WriteFrame(pending, FooterText, NewLineForWrite);
        await WritePendingAsync(target, pending, final: true, cancellationToken).ConfigureAwait(false);
        await target.FlushAsync(cancellationToken).ConfigureAwait(false);

        TotalRecords = count;
    }

    private static async Task WritePendingAsync(AsyncBlockWriter target, RentedBufferWriter pending, bool final, CancellationToken cancellationToken)
    {
        if (pending.WrittenCount == 0 && !final)
            return;

        await target.WriteAsync(pending.WrittenMemory, final, cancellationToken).ConfigureAwait(false);
        pending.Reset();
    }

    /// <summary>
    /// Best effort after a fault of the records, an event or a formatting: writes and flushes the whole records still
    /// pending. Its own failure, a cancellation included, is discarded, so the caller gets the original exception.
    /// </summary>
    private static async Task WriteAfterFaultAsync(AsyncBlockWriter target, RentedBufferWriter pending, CancellationToken cancellationToken)
    {
        try
        {
            // final: true also covers a fault before the first block: the stream then gets the preamble, as WriteFile
            // does when the records fail on the first record. Do not "fix" this.
            await WritePendingAsync(target, pending, final: true, cancellationToken).ConfigureAwait(false);
            await target.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031, RCS1075 // The write already failed; its exception is the one the caller gets.
        catch (Exception)
        {
        }
#pragma warning restore CA1031, RCS1075
    }

    /// <summary>
    /// Writes one record for <see cref="MultiRecordEngine"/>: the text of <c>WriteString([record])</c> without its
    /// terminator (nothing when an event skips the record). Each record is record number 1, as it was when the
    /// multi-record engine wrote every record with its own <c>WriteString</c> call.
    /// </summary>
    internal void WriteMultiRecordText(TextWriter writer, T record, RentedBufferWriter buffer) =>
        WriteRecordText(writer, record, recordNumber: 1, buffer);

    /// <summary>
    /// Raises the write events and writes the formatted <paramref name="record"/> without its terminator;
    /// <see langword="false"/> when an event skipped it.
    /// </summary>
    private bool WriteRecordText(TextWriter writer, T record, int recordNumber, RentedBufferWriter buf)
    {
        if (!FormatRecord(record, recordNumber, buf, out string? eventLine))
            return false;

        if (eventLine is not null)
            writer.Write(eventLine);
        else
            writer.Write(buf.WrittenSpan);
        return true;
    }

    /// <summary>
    /// Formats records of <paramref name="records"/> into <paramref name="pending"/>, each followed by
    /// <see cref="NewLineForWrite"/>, until it holds <see cref="AsyncFlushThreshold"/> characters or more or the
    /// records end; <see langword="true"/> when they ended. Only complete records are ever in
    /// <paramref name="pending"/>: a record whose formatting or events throw adds nothing.
    /// </summary>
    private bool FillPending(
        IEnumerator<T> records, RentedBufferWriter buf, RentedBufferWriter pending, ref int count, CancellationToken cancellationToken)
    {
        while (pending.WrittenCount < AsyncFlushThreshold)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!records.MoveNext())
                return true;

            if (!FormatRecord(records.Current, count + 1, buf, out string? eventLine))
                continue;

            pending.Write(eventLine is not null ? eventLine.AsSpan() : buf.WrittenSpan);
            pending.Write(NewLineForWrite.AsSpan()); // The engine, not the descriptor, writes the record terminator.
            count++;
        }

        return false;
    }

    /// <summary>
    /// The per-record work of every write: raises the write events and formats <paramref name="record"/> without
    /// its terminator into <paramref name="buf"/>; <see langword="false"/> when an event skipped it. The record's
    /// text is <paramref name="eventLine"/> when write events ran (<see cref="AfterWriteRecord"/> can replace it),
    /// otherwise <see cref="RentedBufferWriter.WrittenSpan"/> of <paramref name="buf"/>.
    /// </summary>
    private bool FormatRecord(T record, int recordNumber, RentedBufferWriter buf, out string? eventLine)
    {
        eventLine = null;

        // BeforeWriteRecord event, then INotifyWrite<T>.BeforeWrite
        if (BeforeWriteRecord is not null || _implementsNotifyWrite)
        {
            var beforeArgs = new BeforeWriteEventArgs<T>(this, record, recordNumber);

            if (BeforeWriteRecord is not null)
            {
                BeforeWriteRecord.Invoke(this, beforeArgs);
                if (beforeArgs.SkipThisRecord)
                    return false;
            }

            if (_implementsNotifyWrite && record is INotifyWrite<T> notifyWrite)
            {
                notifyWrite.BeforeWrite(beforeArgs);
                if (beforeArgs.SkipThisRecord)
                    return false;
            }
        }

        buf.Reset();
        buf.NewLine = NewLineForWrite;
        _descriptor.Format(record, buf);

        // AfterWriteRecord event, then INotifyWrite<T>.AfterWrite
        if (AfterWriteRecord is not null || _implementsNotifyWrite)
        {
            string formattedLine = buf.WrittenSpan.ToString();
            var afterArgs = new AfterWriteEventArgs<T>(this, record, recordNumber, formattedLine);

            if (AfterWriteRecord is not null)
            {
                AfterWriteRecord.Invoke(this, afterArgs);
            }

            if (_implementsNotifyWrite && record is INotifyWrite<T> notifyWrite)
            {
                notifyWrite.AfterWrite(afterArgs);
            }

            // A handler that sets RecordLine to null writes nothing, as TextWriter.Write(null) did.
            eventLine = afterArgs.RecordLine ?? string.Empty;
        }

        return true;
    }
}
