namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="string"/> fields.
/// </summary>
/// <remarks>
/// <see cref="TryParse"/> allocates one <see cref="string"/> per call — unavoidable for string fields.
/// <see cref="TryFormat"/> is zero-allocation: it copies characters directly into the destination span.
/// </remarks>
public sealed class StringConverter : ISpanConverter<string>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly StringConverter Instance = new();

    private StringConverter() { }

    /// <inheritdoc/>
    /// <remarks>Allocates one string per call (unavoidable for string fields).</remarks>
    public bool TryParse(ReadOnlySpan<char> span, out string value)
    {
        value = new string(span); // unavoidable allocation — documented
        return true;
    }

    /// <inheritdoc/>
    public bool TryFormat(string value, Span<char> destination, out int charsWritten)
    {
        ReadOnlySpan<char> src = value.AsSpan();
        if (!src.TryCopyTo(destination))
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = src.Length;
        return true;
    }
}
