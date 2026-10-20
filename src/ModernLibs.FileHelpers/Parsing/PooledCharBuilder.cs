using System.Buffers;

namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// Minimal growable <c>char</c> builder over <see cref="ArrayPool{T}"/> used to assemble multiline and
/// multi-physical-line records without per-record allocations.
/// </summary>
internal sealed class PooledCharBuilder : IDisposable
{
    private char[] _buffer = [];
    private int _length;

    /// <summary>The assembled characters.</summary>
    public ReadOnlySpan<char> WrittenSpan => _buffer.AsSpan(0, _length);

    public int Length => _length;

    public void Clear() => _length = 0;

    public void Append(char value)
    {
        Ensure(1);
        _buffer[_length++] = value;
    }

    public void Append(ReadOnlySpan<char> value)
    {
        Ensure(value.Length);
        value.CopyTo(_buffer.AsSpan(_length));
        _length += value.Length;
    }

    public override string ToString() => new(WrittenSpan);

    public void Dispose()
    {
        if (_buffer.Length == 0)
            return;
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = [];
        _length = 0;
    }

    private void Ensure(int additional)
    {
        int required = _length + additional;
        if (required <= _buffer.Length)
            return;

        var larger = ArrayPool<char>.Shared.Rent(Math.Max(Math.Max(256, _buffer.Length * 2), required));
        WrittenSpan.CopyTo(larger);
        if (_buffer.Length > 0)
            ArrayPool<char>.Shared.Return(_buffer);
        _buffer = larger;
    }
}
