namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// Line source over a <see cref="TextReader"/> that splits with <see cref="LineSplitter"/> and reports
/// each line's terminator. Lines are slices of a pooled buffer (no per-line string allocation).
/// The reader is not disposed — the caller owns it.
/// </summary>
internal sealed class TextReaderLineSource : IDisposable
{
    private const int MinimumFill = 4096;

    private readonly TextReader _reader;
    private readonly CharLineBuffer _buffer = new(2 * MinimumFill);

    public TextReaderLineSource(TextReader reader)
    {
        _reader = reader;
    }

    /// <summary>Line read by the last successful <see cref="ReadLineAsync"/>.</summary>
    public ReadOnlyMemory<char> CurrentLine { get; private set; }

    /// <summary>Terminator of <see cref="CurrentLine"/>.</summary>
    public LineEnding CurrentEnding { get; private set; }

    /// <summary>Reads the next line synchronously; <see langword="false"/> at end of input.</summary>
    public bool TryReadLine(out ReadOnlySpan<char> line, out LineEnding ending)
    {
        while (true)
        {
            if (_buffer.TryTakeLine(out int start, out int length, out ending))
            {
                line = _buffer.Array.AsSpan(start, length);
                return true;
            }

            if (_buffer.EndOfInput)
            {
                line = default;
                return false;
            }

            int read = _reader.Read(_buffer.GetWritableSpan(MinimumFill));
            if (read == 0)
                _buffer.MarkEndOfInput();
            else
                _buffer.Commit(read);
        }
    }

    /// <summary>
    /// Reads the next line asynchronously into <see cref="CurrentLine"/>/<see cref="CurrentEnding"/>;
    /// <see langword="false"/> at end of input.
    /// </summary>
    public ValueTask<bool> ReadLineAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TryTakeCurrent())
            return new ValueTask<bool>(true);
        return _buffer.EndOfInput ? new ValueTask<bool>(false) : ReadLineSlowAsync(cancellationToken);
    }

    public void Dispose() => _buffer.Dispose();

    private async ValueTask<bool> ReadLineSlowAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            int read = await _reader.ReadAsync(_buffer.GetWritableMemory(MinimumFill), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                _buffer.MarkEndOfInput();
            else
                _buffer.Commit(read);

            if (TryTakeCurrent())
                return true;
            if (_buffer.EndOfInput)
                return false;
        }
    }

    private bool TryTakeCurrent()
    {
        if (!_buffer.TryTakeLine(out int start, out int length, out var ending))
            return false;

        CurrentLine = new ReadOnlyMemory<char>(_buffer.Array, start, length);
        CurrentEnding = ending;
        return true;
    }
}
