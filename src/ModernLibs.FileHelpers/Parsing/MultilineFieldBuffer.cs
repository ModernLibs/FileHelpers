// Phase 18 MULT-02: Multiline field buffer for line accumulation during multiline parsing
// Accumulates partial lines when inside unclosed quoted fields

using System.Text;

namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// Buffers lines during multiline quoted field assembly.
/// Accumulates lines until the quote balance closes.
/// MULT-02: Line assembly for multiline quoted field support.
/// </summary>
internal sealed class MultilineFieldBuffer
{
    private readonly StringBuilder _builder;
    private int _startLineNumber;
    private string _lineEnding;

    /// <summary>
    /// Creates a new MultilineFieldBuffer with the specified initial capacity.
    /// </summary>
    /// <param name="capacity">Initial StringBuilder capacity (default 256).</param>
    public MultilineFieldBuffer(int capacity = 256)
    {
        _builder = new StringBuilder(capacity);
        _startLineNumber = 0;
        _lineEnding = "\n";  // Default, will be set on first append
    }

    /// <summary>
    /// Returns true if the buffer has accumulated content.
    /// </summary>
    public bool HasContent => _builder.Length > 0;

    /// <summary>
    /// Gets the starting line number of the multiline field (for error reporting).
    /// </summary>
    public int StartLineNumber => _startLineNumber;

    /// <summary>
    /// Clears the buffer and resets state.
    /// </summary>
    public void Clear()
    {
        _builder.Clear();
        _startLineNumber = 0;
    }

    /// <summary>
    /// Appends a line to the buffer with the specified line ending.
    /// Call this when an unclosed quote is detected and more lines are needed.
    /// </summary>
    /// <param name="line">The line content to append.</param>
    /// <param name="lineEnding">The line ending to use (default "\n").</param>
    /// <param name="lineNumber">The physical line number (for error tracking).</param>
    public void AppendLine(string line, string lineEnding, int lineNumber)
    {
        if (_builder.Length == 0)
        {
            // First line - record the starting line number
            _startLineNumber = lineNumber;
            _lineEnding = lineEnding;
        }
        else
        {
            // Subsequent lines - add line ending separator
            _builder.Append(_lineEnding);
        }

        _builder.Append(line);
    }

    /// <summary>
    /// Attempts to assemble a complete line by checking if the next line closes the quote.
    /// </summary>
    /// <param name="nextLine">The next line to check.</param>
    /// <param name="lineEnding">The line ending to use for joining.</param>
    /// <param name="assembled">The assembled complete line if successful, null otherwise.</param>
    /// <returns>True if the quote is now closed and assembly is complete.</returns>
    public bool TryAssembleCompleteLine(string nextLine, string lineEnding, out string? assembled)
    {
        assembled = null;

        if (_builder.Length == 0)
        {
            // Nothing to assemble
            return false;
        }

        // Check if adding the next line would close the quote
        int combinedLength = _builder.Length + _lineEnding.Length + nextLine.Length;
        Span<char> combined = combinedLength <= 512
            ? stackalloc char[combinedLength]
            : new char[combinedLength];

        int pos = 0;
        _builder.CopyTo(0, combined, _builder.Length);
        pos += _builder.Length;

        _lineEnding.CopyTo(combined.Slice(pos));
        pos += _lineEnding.Length;

        nextLine.AsSpan().CopyTo(combined.Slice(pos));

        if (QuoteBalanceScanner.IsBalanced(combined))
        {
            // Quote is closed - assemble the complete line
            assembled = combined.ToString();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the current buffered content as a string.
    /// </summary>
    public override string ToString()
    {
        return _builder.ToString();
    }

    /// <summary>
    /// Gets the current accumulated content as a span (for quote balance checking).
    /// Note: This allocates a string internally due to StringBuilder limitations.
    /// </summary>
    public ReadOnlySpan<char> AsSpan()
    {
        return _builder.ToString().AsSpan();
    }
}
