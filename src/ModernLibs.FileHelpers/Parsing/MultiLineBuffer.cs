using System.Buffers;

namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// Buffers multiple physical lines for multi-line logical record parsing.
/// Uses ArrayPool&lt;char&gt; for zero-allocation line concatenation.
/// Phase 14 MLIN-02: Supports gathering N physical lines into one logical record.
/// </summary>
internal ref struct MultiLineBuffer
{
    private char[]? _rented;
    private int _written;
    private int _lineCount;
    private int _lastLineNumber;
    private bool _disposed;

    /// <summary>Number of physical lines buffered.</summary>
    public int LineCount => _lineCount;

    /// <summary>Line number of the last physical line added.</summary>
    public int LastLineNumber => _lastLineNumber;

    /// <summary>
    /// Adds a line to the buffer.
    /// Phase 14 MLIN-02: Concatenates lines with newline separators for multi-line records.
    /// </summary>
    /// <param name="line">The line content to add.</param>
    /// <param name="lineNumber">The 1-based physical line number.</param>
    /// <param name="newline">The newline string to use as separator (e.g., "\n" or "\r\n").</param>
    public void AddLine(ReadOnlySpan<char> line, int lineNumber, string newline)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MultiLineBuffer));

        int needed = line.Length;
        if (_lineCount > 0)
            needed += newline.Length;

        EnsureCapacity(needed);

        if (_lineCount > 0)
        {
            // Add newline separator between lines
            newline.AsSpan().CopyTo(_rented.AsSpan(_written));
            _written += newline.Length;
        }

        line.CopyTo(_rented.AsSpan(_written));
        _written += line.Length;
        _lineCount++;
        _lastLineNumber = lineNumber;
    }

    /// <summary>
    /// Returns buffered content as a span.
    /// Phase 14 MLIN-02: The returned span contains all lines concatenated with newlines.
    /// </summary>
    public ReadOnlySpan<char> GetLines() =>
        _rented is null ? ReadOnlySpan<char>.Empty : _rented.AsSpan(0, _written);

    /// <summary>
    /// Returns the rented buffer to the pool.
    /// MLIN-03: Ensure Dispose is called to prevent ArrayPool leakage.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_rented is not null)
        {
            ArrayPool<char>.Shared.Return(_rented);
            _rented = null;
        }
    }

    private void EnsureCapacity(int needed)
    {
        if (_rented is null)
        {
            _rented = ArrayPool<char>.Shared.Rent(Math.Max(256, needed));
            return;
        }

        if (_written + needed > _rented.Length)
        {
            int newSize = Math.Max(_rented.Length * 2, _written + needed);
            var newRent = ArrayPool<char>.Shared.Rent(newSize);
            _rented.AsSpan(0, _written).CopyTo(newRent);
            ArrayPool<char>.Shared.Return(_rented);
            _rented = newRent;
        }
    }
}
