using System.Buffers;

namespace ModernLibs.FileHelpers;

/// <summary>
/// <see cref="IBufferWriter{T}"/> implementation backed by a single <see cref="ArrayPool{T}.Shared"/>
/// rental. Reused across every record in a write session via <see cref="Reset"/>;
/// grows by doubling on overflow and returns the old buffer to the pool before renting a
/// larger one. Disposal returns the current buffer.
/// </summary>
internal sealed class RentedBufferWriter : IBufferWriter<char>, IDisposable
{
    private char[] _buffer;
    private int _written;
    private bool _disposed;

    public RentedBufferWriter(int initialSize)
    {
        _buffer = ArrayPool<char>.Shared.Rent(initialSize);
        _written = 0;
    }

    /// <summary>
    /// The engine's <c>NewLineForWrite</c>, written before each <see cref="FieldInNewLineAttribute"/> field
    /// (see <see cref="RecordFormatting.WriteFieldNewLine"/>).
    /// </summary>
    public string NewLine { get; set; } = "\r\n";

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

        // Grow by doubling until the required capacity fits.
        // Math.Max seeds newSize at `required` when _buffer.Length is 0 (e.g. post-dispose),
        // preventing an infinite loop where 0 * 2 never reaches the required threshold.
        int newSize = Math.Max(_buffer.Length * 2, required);
        while (newSize < required)
            newSize *= 2;

        char[] larger = ArrayPool<char>.Shared.Rent(newSize);
        _buffer.AsSpan(0, _written).CopyTo(larger);
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = larger;
    }

    private void ThrowIfDisposed()
    {
        // Guard post-disposal reuse. Without this, GetSpan(0)/EnsureCapacity(0) on a
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
