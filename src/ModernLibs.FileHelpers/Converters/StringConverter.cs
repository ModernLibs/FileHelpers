namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="string"/> fields.
/// </summary>
/// <remarks>
/// <see cref="TryParse"/> allocates one <see cref="string"/> per call, except for an empty value and a single character below 128, which are shared.
/// <see cref="TryFormat"/> is zero-allocation: it copies characters directly into the destination span.
/// </remarks>
public sealed class StringConverter : ISpanConverter<string>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly StringConverter Instance = new();

    private StringConverter() { }

    /// <inheritdoc/>
    /// <remarks>Allocates one string per call, except for an empty value and a single character below 128, which are shared.</remarks>
    public bool TryParse(ReadOnlySpan<char> span, out string value)
    {
        value = RecordParsing.CreateString(span);
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
