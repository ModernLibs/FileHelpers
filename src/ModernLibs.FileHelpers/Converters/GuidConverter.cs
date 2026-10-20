namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="Guid"/> fields with the FileHelpers 3.x <c>GuidConverter</c> semantics.
/// Parsing accepts every format <c>new Guid(string)</c> accepts (N, D, B, P and X, surrounding
/// blanks ignored) and empty input parses as <see cref="Guid.Empty"/>. Formatting uses the
/// configured format: <c>"D"</c> by default, or <c>"N"</c>, <c>"B"</c>, <c>"P"</c>.
/// Parse and format are allocation-free.
/// </summary>
public sealed class GuidConverter : ISpanConverter<Guid>
{
    /// <summary>Shared singleton instance writing the <c>"D"</c> format.</summary>
    public static readonly GuidConverter Instance = new();

    private readonly string _format = "D";

    private GuidConverter() { }

    /// <summary>
    /// Initializes a converter writing the given 3.x format (<c>[FieldConverter(ConverterKind.Guid, format)]</c>).
    /// </summary>
    /// <param name="format">
    /// <c>"N"</c>, <c>"D"</c>, <c>"B"</c> or <c>"P"</c> (case-insensitive, surrounding blanks ignored);
    /// <see langword="null"/> or empty means <c>"D"</c>.
    /// </param>
    /// <exception cref="BadUsageException">The format is not N, D, B or P.</exception>
    public GuidConverter(string? format)
    {
        if (string.IsNullOrEmpty(format))
            return;

        string normalized = format.Trim().ToUpperInvariant();
        if (normalized is not ("N" or "D" or "B" or "P"))
            throw new BadUsageException("The format of the Guid Converter must be N, D, B or P.");

        _format = normalized;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out Guid value)
    {
        if (span.IsEmpty)
        {
            value = Guid.Empty;
            return true;
        }

        return Guid.TryParse(span, out value);
    }

    /// <inheritdoc/>
    public bool TryFormat(Guid value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, _format);
}
