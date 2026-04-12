namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="Guid"/> fields using the standard "D" format (8-4-4-4-12 with hyphens).
/// Zero allocations on format; parse allocates nothing.
/// </summary>
public sealed class GuidConverter : ISpanConverter<Guid>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly GuidConverter Instance = new();

    private GuidConverter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out Guid value)
        => Guid.TryParseExact(span, "D", out value);

    /// <inheritdoc/>
    public bool TryFormat(Guid value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, "D");
}
