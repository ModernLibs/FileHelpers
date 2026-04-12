namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Zero-allocation span-based converter interface. All implementations are sealed with a static Instance.
/// </summary>
/// <typeparam name="T">The type to convert to/from a span of characters.</typeparam>
public interface ISpanConverter<T>
{
    /// <summary>Parses a value from the given character span. Returns false if parsing fails; sets <paramref name="value"/> to default on failure.</summary>
    bool TryParse(ReadOnlySpan<char> span, out T value);

    /// <summary>Formats <paramref name="value"/> into <paramref name="destination"/>. Returns false if the destination is too small.</summary>
    bool TryFormat(T value, Span<char> destination, out int charsWritten);
}
