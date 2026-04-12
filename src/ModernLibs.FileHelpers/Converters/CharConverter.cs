namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts single-character fields. Returns <see langword="false"/> if the span is not exactly one character long.
/// Zero allocations on both parse and format paths.
/// </summary>
public sealed class CharConverter : ISpanConverter<char>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly CharConverter Instance = new();

    private CharConverter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out char value)
    {
        if (span.Length == 1)
        {
            value = span[0];
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    public bool TryFormat(char value, Span<char> destination, out int charsWritten)
    {
        if (destination.Length < 1)
        {
            charsWritten = 0;
            return false;
        }

        destination[0] = value;
        charsWritten = 1;
        return true;
    }
}
