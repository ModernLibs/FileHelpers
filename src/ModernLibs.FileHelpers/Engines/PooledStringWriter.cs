using System.Buffers;
using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>
/// A <see cref="TextWriter"/> that collects text in pooled chunks and builds the result string once, for the
/// <c>WriteString</c> methods. A <see cref="StringWriter"/> allocates the chunks of its
/// <see cref="StringBuilder"/> and then copies them into the result, so it allocates the text twice.
/// </summary>
/// <remarks>
/// <para>Call <see cref="ToString"/> before <see cref="Dispose(bool)"/>: disposing returns the chunks to the pool,
/// and a disposed writer has no text.</para>
/// <para>The engines format records of a 4.10 descriptor straight into the current chunk through
/// <see cref="RecordBuffer"/> (P5, design 5.1). A record is always contiguous: when it does not fit in the rest of
/// the chunk, its written part moves to a new chunk of at least the size it needs, above <see cref="MaxChunk"/> when
/// one record needs that. So a chunk may end partly filled; each retired chunk keeps the length of its text.</para>
/// </remarks>
internal sealed class PooledStringWriter : TextWriter
{
    private const int FirstChunk = 4 * 1024;
    private const int MaxChunk = 1024 * 1024;

    private readonly Chunks _chunks = new();

    /// <summary>The encoding <see cref="StringWriter"/> reports.</summary>
    public override Encoding Encoding => Encoding.Unicode;

    /// <summary>
    /// The current chunk as a <see cref="RecordWriteBuffer"/>: a record formatted into it is appended to the text
    /// when <c>Format</c> returns. It shares the chunks with the <see cref="TextWriter"/> methods.
    /// </summary>
    internal RecordWriteBuffer RecordBuffer => _chunks;

    /// <summary>The records the engine formatted straight into <see cref="RecordBuffer"/> (tests).</summary>
    internal int RecordsFormattedInPlace { get; set; }

    public override void Write(char value)
    {
        ObjectDisposedException.ThrowIf(_chunks.Disposed, this);
        _chunks.Append(value);
    }

    public override void Write(string? value) => Write(value.AsSpan());

    public override void Write(char[] buffer, int index, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Write(buffer.AsSpan(index, count));
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        ObjectDisposedException.ThrowIf(_chunks.Disposed, this);
        _chunks.Append(buffer);
    }

    /// <summary>The text written so far.</summary>
    /// <exception cref="ObjectDisposedException">The writer was disposed.</exception>
    public override string ToString()
    {
        ObjectDisposedException.ThrowIf(_chunks.Disposed, this);
        return _chunks.Text();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _chunks.Dispose();

        base.Dispose(disposing);
    }

    /// <summary>
    /// The chunks: the retired ones with the length of their text, and the current one, which is the storage of the
    /// <see cref="RecordWriteBuffer"/> (its committed length is the text in it).
    /// </summary>
    private sealed class Chunks : RecordWriteBuffer
    {
        /// <summary>The retired chunks, in order, each with the length of its text.</summary>
        private readonly List<(char[] Chunk, int Length)> _retired = [];

        /// <summary>The length of the text in <see cref="_retired"/>.</summary>
        private int _retiredLength;

        public Chunks()
            : base(ArrayPool<char>.Shared.Rent(FirstChunk))
        {
        }

        public bool Disposed { get; private set; }

        public void Append(char value)
        {
            if (Committed == Storage.Length)
                NextChunk();
            Storage[Committed++] = value;
        }

        public void Append(ReadOnlySpan<char> text)
        {
            while (!text.IsEmpty)
            {
                if (Committed == Storage.Length)
                    NextChunk();

                int count = Math.Min(Storage.Length - Committed, text.Length);
                text[..count].CopyTo(Storage.AsSpan(Committed));
                Committed += count;
                text = text[count..];
            }
        }

        public string Text()
        {
            int length = checked(_retiredLength + Committed);
            if (length == 0)
                return string.Empty;

            return string.Create(length, this, static (destination, self) =>
            {
                foreach (var (chunk, used) in self._retired)
                {
                    chunk.AsSpan(0, used).CopyTo(destination);
                    destination = destination[used..];
                }

                self.Storage.AsSpan(0, self.Committed).CopyTo(destination);
            });
        }

        /// <summary>
        /// Moves the record being formatted to a new chunk with at least <paramref name="minFree"/> chars free after
        /// its <paramref name="used"/> chars; the chunk it leaves is retired with the text before the record.
        /// </summary>
        internal override Span<char> GrowRecord(int used, int minFree)
        {
            ObjectDisposedException.ThrowIf(Disposed, typeof(PooledStringWriter));
            int needed = checked(used + minFree);

            // Up to MaxChunk the chunks double as before; a record above it gets a chunk of its own that still grows
            // geometrically, so a long record written in small pieces is copied O(1) times per char.
            int size = needed <= MaxChunk
                ? Math.Max(Math.Min(Storage.Length * 2, MaxChunk), needed)
                : RentedBufferWriter.NextSize(used, needed);

            CheckRetire();
            char[] next = ArrayPool<char>.Shared.Rent(size);
            Storage.AsSpan(Committed, used).CopyTo(next);
            Retire(next);
            return next;
        }

        public void Dispose()
        {
            if (Disposed)
                return;

            Disposed = true;
            foreach (var (chunk, _) in _retired)
                ArrayPool<char>.Shared.Return(chunk);
            _retired.Clear();
            _retiredLength = 0;
            ArrayPool<char>.Shared.Return(Storage);
            Storage = [];
            Committed = 0;
        }

        /// <summary>The current chunk is full: retires it and starts one twice as long, up to <see cref="MaxChunk"/>.</summary>
        private void NextChunk()
        {
            CheckRetire();
            Retire(ArrayPool<char>.Shared.Rent(Math.Min(Storage.Length * 2, MaxChunk)));
        }

        /// <summary>
        /// Does the size check of <see cref="Retire"/> before a chunk is rented, so that an overflow throws with
        /// nothing rented and nothing to return.
        /// </summary>
        private void CheckRetire()
        {
            if (Committed != 0)
                _ = checked(_retiredLength + Committed);
        }

        /// <summary>
        /// Makes <paramref name="next"/> the current chunk, empty. The old one is retired with its text, or returned
        /// to the pool when it holds none.
        /// </summary>
        private void Retire(char[] next)
        {
            if (Committed == 0)
            {
                ArrayPool<char>.Shared.Return(Storage);
            }
            else
            {
                _retiredLength = checked(_retiredLength + Committed);
                _retired.Add((Storage, Committed));
            }

            Storage = next;
            Committed = 0;
        }
    }
}
