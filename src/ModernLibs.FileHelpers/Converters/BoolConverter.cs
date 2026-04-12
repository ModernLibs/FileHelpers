namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="bool"/> fields using FileHelpers 3.x-compatible string representations.
/// Parses "true"/"1"/"yes" as <see langword="true"/> and "false"/"0"/"no" as <see langword="false"/> (all case-insensitive).
/// Formats as "True" or "False" (capital first letter) matching FileHelpers 3.x round-trip behaviour.
/// </summary>
public sealed class BoolConverter : ISpanConverter<bool>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly BoolConverter Instance = new();

    private BoolConverter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out bool value)
    {
        if (span.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            span.Equals("1", StringComparison.OrdinalIgnoreCase) ||
            span.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (span.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            span.Equals("0", StringComparison.OrdinalIgnoreCase) ||
            span.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    /// <remarks><see cref="bool.TryFormat"/> writes "True" or "False" in .NET 10 — capital first letter, matching FileHelpers 3.x.</remarks>
    public bool TryFormat(bool value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten);
}
