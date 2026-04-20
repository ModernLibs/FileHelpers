// MA0051: OpenReaderCore and ReadNext contain the full streaming pipeline; fragmenting them
// into micro-methods would obscure the ring-buffer state-machine flow.
#pragma warning disable MA0051
// CA1000: Nested generics are intentional — FileHelperAsyncEngine<T> is the public entry type.
#pragma warning disable CA1000
// MA0004: Library code; ConfigureAwait(false) not used per project pattern (callers control context).
#pragma warning disable MA0004

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
public sealed class FileHelperAsyncEngine<T> : IEnumerable<T>, IDisposable
{
    private readonly IRecordDescriptor<T> _descriptor;

    // ---- Reader state -------------------------------------------------------
    private TextReader? _reader;
    private bool _ownsReader;
    private string? _currentLine;      // pre-fetched content line (null = EOF)
    private Queue<string>? _tailBuffer; // ring buffer for IgnoreLast

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
        // Cast once at construction: non-null only for class record types (IRecyclableDescriptor<T>
        // is emitted by the source generator for class types only). Struct types return null here.
        _recyclable = _descriptor as IRecyclableDescriptor<T>;
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
        var opts = _descriptor.Options;

        // Ring-buffer / filter loop — advances until a parseable line is dequeued.
        while (_currentLine is not null)
        {
            string line = _currentLine;

            // Pre-fetch the next raw line before any filtering so the ring buffer always
            // holds the most-recent N lines (where N = opts.IgnoreLast).
            _currentLine = _reader!.ReadLine();
            if (_currentLine is not null) LineNumber++;

            // IgnoreLast ring buffer: hold lines in the queue; only yield after the
            // queue overflows (the oldest dequeued line is safe to parse).
            if (opts.IgnoreLast > 0)
            {
                _tailBuffer!.Enqueue(line);
                if (_tailBuffer.Count <= opts.IgnoreLast)
                    continue; // buffer not yet full — don't yield this line yet
                line = _tailBuffer.Dequeue();
            }

            // Line-level filters
            if (opts.IgnoreEmptyLines && line.Length == 0) continue;
            if (opts.CommentMarker != '\0' && line.Length > 0 && line[0] == opts.CommentMarker) continue;

            // Parse — RecycleRecords path wired in Plan 04.
            T record;
            if (RecycleRecords && _recyclable is not null && _recycled is not null)
            {
                _recyclable.ParseInto(line.AsSpan(), _recycled);
                record = _recycled;
            }
            else
            {
                record = _descriptor.Parse(line.AsSpan());
            }

            TotalRecords++;
            LastRecord = record;
            _recycled = record;
            return record;
        }

        // EOF: drain remaining tail buffer lines into FooterText.
        if (opts.IgnoreLast > 0 && _tailBuffer!.Count > 0)
        {
            FooterText = string.Join(NewLineForWrite, _tailBuffer);
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

    // =========================================================================
    // IAsyncEnumerable stub — ENGI-11 (Plan 04)
    // =========================================================================

    /// <summary>
    /// ENGI-11: Returns an <see cref="IAsyncEnumerable{T}"/> that streams records from
    /// <paramref name="filePath"/> via <see cref="PipelineLineReader"/>.
    /// Honors <see cref="IgnoreFirstAttribute"/>, <see cref="IgnoreEmptyLinesAttribute"/>,
    /// and <see cref="IgnoreCommentedLinesAttribute"/> filters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Known limitation:</strong> <see cref="IgnoreLastAttribute"/> is <em>not</em> honored
    /// by this method. Trailing lines that would be skipped by <c>[IgnoreLast(N)]</c> will be
    /// parsed as data records instead. Use <see cref="BeginReadFile"/>/<see cref="ReadNext()"/>
    /// (the blocking pull model) when <c>IgnoreLast</c> semantics are required — it implements a
    /// ring-buffer that correctly defers the final N lines until EOF is confirmed.
    /// </para>
    /// </remarks>
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

        var opts = _descriptor.Options;
        int ignoredFirst = 0;

        using var stream = File.OpenRead(filePath);
        await using var lineReader = new PipelineLineReader(stream, Encoding);

        await foreach (var line in lineReader.ReadLinesAsync(cancellationToken))
        {
            // IgnoreFirst: skip the first N lines (header rows)
            if (ignoredFirst < opts.IgnoreFirst)
            {
                ignoredFirst++;
                continue;
            }

            // IgnoreEmptyLines
            if (opts.IgnoreEmptyLines && line.Length == 0) continue;

            // CommentMarker
            if (opts.CommentMarker != '\0' && line.Length > 0 && line.Span[0] == opts.CommentMarker) continue;

            cancellationToken.ThrowIfCancellationRequested();
            yield return _descriptor.Parse(line.Span);
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
        _writeBuf!.Reset();
        _descriptor.Format(record, _writeBuf);
        _writer!.Write(_writeBuf.WrittenSpan);
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

        // Consume IgnoreFirst header lines and capture them into HeaderText.
        var headerLines = new List<string>(opts.IgnoreFirst);
        for (int i = 0; i < opts.IgnoreFirst; i++)
        {
            string? line = _reader!.ReadLine();
            if (line is null) break;
            headerLines.Add(line);
            LineNumber++;
        }
        if (headerLines.Count > 0)
            HeaderText = string.Join(NewLineForWrite, headerLines);

        // Initialise ring buffer for IgnoreLast (capacity = N+1 to detect overflow).
        if (opts.IgnoreLast > 0)
            _tailBuffer = new Queue<string>(opts.IgnoreLast + 1);

        // Pre-read first content line.
        _currentLine = _reader!.ReadLine();
        if (_currentLine is not null) LineNumber++;
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
}
