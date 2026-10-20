using System.Buffers;

namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// Growable pooled <c>char</c> buffer that streaming sources fill with decoded characters and from
/// which complete lines are taken using <see cref="LineSplitter"/>. A <c>\r</c> at the end of the filled
/// data is kept until the next fill (or end of input) decides whether it starts a <c>\r\n</c>.
/// A pending partial line is scanned once: later fills only search the newly committed characters, so a
/// very long line costs linear, not quadratic, time.
/// </summary>
/// <remarks>
/// A line returned by <see cref="TryTakeLine"/> stays valid until the next call to
/// <see cref="GetWritableSpan"/>/<see cref="GetWritableMemory"/> (which may compact or grow the buffer).
/// </remarks>
internal sealed class CharLineBuffer : IDisposable
{
    private char[] _buffer;
    private int _start;
    private int _end;

    // Characters after _start already known to contain no line terminator.
    private int _scanned;

    public CharLineBuffer(int initialSize = 4096)
    {
        _buffer = ArrayPool<char>.Shared.Rent(initialSize);
    }

    /// <summary><see langword="true"/> once the producer signalled that no more characters follow.</summary>
    public bool EndOfInput { get; private set; }

    /// <summary>The underlying array; lines are slices of it.</summary>
    public char[] Array => _buffer;

    /// <summary>Takes the next complete line, if one is available.</summary>
    public bool TryTakeLine(out int lineStart, out int lineLength, out LineEnding ending)
    {
        var pending = _buffer.AsSpan(_start, _end - _start);
        int skip = _scanned;
        if (!LineSplitter.TryFindLine(pending[skip..], EndOfInput, out lineLength, out int consumed, out ending))
        {
            if (EndOfInput && skip > 0)
            {
                // Only already-scanned characters remain: they are the final line.
                lineStart = _start;
                lineLength = skip;
                _start = _end;
                _scanned = 0;
                return true;
            }

            // No terminator yet; a trailing '\r' is examined again once the next fill shows what follows it.
            _scanned = pending.Length > 0 && pending[^1] == '\r' ? pending.Length - 1 : pending.Length;
            lineStart = 0;
            return false;
        }

        lineStart = _start;
        lineLength += skip;
        _start += consumed + skip;
        _scanned = 0;
        return true;
    }

    /// <summary>Returns free space of at least <paramref name="minimumFree"/> chars after the pending data.</summary>
    public Span<char> GetWritableSpan(int minimumFree) => EnsureFree(minimumFree).AsSpan(_end);

    /// <summary>Memory form of <see cref="GetWritableSpan"/> for asynchronous producers.</summary>
    public Memory<char> GetWritableMemory(int minimumFree) => EnsureFree(minimumFree).AsMemory(_end);

    /// <summary>Marks <paramref name="count"/> chars written into the writable space as filled.</summary>
    public void Commit(int count) => _end += count;

    /// <summary>Signals that no more characters will be committed.</summary>
    public void MarkEndOfInput() => EndOfInput = true;

    public void Dispose()
    {
        if (_buffer.Length == 0)
            return;
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = [];
        _start = 0;
        _end = 0;
        _scanned = 0;
    }

    private char[] EnsureFree(int minimumFree)
    {
        int pending = _end - _start;
        if (_buffer.Length - _end >= minimumFree)
            return _buffer;

        if (_start > 0 && _buffer.Length - pending >= minimumFree)
        {
            // Compact: move the pending partial line to the front.
            _buffer.AsSpan(_start, pending).CopyTo(_buffer);
        }
        else
        {
            var larger = ArrayPool<char>.Shared.Rent(Math.Max(_buffer.Length * 2, pending + minimumFree));
            _buffer.AsSpan(_start, pending).CopyTo(larger);
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = larger;
        }

        _start = 0;
        _end = pending;
        return _buffer;
    }
}
