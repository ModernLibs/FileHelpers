using System.Globalization;

namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="TimeSpan"/> fields. Not part of FileHelpers 3.x (which required a custom
/// converter); used by the source generator for implicit <see cref="TimeSpan"/> fields.
/// Without a format the input is parsed with <see cref="TimeSpan.TryParse(ReadOnlySpan{char}, IFormatProvider?, out TimeSpan)"/>
/// and written with the invariant constant format <c>"c"</c> (<c>[-][d.]hh:mm:ss[.fffffff]</c>).
/// The input is trimmed; the culture is <see cref="CultureInfo.InvariantCulture"/> unless one is configured.
/// </summary>
public sealed class TimeSpanConverter : ISpanConverter<TimeSpan>
{
    private const string DefaultFormat = "c";

    /// <summary>Singleton instance using the constant format and InvariantCulture.</summary>
    public static readonly TimeSpanConverter Instance = new();

    private readonly string? _format;
    private readonly IFormatProvider _culture;

    /// <summary>Initializes a new instance with an optional exact format and culture.</summary>
    /// <param name="format">Exact format for parsing and writing; <see langword="null"/> parses any standard form and writes <c>"c"</c>.</param>
    /// <param name="culture">Culture used to parse and format; <see langword="null"/> means invariant.</param>
    public TimeSpanConverter(string? format = null, CultureInfo? culture = null)
    {
        _format = string.IsNullOrEmpty(format) ? null : format;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out TimeSpan value) =>
        _format is null
            ? TimeSpan.TryParse(span.Trim(), _culture, out value)
            : TimeSpan.TryParseExact(span.Trim(), _format, _culture, out value);

    /// <inheritdoc/>
    public bool TryFormat(TimeSpan value, Span<char> destination, out int charsWritten) =>
        value.TryFormat(destination, out charsWritten, _format ?? DefaultFormat, _culture);
}
