// MA0048: SpanLines is the allocation-free iterator form of LineSplitter; kept together.
#pragma warning disable MA0048

using System.Buffers;

namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// The single line-splitting rule shared by every read path (batch spans, <see cref="TextReader"/>
/// sources and <see cref="PipelineLineReader"/>): <c>\n</c>, <c>\r\n</c> and bare <c>\r</c> each end a
/// line, the first terminator wins, and a trailing segment after the last terminator is a line only
/// when it is non-empty. This matches <see cref="SpanLineReader"/> and <see cref="TextReader.ReadLine"/>.
/// </summary>
internal static class LineSplitter
{
    private static readonly SearchValues<char> s_newlineChars = SearchValues.Create(['\r', '\n']);

    /// <summary>
    /// Finds the next line at the start of <paramref name="span"/>.
    /// </summary>
    /// <param name="span">Unconsumed characters.</param>
    /// <param name="isFinal"><see langword="true"/> when no more characters will follow <paramref name="span"/>.</param>
    /// <param name="lineLength">Length of the line content (terminator excluded).</param>
    /// <param name="consumed">Characters to consume (line plus terminator).</param>
    /// <param name="ending">The terminator found.</param>
    /// <returns>
    /// <see langword="false"/> when more input is needed to decide (no terminator yet, or a <c>\r</c> as the
    /// last available character) or when the input is exhausted.
    /// </returns>
    public static bool TryFindLine(
        ReadOnlySpan<char> span,
        bool isFinal,
        out int lineLength,
        out int consumed,
        out LineEnding ending)
    {
        int idx = span.IndexOfAny(s_newlineChars);
        if (idx < 0)
        {
            if (!isFinal || span.IsEmpty)
            {
                lineLength = 0;
                consumed = 0;
                ending = LineEnding.None;
                return false;
            }

            lineLength = span.Length;
            consumed = span.Length;
            ending = LineEnding.None;
            return true;
        }

        lineLength = idx;
        if (span[idx] == '\n')
        {
            consumed = idx + 1;
            ending = LineEnding.Lf;
            return true;
        }

        if (idx + 1 < span.Length)
        {
            if (span[idx + 1] == '\n')
            {
                consumed = idx + 2;
                ending = LineEnding.CrLf;
            }
            else
            {
                consumed = idx + 1;
                ending = LineEnding.Cr;
            }

            return true;
        }

        if (!isFinal)
        {
            // A '\r' at the end of the available data may be the first half of "\r\n".
            lineLength = 0;
            consumed = 0;
            ending = LineEnding.None;
            return false;
        }

        consumed = idx + 1;
        ending = LineEnding.Cr;
        return true;
    }

    /// <summary>
    /// Counts the line breaks (<c>\r\n</c>, <c>\r</c> or <c>\n</c>) in <paramref name="text"/>;
    /// <paramref name="lastLineStart"/> is the index after the last one (0 when there is none).
    /// </summary>
    public static int CountLineBreaks(ReadOnlySpan<char> text, out int lastLineStart)
    {
        int count = 0;
        int i = 0;
        lastLineStart = 0;
        while (i < text.Length)
        {
            int idx = text[i..].IndexOfAny(s_newlineChars);
            if (idx < 0)
                break;

            i += idx + 1;
            if (text[i - 1] == '\r' && i < text.Length && text[i] == '\n')
                i++;
            count++;
            lastLineStart = i;
        }

        return count;
    }

    /// <summary>Counts the lines <see cref="TryFindLine"/> yields for a complete input.</summary>
    public static int CountLines(ReadOnlySpan<char> content)
    {
        int count = 0;
        while (!content.IsEmpty)
        {
            int idx = content.IndexOfAny(s_newlineChars);
            count++;
            if (idx < 0)
                break;

            int next = idx + 1;
            if (content[idx] == '\r' && next < content.Length && content[next] == '\n')
                next++;
            content = content[next..];
        }

        return count;
    }

    /// <summary>Text of a terminator, used to re-join physical lines of a multiline record.</summary>
    public static string ToText(LineEnding ending) => ending switch
    {
        LineEnding.Lf => "\n",
        LineEnding.Cr => "\r",
        LineEnding.CrLf => "\r\n",
        _ => string.Empty,
    };
}

/// <summary>Allocation-free line iterator over a complete in-memory input.</summary>
internal ref struct SpanLines
{
    private ReadOnlySpan<char> _remaining;

    public SpanLines(ReadOnlySpan<char> content)
    {
        _remaining = content;
    }

    /// <summary>Returns the next line and its terminator; <see langword="false"/> at end of input.</summary>
    public bool TryRead(out ReadOnlySpan<char> line, out LineEnding ending)
    {
        if (!LineSplitter.TryFindLine(_remaining, isFinal: true, out int length, out int consumed, out ending))
        {
            line = default;
            return false;
        }

        line = _remaining[..length];
        _remaining = _remaining[consumed..];
        return true;
    }
}
