using System.Buffers;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Span-based line reader that splits a <see cref="ReadOnlySpan{T}"/> of chars into lines
/// using <see cref="SearchValues{T}"/> for SIMD-accelerated newline detection.
/// Handles LF (\n), CRLF (\r\n), and CR (\r) line endings.
/// This is a pure span-slicing primitive — no I/O, no ArrayPool ownership.
/// </summary>
/// <remarks>
/// D-01: Operates on ReadOnlySpan&lt;char&gt; only. Phase 7 batch engine owns the buffer
/// and hands filled slices to SpanLineReader.
/// Loop invariant: <c>_remaining</c> strictly decreases on every <see cref="TryReadLine"/> call
/// (advances by at least 1 char), preventing infinite loops (T-05-02).
/// </remarks>
public ref struct SpanLineReader
{
    private ReadOnlySpan<char> _remaining;
    private int _lineNumber;

    // Static readonly: SearchValues creation cost paid once per AppDomain (CLAUDE.md §SearchValues).
    private static readonly SearchValues<char> s_newlineChars =
        SearchValues.Create(['\r', '\n']);

    /// <summary>Initializes a new <see cref="SpanLineReader"/> over the given input span.</summary>
    public SpanLineReader(ReadOnlySpan<char> input)
    {
        _remaining = input;
        _lineNumber = 0;
    }

    /// <summary>The 1-based line number of the most recently returned line (0 before first call).</summary>
    public int LineNumber => _lineNumber;

    /// <summary>
    /// Attempts to read the next line from the input.
    /// Returns <see langword="false"/> when the input is exhausted.
    /// The returned <paramref name="line"/> span does not include the line terminator.
    /// </summary>
    public bool TryReadLine(out ReadOnlySpan<char> line)
    {
        if (_remaining.IsEmpty)
        {
            line = default;
            return false;
        }

        int idx = _remaining.IndexOfAny(s_newlineChars);
        if (idx < 0)
        {
            // Last line with no trailing newline — return it and mark exhausted.
            line = _remaining;
            _remaining = default;
            _lineNumber++;
            return true;
        }

        line = _remaining[..idx];
        char terminator = _remaining[idx]; // save before advancing
        _remaining = _remaining[(idx + 1)..];
        // CRLF (\r\n) counts as one line terminator. After consuming '\r' at idx,
        // if next char is '\n', consume it too so CRLF does not produce an empty second line.
        if (terminator == '\r' && !_remaining.IsEmpty && _remaining[0] == '\n')
            _remaining = _remaining[1..];
        _lineNumber++;
        return true;
    }
}
