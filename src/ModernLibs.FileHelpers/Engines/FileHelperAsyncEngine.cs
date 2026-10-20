// CA1000: Nested generics are intentional — FileHelperAsyncEngine<T> is the public entry type.
#pragma warning disable CA1000
// CA1710/MA0046: Delegate names and signatures intentionally match FileHelpers compatibility surface.
#pragma warning disable CA1710, MA0046

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
/// <para>The engine streams the input through a pooled buffer — it does NOT materialise the whole file —
/// and runs the same record pipeline as <see cref="FileHelperEngine{T}"/> (header/footer, multiline
/// assembly before filters, field-count validation, <see cref="ErrorMode"/>, events, line numbers).</para>
///
/// <para><see cref="IEnumerable{T}"/> is implemented so callers can use <c>foreach</c> over an open
/// <c>BeginRead*</c> session. The engine does NOT open the reader itself on <see cref="GetEnumerator"/> —
/// <see cref="BeginReadFile"/>, <see cref="BeginReadStream"/>, or <see cref="BeginReadString"/> must be
/// called first.</para>
/// </summary>
/// <typeparam name="T">
/// A record type annotated with <see cref="DelimitedRecordAttribute"/> or
/// <see cref="FixedLengthRecordAttribute"/>. The source generator must have produced an
/// <see cref="IRecordDescriptor{T}"/> registered in <see cref="FileHelperRegistry{T}"/>
/// via a <c>[ModuleInitializer]</c> before the engine is constructed.
/// </typeparam>
public sealed class FileHelperAsyncEngine<T> : IFileHelperAsyncEngine<T>, IRecordReadHost<T>
{
    private readonly IRecordDescriptor<T> _descriptor;
    private readonly RecordReadCore<T> _core;
    private readonly bool _implementsNotifyWrite;   // T implements INotifyWrite<T>

    // ---- Reader state -------------------------------------------------------
    private TextReader? _reader;
    private bool _ownsReader;
    private TextReaderLineSource? _source;
    private RecordReadSession<T>? _session;
    private bool _endOfInput;
    private bool _streamingRead; // a ReadAsync enumeration is running
    private int _streamingReadId; // changed by Close(): ends the running ReadAsync enumeration

    // ---- Writer state -------------------------------------------------------
    private TextWriter? _writer;
    private bool _ownsWriter;
    private StringWriter? _stringWriter; // non-null only for BeginWriteString path
    private RentedBufferWriter? _writeBuf;

    private readonly ErrorManager _errorManager = new();

    // ---- Lifecycle ----------------------------------------------------------
    // NOTE: No _disposed flag — this engine is intentionally reusable after Close()/Dispose().
    // Close() is idempotent (nulls reader/writer), so calling Dispose() and then BeginRead*
    // again is a supported pattern (matches FileHelpers 3.x engine behavior).

    /// <summary>
    /// Creates an engine instance backed by the source-generated descriptor for <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no descriptor is registered for <typeparamref name="T"/> — typically because
    /// the record type is missing <c>[DelimitedRecord]</c>/<c>[FixedLengthRecord]</c> or the
    /// source generator did not run. See <see cref="FileHelperRegistry{T}.Descriptor"/>.
    /// </exception>
    public FileHelperAsyncEngine()
    {
        _descriptor = FileHelperRegistry<T>.Descriptor;
        _core = new RecordReadCore<T>(_descriptor);

        // Detect the INotifyWrite<T> implementation once at construction.
        _implementsNotifyWrite = typeof(INotifyWrite<T>).IsAssignableFrom(typeof(T));
    }

    // =========================================================================
    // Public properties
    // =========================================================================

    /// <summary>
    /// 1-based number of the last physical line of the most recently returned record (counting header,
    /// empty and comment lines so it reflects the original file position).
    /// </summary>
    public int LineNumber { get; private set; }

    /// <summary>Number of records returned since the last <c>BeginRead*</c>, or written since the last <c>BeginWrite*</c>.</summary>
    public int TotalRecords { get; private set; }

    /// <summary>
    /// Header lines captured from the leading <see cref="IgnoreFirstAttribute"/> lines during
    /// <c>BeginRead*</c>. Setting this property emits it as the first line in <c>BeginWrite*</c>.
    /// </summary>
    public string? HeaderText { get; set; }

    /// <summary>
    /// Footer lines captured from the trailing <see cref="IgnoreLastAttribute"/> lines once the end of the
    /// input is reached. Setting this property emits it as the last line on <see cref="Close"/>.
    /// </summary>
    public string? FooterText { get; set; }

    /// <summary>Line terminator written after each record. Defaults to <c>"\r\n"</c>.</summary>
    public string NewLineForWrite { get; set; } = "\r\n";

    /// <summary>
    /// Encoding used by <see cref="BeginReadFile"/>, <see cref="ReadAsync"/> and <see cref="BeginWriteFile"/>.
    /// Defaults to <see cref="Encoding.UTF8"/>. A byte order mark in a file being read wins over it.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// When <see langword="true"/>, <see cref="ReadNext()"/>, <c>foreach</c> and <see cref="ReadAsync"/> reuse a
    /// single <typeparamref name="T"/> instance (zero-alloc for class types). Array-returning reads
    /// (<see cref="ReadNext(int)"/>, <see cref="ReadRemaining"/>) are never affected. Requires
    /// <c>IRecyclableDescriptor&lt;T&gt;</c>; no effect for struct types.
    /// </summary>
    public bool RecycleRecords { get; set; }

    /// <summary>The last record returned by <see cref="ReadNext()"/>.</summary>
    public T? LastRecord { get; private set; }

    /// <summary>Shared error manager instance for this engine. Cleared when a read starts.</summary>
    public ErrorManager ErrorManager => _errorManager;

    /// <summary>Shortcut to <see cref="ErrorManager.ErrorMode"/>.</summary>
    public ErrorMode ErrorMode
    {
        get => _errorManager.ErrorMode;
        set => _errorManager.ErrorMode = value;
    }

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

    // =========================================================================
    // Blocking streaming read
    // =========================================================================

    /// <summary>
    /// Opens <paramref name="filePath"/> for streaming with the current
    /// <see cref="Encoding"/>. The engine owns the underlying <see cref="StreamReader"/>
    /// and disposes it on <see cref="Close"/>/<see cref="Dispose"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a read session is already open, a <see cref="ReadAsync"/> enumeration is running, or a write session is open.</exception>
    public void BeginReadFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ThrowIfReaderOpen();
        _reader = new StreamReader(filePath, Encoding, detectEncodingFromByteOrderMarks: true);
        _ownsReader = true;
        OpenReaderCore();
    }

    /// <summary>
    /// Uses <paramref name="reader"/> for streaming. The engine does NOT dispose
    /// the reader on <see cref="Close"/>/<see cref="Dispose"/> — the caller owns it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a read session is already open, a <see cref="ReadAsync"/> enumeration is running, or a write session is open.</exception>
    public void BeginReadStream(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ThrowIfReaderOpen();
        _reader = reader;
        _ownsReader = false;
        OpenReaderCore();
    }

    /// <summary>
    /// Wraps <paramref name="content"/> in a <see cref="StringReader"/> for streaming.
    /// The engine owns the underlying reader.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a read session is already open, a <see cref="ReadAsync"/> enumeration is running, or a write session is open.</exception>
    public void BeginReadString(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        ThrowIfReaderOpen();
        _reader = new StringReader(content);
        _ownsReader = true;
        OpenReaderCore();
    }

    // =========================================================================
    // Per-record pull
    // =========================================================================

    /// <summary>
    /// Returns the next parsed record, or <c>default</c> at end of input (for struct records use
    /// <see cref="ReadNext(int)"/>, <see cref="ReadRemaining"/> or <c>foreach</c>, which detect the end
    /// of input without relying on <c>default</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no read session is open.</exception>
    public T? ReadNext() => TryReadNext(out var record, allowRecycle: true) ? record : default;

    /// <summary>
    /// Returns up to <paramref name="count"/> records from the open read session.
    /// The returned array may be shorter than <paramref name="count"/> if the end of input is reached.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no read session is open.</exception>
    public T[] ReadNext(int count)
    {
        ThrowIfNoReader();
        var results = new List<T>(Math.Max(0, count));
        for (int i = 0; i < count && TryReadNext(out var record, allowRecycle: false); i++)
            results.Add(record);
        return results.ToArray();
    }

    /// <summary>
    /// Reads all remaining records from the current position until the end of input.
    /// </summary>
    /// <returns>Array of all remaining parsed records.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no read session is open.</exception>
    public T[] ReadRemaining()
    {
        ThrowIfNoReader();
        var results = new List<T>();
        while (TryReadNext(out var record, allowRecycle: false))
            results.Add(record);
        return results.ToArray();
    }

    /// <summary>
    /// Pulls the next record of the open session. End of input is signalled by the return value, never by a
    /// <c>null</c>/<c>default</c> record (struct records have no distinguishable default).
    /// </summary>
    internal bool TryReadNext(out T record, bool allowRecycle)
    {
        ThrowIfNoReader();
        var session = _session!;
        session.AllowRecycle = allowRecycle && RecycleRecords;

        while (!session.Stopped && !_endOfInput)
        {
            // After an error thrown by the previous pull, the lines that followed the failing line come first.
            if (session.HasPendingLines)
            {
                if (session.ResumePending())
                {
                    record = session.Current;
                    LastRecord = record;
                    SyncPosition(session);
                    return true;
                }

                continue;
            }

            if (!_source!.TryReadLine(out var line, out var ending))
            {
                _endOfInput = true;
                session.Complete();
                break;
            }

            if (session.Push(line, ending))
            {
                record = session.Current;
                LastRecord = record;
                SyncPosition(session);
                return true;
            }
        }

        SyncPosition(session);
        record = default!;
        return false;
    }

    // =========================================================================
    // IAsyncEnumerable
    // =========================================================================

    /// <summary>
    /// Streams records from <paramref name="filePath"/> via <see cref="PipelineLineReader"/>,
    /// with the same record semantics as the synchronous reads.
    /// </summary>
    /// <param name="filePath">Path to the file to read. Must not be null.</param>
    /// <param name="cancellationToken">
    /// Cancellation token. The <c>[EnumeratorCancellation]</c> attribute ensures callers using
    /// <c>.WithCancellation(ct)</c> have their token correctly merged with this parameter.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a <c>BeginRead*</c> or <c>BeginWrite*</c> session is open, or another <see cref="ReadAsync"/>
    /// enumeration is running on this engine: they share <see cref="LineNumber"/>, <see cref="TotalRecords"/>,
    /// <see cref="HeaderText"/>/<see cref="FooterText"/> and <see cref="ErrorManager"/>. Use a separate engine
    /// instance for concurrent reads.
    /// </exception>
    public async IAsyncEnumerable<T> ReadAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (_reader is not null)
            throw new InvalidOperationException("A BeginRead* session is open on this engine. Call Close() before ReadAsync, or use a separate engine instance.");
        ThrowIfStreamingRead();
        ThrowIfWriterOpenForRead();

        _streamingRead = true;
        int id = ++_streamingReadId;
        try
        {
            await foreach (var record in StreamFileAsync(filePath, id, cancellationToken).ConfigureAwait(false))
                yield return record;
        }
        finally
        {
            // After Close() the engine belongs to whatever session was opened since.
            if (id == _streamingReadId)
                _streamingRead = false;
        }
    }

    private async IAsyncEnumerable<T> StreamFileAsync(string filePath, int id, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        var lineReader = new PipelineLineReader(stream, Encoding);
        await using var lineReaderScope = lineReader.ConfigureAwait(false);
        using var session = BeginSession();
        session.AllowRecycle = RecycleRecords;
        try
        {
            while (id == _streamingReadId && !session.Stopped && await lineReader.ReadLineAsync(cancellationToken).ConfigureAwait(false))
            {
                if (session.Push(lineReader.CurrentLine.Span, lineReader.CurrentEnding))
                {
                    // A Close() from AfterReadRecord still returns this record, then ends the enumeration.
                    if (id == _streamingReadId)
                    {
                        LastRecord = session.Current;
                        SyncPosition(session);
                    }

                    yield return session.Current;
                }
            }

            if (id == _streamingReadId)
                session.Complete();
        }
        finally
        {
            if (id == _streamingReadId)
                SyncPosition(session);
        }
    }

    // =========================================================================
    // Streaming write
    // =========================================================================

    /// <summary>
    /// Creates or overwrites <paramref name="filePath"/> for streaming writes.
    /// The engine owns the <see cref="StreamWriter"/> and disposes it on <see cref="Close"/>/<see cref="Dispose"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a write session is already open, or a read session (or <see cref="ReadAsync"/> enumeration) is open.</exception>
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
    /// Like FileHelpers 3.x, <see cref="HeaderText"/> and <see cref="FooterText"/> are cleared so neither is
    /// written into the middle of the existing file.
    /// The engine owns the <see cref="StreamWriter"/> and disposes it on <see cref="Close"/>/<see cref="Dispose"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a write session is already open, or a read session (or <see cref="ReadAsync"/> enumeration) is open.</exception>
    public void BeginAppendFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ThrowIfWriterOpen();
        // Like AppendToFile: a missing final line terminator in the existing file is written first (3.x).
        _writer = WriteFraming.OpenAppender(filePath, Encoding, NewLineForWrite);
        _ownsWriter = true;

        // 3.x BeginAppendToFile: no header/footer in the middle of an existing file.
        HeaderText = null;
        FooterText = null;
        InitWriteSession();
    }

    /// <summary>
    /// Uses <paramref name="writer"/> for streaming writes. The engine does NOT dispose
    /// the writer on <see cref="Close"/>/<see cref="Dispose"/> — the caller owns it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a write session is already open, or a read session (or <see cref="ReadAsync"/> enumeration) is open.</exception>
    public void BeginWriteStream(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ThrowIfWriterOpen();
        _writer = writer;
        _ownsWriter = false;
        InitWriteSession();
    }

    /// <summary>
    /// Accumulates written records into an internal <see cref="StringWriter"/>.
    /// Retrieve the result via <see cref="EndWriteString"/> (which also calls <see cref="Close"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a write session is already open, or a read session (or <see cref="ReadAsync"/> enumeration) is open.</exception>
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
        // Close() writes FooterText first; a disposed StringWriter still returns its content.
        var stringWriter = _stringWriter;
        Close();
        return stringWriter?.ToString() ?? string.Empty;
    }

    // =========================================================================
    // Per-record push
    // =========================================================================

    /// <summary>
    /// Formats <paramref name="record"/> via the descriptor and writes it to the
    /// open write session, followed by <see cref="NewLineForWrite"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no write session is open.</exception>
    public void WriteNext(T record)
    {
        ThrowIfNoWriter();

        // BeforeWriteRecord event, then INotifyWrite<T>.BeforeWrite
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
        _writeBuf.NewLine = NewLineForWrite;
        _descriptor.Format(record, _writeBuf);

        // AfterWriteRecord event, then INotifyWrite<T>.AfterWrite
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

    /// <summary>Writes each record in <paramref name="records"/> via <see cref="WriteNext(T)"/>.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="records"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no write session is open.</exception>
    public void WriteNext(IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (var record in records)
            WriteNext(record);
    }

    // =========================================================================
    // Lifecycle
    // =========================================================================

    /// <summary>
    /// Flushes and closes the current read or write session. Idempotent.
    /// Writes <see cref="FooterText"/> (if set) before flushing the writer. A running
    /// <see cref="ReadAsync"/> enumeration ends at its next record, and the engine can start a new session
    /// even when that enumerator is never disposed.
    /// </summary>
    public void Close()
    {
        if (_streamingRead)
        {
            _streamingRead = false;
            _streamingReadId++;
        }

        // Flush writer session
        if (_writer is not null)
        {
            WriteFraming.WriteFrame(_writer, FooterText, NewLineForWrite);
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
        _session?.Dispose();
        _session = null;
        _source?.Dispose();
        _source = null;
        if (_reader is not null)
        {
            if (_ownsReader)
                _reader.Dispose();
            _reader = null;
            _ownsReader = false;
        }
    }

    /// <summary>
    /// Calls <see cref="Close"/>. Idempotent and reuse-safe — the engine can be
    /// opened again with a new <c>BeginRead*</c> or <c>BeginWrite*</c> call after disposal.
    /// This matches FileHelpers 3.x engine behavior where the engine object is reused across
    /// multiple file-processing sessions.
    /// </summary>
    public void Dispose()
    {
        Close();
    }

    // =========================================================================
    // IEnumerable<T>
    // =========================================================================

    /// <summary>
    /// Iterates the open read session until the end of input.
    /// A <see cref="BeginReadFile"/>, <see cref="BeginReadStream"/>, or
    /// <see cref="BeginReadString"/> call is required before enumerating.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no read session is open.</exception>
    public IEnumerator<T> GetEnumerator()
    {
        ThrowIfNoReader();
        while (TryReadNext(out var record, allowRecycle: true))
            yield return record;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>Resets the read-side state and starts a session on the shared record pipeline.</summary>
    private RecordReadSession<T> BeginSession()
    {
        LineNumber = 0;
        TotalRecords = 0;
        HeaderText = null;
        FooterText = null;
        LastRecord = default;
        _errorManager.ClearErrors();
        return new RecordReadSession<T>(_core, this, this, _errorManager, NewLineForWrite, maxRecords: 0);
    }

    /// <summary>Starts the session and consumes the <see cref="IgnoreFirstAttribute"/> header lines eagerly.</summary>
    private void OpenReaderCore()
    {
        _session = BeginSession();
        _source = new TextReaderLineSource(_reader!);
        _endOfInput = false;

        while (_session.InHeader)
        {
            if (!_source.TryReadLine(out var line, out var ending))
            {
                _endOfInput = true;
                _session.Complete();
                break;
            }

            _session.Push(line, ending);
        }
    }

    private void SyncPosition(RecordReadSession<T> session)
    {
        LineNumber = session.LastLineNumber;
        TotalRecords = session.RecordCount;
    }

    private void InitWriteSession()
    {
        int initialSize = Math.Max(4096, _descriptor.Fields.Length * 64);
        _writeBuf = new RentedBufferWriter(initialSize);
        TotalRecords = 0;

        WriteFraming.WriteFrame(_writer!, HeaderText, NewLineForWrite);
    }

    /// <summary>Guards <c>BeginRead*</c>: one read at a time, never while writing (3.x).</summary>
    private void ThrowIfReaderOpen()
    {
        if (_reader is not null)
            throw new InvalidOperationException("A read session is already open. Call Close() first.");
        ThrowIfStreamingRead();
        ThrowIfWriterOpenForRead();
    }

    private void ThrowIfStreamingRead()
    {
        if (_streamingRead)
            throw new InvalidOperationException("A ReadAsync enumeration is running on this engine. Finish or dispose it first, or use a separate engine instance.");
    }

    private void ThrowIfWriterOpenForRead()
    {
        if (_writer is not null)
            throw new InvalidOperationException("A write session is open on this engine. Call Close() before reading.");
    }

    private void ThrowIfNoReader()
    {
        if (_reader is null)
            throw new InvalidOperationException("No read session is open. Call BeginReadFile, BeginReadStream, or BeginReadString first.");
    }

    /// <summary>Guards <c>BeginWrite*</c>: one write at a time, never while reading (3.x).</summary>
    private void ThrowIfWriterOpen()
    {
        if (_writer is not null)
            throw new InvalidOperationException("A write session is already open. Call Close() first.");
        if (_reader is not null || _streamingRead)
            throw new InvalidOperationException("A read session is open on this engine. Call Close() (or finish the ReadAsync enumeration) before writing.");
    }

    private void ThrowIfNoWriter()
    {
        if (_writer is null)
            throw new InvalidOperationException("No write session is open. Call BeginWriteFile, BeginWriteStream, or BeginWriteString first.");
    }
}
