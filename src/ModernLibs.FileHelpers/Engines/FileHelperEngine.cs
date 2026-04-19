// MA0051: ReadCore and WriteCore are single-pipeline methods; keeping each together is more
// readable than fragmenting into micro-methods that would obscure the flow.
#pragma warning disable MA0051
// CA1000: Nested generics are intentional — FileHelperEngine<T> is the public entry type.
#pragma warning disable CA1000

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

    /// <summary>
    /// Creates an engine instance backed by the source-generated descriptor for <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no descriptor is registered for <typeparamref name="T"/> — typically because the
    /// record type is missing <c>[DelimitedRecord]</c>/<c>[FixedLengthRecord]</c> or the source generator
    /// did not run. See <see cref="FileHelperRegistry{T}.Descriptor"/>.
    /// </exception>
    public FileHelperEngine()
    {
        _descriptor = FileHelperRegistry<T>.Descriptor;
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
        _descriptor.Format(record, buf);
        sw.Write(buf.WrittenSpan);
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

        // Step 5: Parse loop. Track the original line position so LineNumber reflects the file's
        // 1-based index of the last parsed line (D-12) — header lines already consumed from the list
        // still count toward that position.
        var result = new List<T>(lines.Count);
        int originalLineIndex = opts.IgnoreFirst; // 0-based; bumped to 1-based on entry

        foreach (var line in lines)
        {
            originalLineIndex++;

            // D-08: IgnoreEmptyLines — a line is empty iff it has no characters.
            if (opts.IgnoreEmptyLines && line.Length == 0)
                continue;

            // D-08: CommentMarker — '\0' signals "no marker"; otherwise skip lines beginning with it.
            if (opts.CommentMarker != '\0' && line.Length > 0 && line[0] == opts.CommentMarker)
                continue;

            var record = _descriptor.Parse(line.AsSpan());
            result.Add(record);
            LineNumber = originalLineIndex;

            // D-11: Early termination after N valid records.
            if (maxRecords > 0 && result.Count >= maxRecords)
                break;
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
            buf.Reset();
            _descriptor.Format(record, buf);
            writer.Write(buf.WrittenSpan);
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
    /// <see cref="IBufferWriter{T}"/> implementation backed by a single <see cref="ArrayPool{T}.Shared"/>
    /// rental. Reused across every record in a <see cref="WriteCore"/> session via <see cref="Reset"/>;
    /// grows by doubling on overflow (D-16) and returns the old buffer to the pool before renting a
    /// larger one. Disposal returns the current buffer.
    /// </summary>
    private sealed class RentedBufferWriter : IBufferWriter<char>, IDisposable
    {
        private char[] _buffer;
        private int _written;
        private bool _disposed;

        public RentedBufferWriter(int initialSize)
        {
            _buffer = ArrayPool<char>.Shared.Rent(initialSize);
            _written = 0;
        }

        /// <summary>Span of chars written since the last <see cref="Reset"/>.</summary>
        public ReadOnlySpan<char> WrittenSpan => _buffer.AsSpan(0, _written);

        /// <summary>Marks the buffer empty without returning it to the pool — reuse across records.</summary>
        public void Reset()
        {
            ThrowIfDisposed();
            _written = 0;
        }

        public void Advance(int count)
        {
            ThrowIfDisposed();
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Advance count must be non-negative.");
            if (_written + count > _buffer.Length)
                throw new InvalidOperationException("Cannot advance past the current buffer capacity. Ensure GetSpan/GetMemory was called with a sufficient sizeHint.");
            _written += count;
        }

        public Memory<char> GetMemory(int sizeHint = 0)
        {
            ThrowIfDisposed();
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(_written);
        }

        public Span<char> GetSpan(int sizeHint = 0)
        {
            ThrowIfDisposed();
            EnsureCapacity(sizeHint);
            return _buffer.AsSpan(_written);
        }

        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint), sizeHint, "sizeHint must be non-negative.");

            // IBufferWriter<T> contract: sizeHint == 0 means "any amount > 0 is fine". Guarantee
            // at least one free slot in that case so callers never see a zero-length span.
            int minRequested = sizeHint == 0 ? 1 : sizeHint;
            int required = _written + minRequested;
            if (required <= _buffer.Length)
                return;

            // D-16: Grow by doubling until the required capacity fits.
            int newSize = _buffer.Length * 2;
            while (newSize < required)
                newSize *= 2;

            char[] larger = ArrayPool<char>.Shared.Rent(newSize);
            _buffer.AsSpan(0, _written).CopyTo(larger);
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = larger;
        }

        private void ThrowIfDisposed()
        {
            // WR-01: guard post-disposal reuse. Without this, GetSpan(0)/EnsureCapacity(0) on a
            // disposed writer would loop forever (_buffer.Length == 0 → newSize = 0 → never grows).
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_buffer.Length > 0)
                ArrayPool<char>.Shared.Return(_buffer);
            _buffer = [];
            _written = 0;
        }
    }
}
