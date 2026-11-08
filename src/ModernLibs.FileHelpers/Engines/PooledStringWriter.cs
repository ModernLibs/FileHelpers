using System.Buffers;
using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>
/// A <see cref="TextWriter"/> that collects text in pooled chunks and builds the result string once, for the
/// <c>WriteString</c> methods. A <see cref="StringWriter"/> allocates the chunks of its
/// <see cref="StringBuilder"/> and then copies them into the result, so it allocates the text twice.
/// </summary>
/// <remarks>
/// Call <see cref="ToString"/> before <see cref="Dispose(bool)"/>: disposing returns the chunks to the pool,
/// and a disposed writer has no text.
/// </remarks>
internal sealed class PooledStringWriter : TextWriter
{
    private const int FirstChunk = 4 * 1024;
    private const int MaxChunk = 1024 * 1024;

    /// <summary>Chunks filled to their full length, in order.</summary>
    private readonly List<char[]> _full = [];

    private char[] _current = ArrayPool<char>.Shared.Rent(FirstChunk);
    private int _position;
    private int _length;
    private bool _disposed;

    /// <summary>The encoding <see cref="StringWriter"/> reports.</summary>
    public override Encoding Encoding => Encoding.Unicode;

    public override void Write(char value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_position == _current.Length)
            NextChunk();
        _current[_position++] = value;
        _length = checked(_length + 1);
    }

    public override void Write(string? value) => Write(value.AsSpan());

    public override void Write(char[] buffer, int index, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Write(buffer.AsSpan(index, count));
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _length = checked(_length + buffer.Length);
        while (!buffer.IsEmpty)
        {
            if (_position == _current.Length)
                NextChunk();

            int count = Math.Min(_current.Length - _position, buffer.Length);
            buffer[..count].CopyTo(_current.AsSpan(_position));
            _position += count;
            buffer = buffer[count..];
        }
    }

    /// <summary>The text written so far.</summary>
    /// <exception cref="ObjectDisposedException">The writer was disposed.</exception>
    public override string ToString()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_length == 0)
            return string.Empty;

        return string.Create(_length, this, static (destination, self) =>
        {
            foreach (char[] chunk in self._full)
            {
                chunk.CopyTo(destination);
                destination = destination[chunk.Length..];
            }

            self._current.AsSpan(0, self._position).CopyTo(destination);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            foreach (char[] chunk in _full)
                ArrayPool<char>.Shared.Return(chunk);
            _full.Clear();
            ArrayPool<char>.Shared.Return(_current);
            _current = [];
            _position = 0;
            _length = 0;
        }

        base.Dispose(disposing);
    }

    private void NextChunk()
    {
        _full.Add(_current);
        _current = ArrayPool<char>.Shared.Rent(Math.Min(_current.Length * 2, MaxChunk));
        _position = 0;
    }
}
