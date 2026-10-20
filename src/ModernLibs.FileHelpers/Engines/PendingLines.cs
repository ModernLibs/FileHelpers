using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>
/// The physical lines after the failing line of a [FieldInNewLine] record, which FileHelpers 3.x reads again
/// as the start of the next record. The readers report the error first and then read these lines; when the
/// error is thrown (<see cref="ErrorMode.ThrowException"/>) the lines stay here until the next pull.
/// </summary>
/// <remarks>
/// Lines are taken from the front. A record that fails while these lines are read holds its own remaining
/// lines, which are the lines just read, so they go before the lines still waiting. Errors are rare, so the
/// text is copied into a new array (never pooled: a line handed out by <see cref="TryRead"/> stays valid).
/// </remarks>
internal sealed class PendingLines
{
    private char[]? _text;
    private int _position;
    private int _lineNumber;

    /// <summary>Lines are waiting.</summary>
    public bool HasLines => _text is not null;

    /// <summary>Adds the lines of <paramref name="rest"/> before the lines that are still waiting.</summary>
    /// <param name="rest">Record text after the failing line; its lines are separated by line breaks.</param>
    /// <param name="firstLine">Physical line number of the first line of <paramref name="rest"/>.</param>
    public void Hold(ReadOnlySpan<char> rest, int firstLine)
    {
        var waiting = _text is null ? default : _text.AsSpan(_position);
        var text = new char[rest.Length + (_text is null ? 0 : 1 + waiting.Length)];
        rest.CopyTo(text);
        if (_text is not null)
        {
            text[rest.Length] = '\n';
            waiting.CopyTo(text.AsSpan(rest.Length + 1));
        }

        _text = text;
        _position = 0;
        _lineNumber = firstLine;
    }

    /// <summary>
    /// Takes the next line. The last line of the text is a line even when it is empty (the record's last
    /// physical line).
    /// </summary>
    public bool TryRead(out ReadOnlySpan<char> line, out int lineNumber, out LineEnding ending)
    {
        if (_text is not { } text)
        {
            line = default;
            lineNumber = 0;
            ending = LineEnding.None;
            return false;
        }

        var remaining = text.AsSpan(_position);
        lineNumber = _lineNumber++;
        int idx = remaining.IndexOfAny('\r', '\n');
        if (idx < 0)
        {
            line = remaining;
            ending = LineEnding.Lf;
            _text = null;
            return true;
        }

        int next = idx + 1;
        ending = LineEnding.Lf;
        if (remaining[idx] == '\r')
        {
            ending = LineEnding.Cr;
            if (next < remaining.Length && remaining[next] == '\n')
            {
                next++;
                ending = LineEnding.CrLf;
            }
        }

        line = remaining[..idx];
        _position += next;
        return true;
    }
}
