using System.Globalization;

namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="DateTimeOffset"/> fields. Not part of FileHelpers 3.x (which required a custom
/// converter); used by the source generator for implicit <see cref="DateTimeOffset"/> fields.
/// Without a format the input is parsed with
/// <see cref="DateTimeOffset.TryParse(ReadOnlySpan{char}, IFormatProvider?, DateTimeStyles, out DateTimeOffset)"/>
/// and written with the round-trip format <c>"O"</c>. The input is trimmed; the culture is
/// <see cref="CultureInfo.InvariantCulture"/> unless one is configured.
/// </summary>
public sealed class DateTimeOffsetConverter : ISpanConverter<DateTimeOffset>
{
    private const string DefaultFormat = "O";

    /// <summary>Singleton instance using the round-trip format and InvariantCulture.</summary>
    public static readonly DateTimeOffsetConverter Instance = new();

    private readonly string? _format;
    private readonly IFormatProvider _culture;

    /// <summary>Initializes a new instance with an optional exact format and culture.</summary>
    /// <param name="format">Exact format for parsing and writing; <see langword="null"/> parses any standard form and writes <c>"O"</c>.</param>
    /// <param name="culture">Culture used to parse and format; <see langword="null"/> means invariant.</param>
    public DateTimeOffsetConverter(string? format = null, CultureInfo? culture = null)
    {
        _format = string.IsNullOrEmpty(format) ? null : format;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out DateTimeOffset value) =>
        _format is null
            ? DateTimeOffset.TryParse(span.Trim(), _culture, DateTimeStyles.None, out value)
            : DateTimeOffset.TryParseExact(span.Trim(), _format, _culture, DateTimeStyles.None, out value);

    /// <inheritdoc/>
    public bool TryFormat(DateTimeOffset value, Span<char> destination, out int charsWritten) =>
        value.TryFormat(destination, out charsWritten, _format ?? DefaultFormat, _culture);
}
