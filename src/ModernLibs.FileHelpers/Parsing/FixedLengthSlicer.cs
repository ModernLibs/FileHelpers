namespace ModernLibs.FileHelpers;

/// <summary>
/// Span-based positional field extractor for fixed-length record lines.
/// Extracts fields by character offset and width from a <see cref="ReadOnlySpan{T}"/> line.
/// A ref struct — stack-allocated, no heap allocation.
/// </summary>
public ref struct FixedLengthSlicer
{
    private readonly ReadOnlySpan<char> _line;

    /// <summary>
    /// Initializes a new <see cref="FixedLengthSlicer"/> over the given line span.
    /// </summary>
    /// <param name="line">The fixed-length record line to extract fields from.</param>
    public FixedLengthSlicer(ReadOnlySpan<char> line)
    {
        _line = line;
    }

    /// <summary>
    /// Extracts a field by character offset and width.
    /// Clamps the slice to the actual line length if the line is shorter than expected
    /// (handles AllowLessChars / AllowVariableLength mode gracefully).
    /// </summary>
    /// <param name="offset">Zero-based character start position.</param>
    /// <param name="width">Character width of the field.</param>
    /// <returns>
    /// The field span, clamped to available characters.
    /// Returns <see cref="ReadOnlySpan{T}.Empty"/> if <paramref name="offset"/> is negative,
    /// beyond the end of the line, or if <paramref name="width"/> is non-positive.
    /// </returns>
    /// <remarks>
    /// T-05-09: Negative offset returns Empty (guard against DoS from unexpected runtime callers).
    /// Source-generated code always passes compile-time non-negative constants from [FieldFixedLength].
    /// T-05-11: FieldDescriptor.Offset sentinel -1 maps to Empty via the negative-offset guard.
    /// </remarks>
    public ReadOnlySpan<char> Slice(int offset, int width)
    {
        if (offset < 0 || width <= 0 || offset >= _line.Length) return ReadOnlySpan<char>.Empty;
        int available = _line.Length - offset;
        int actualWidth = width > available ? available : width;
        return _line.Slice(offset, actualWidth);
    }
}
