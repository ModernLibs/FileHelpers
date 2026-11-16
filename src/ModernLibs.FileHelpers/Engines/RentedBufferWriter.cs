using System.Buffers;

namespace ModernLibs.FileHelpers;

/// <summary>
/// <see cref="IBufferWriter{T}"/> implementation backed by a single <see cref="ArrayPool{T}.Shared"/>
/// rental. Reused across every record in a write session via <see cref="Reset"/>;
/// grows by doubling on overflow and returns the old buffer to the pool before renting a
/// larger one. Disposal returns the current buffer.
/// </summary>
/// <remarks>
/// The first <see cref="RecordWriteBuffer"/>: 4.10 descriptors format into it through a <see cref="RecordWriter"/>,
/// 4.8 and 4.9 descriptors through <see cref="IBufferWriter{T}"/> with <see cref="RecordWriteBuffer.NewLine"/> set,
/// as before.
/// </remarks>
internal sealed class RentedBufferWriter : RecordWriteBuffer, IBufferWriter<char>, IDisposable
{
    private bool _disposed;

    public RentedBufferWriter(int initialSize)
        : base(ArrayPool<char>.Shared.Rent(initialSize))
    {
    }

    /// <summary>Span of chars written since the last <see cref="Reset"/>.</summary>
    public ReadOnlySpan<char> WrittenSpan => Storage.AsSpan(0, Committed);

    /// <summary><see cref="WrittenSpan"/> as memory, for an asynchronous write.</summary>
    public ReadOnlyMemory<char> WrittenMemory => Storage.AsMemory(0, Committed);

    /// <summary>Number of chars written since the last <see cref="Reset"/>.</summary>
    public int WrittenCount => Committed;

    /// <summary>Marks the buffer empty without returning it to the pool — reuse across records.</summary>
    public void Reset()
    {
        ThrowIfDisposed();
        Committed = 0;
    }

    public void Advance(int count)
    {
        ThrowIfDisposed();
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Advance count must be non-negative.");
        if (Committed + count > Storage.Length)
            throw new InvalidOperationException("Cannot advance past the current buffer capacity. Ensure GetSpan/GetMemory was called with a sufficient sizeHint.");
        Committed += count;
    }

    public Memory<char> GetMemory(int sizeHint = 0)
    {
        ThrowIfDisposed();
        EnsureCapacity(sizeHint);
        return Storage.AsMemory(Committed);
    }

    public Span<char> GetSpan(int sizeHint = 0)
    {
        ThrowIfDisposed();
        EnsureCapacity(sizeHint);
        return Storage.AsSpan(Committed);
    }

    /// <summary>
    /// Growth for a <see cref="RecordWriter"/>: a larger rental that holds the committed text and the
    /// <paramref name="used"/> chars of the record after it, with at least <paramref name="minFree"/> chars free.
    /// </summary>
    internal override Span<char> GrowRecord(int used, int minFree)
    {
        ThrowIfDisposed();
        Grow(checked(Committed + used + minFree), Committed + used);
        return Storage.AsSpan(Committed);
    }

    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeHint), sizeHint, "sizeHint must be non-negative.");

        // IBufferWriter<T> contract: sizeHint == 0 means "any amount > 0 is fine". Guarantee
        // at least one free slot in that case so callers never see a zero-length span.
        int minRequested = sizeHint == 0 ? 1 : sizeHint;
        int required = Committed + minRequested;
        if (required <= Storage.Length)
            return;

        Grow(required, Committed);
    }

    /// <summary>Rents an array of at least <paramref name="required"/> chars and keeps the first <paramref name="keep"/>.</summary>
    private void Grow(int required, int keep)
    {
        // Grow by doubling until the required capacity fits.
        // Math.Max seeds newSize at `required` when the buffer length is 0 (e.g. post-dispose),
        // preventing an infinite loop where 0 * 2 never reaches the required threshold.
        int newSize = (int)Math.Max(Math.Min(2L * Storage.Length, Array.MaxLength), required);

        char[] larger = ArrayPool<char>.Shared.Rent(newSize);
        Storage.AsSpan(0, keep).CopyTo(larger);
        ArrayPool<char>.Shared.Return(Storage);
        Storage = larger;
    }

    private void ThrowIfDisposed()
    {
        // Guard post-disposal reuse. Without this, GetSpan(0)/EnsureCapacity(0) on a
        // disposed writer would loop forever (buffer length 0 → newSize = 0 → never grows).
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (Storage.Length > 0)
            ArrayPool<char>.Shared.Return(Storage);
        Storage = [];
        Committed = 0;
    }
}
