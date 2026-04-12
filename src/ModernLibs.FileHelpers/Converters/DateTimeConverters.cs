// MA0048: File name intentionally does not match type names — DateTime, DateOnly, TimeOnly, and DateMultiFormat converters grouped in one file.
#pragma warning disable MA0048
using System.Globalization;

namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="DateTime"/> values using a configurable format string.
/// Default format is <c>"MM/dd/yyyy HH:mm:ss"</c> (FileHelpers 3.x compatible).
/// </summary>
public sealed class DateTimeConverter : ISpanConverter<DateTime>
{
    private const string DefaultFormat = "MM/dd/yyyy HH:mm:ss";

    /// <summary>Singleton instance using the default format and InvariantCulture.</summary>
    public static readonly DateTimeConverter Instance = new();

    private readonly string _format;
    private readonly IFormatProvider _culture;

    /// <summary>Initializes a new instance with an optional custom format and culture.</summary>
    public DateTimeConverter(string? format = null, CultureInfo? culture = null)
    {
        _format = format ?? DefaultFormat;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out DateTime value)
        => DateTime.TryParseExact(span, _format, _culture, DateTimeStyles.None, out value);

    /// <inheritdoc/>
    public bool TryFormat(DateTime value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, _format, _culture);
}

/// <summary>
/// Converts <see cref="DateOnly"/> values using a configurable format string.
/// Default format is <c>"MM/dd/yyyy"</c>.
/// </summary>
public sealed class DateOnlyConverter : ISpanConverter<DateOnly>
{
    private const string DefaultFormat = "MM/dd/yyyy";

    /// <summary>Singleton instance using the default format and InvariantCulture.</summary>
    public static readonly DateOnlyConverter Instance = new();

    private readonly string _format;
    private readonly IFormatProvider _culture;

    /// <summary>Initializes a new instance with an optional custom format and culture.</summary>
    public DateOnlyConverter(string? format = null, CultureInfo? culture = null)
    {
        _format = format ?? DefaultFormat;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out DateOnly value)
        => DateOnly.TryParseExact(span, _format, _culture, DateTimeStyles.None, out value);

    /// <inheritdoc/>
    public bool TryFormat(DateOnly value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, _format, _culture);
}

/// <summary>
/// Converts <see cref="TimeOnly"/> values using a configurable format string.
/// Default format is <c>"HH:mm:ss"</c>.
/// </summary>
public sealed class TimeOnlyConverter : ISpanConverter<TimeOnly>
{
    private const string DefaultFormat = "HH:mm:ss";

    /// <summary>Singleton instance using the default format and InvariantCulture.</summary>
    public static readonly TimeOnlyConverter Instance = new();

    private readonly string _format;
    private readonly IFormatProvider _culture;

    /// <summary>Initializes a new instance with an optional custom format and culture.</summary>
    public TimeOnlyConverter(string? format = null, CultureInfo? culture = null)
    {
        _format = format ?? DefaultFormat;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out TimeOnly value)
        => TimeOnly.TryParseExact(span, _format, _culture, DateTimeStyles.None, out value);

    /// <inheritdoc/>
    public bool TryFormat(TimeOnly value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, _format, _culture);
}

/// <summary>
/// Converts <see cref="DateTime"/> values by trying multiple format strings in order.
/// On write, uses the first format string in the array.
/// </summary>
public sealed class DateMultiFormatConverter : ISpanConverter<DateTime>
{
    private readonly string[] _formats;
    private readonly IFormatProvider _culture;

    /// <summary>
    /// Initializes a new instance with the given format strings and optional culture.
    /// </summary>
    /// <param name="formats">Format strings tried in order during parsing. The first format is used during formatting.</param>
    /// <param name="culture">Optional culture; defaults to <see cref="CultureInfo.InvariantCulture"/>.</param>
    public DateMultiFormatConverter(string[] formats, CultureInfo? culture = null)
    {
        _formats = formats;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out DateTime value)
    {
        foreach (string fmt in _formats)
        {
            if (DateTime.TryParseExact(span, fmt, _culture, DateTimeStyles.None, out value))
                return true;
        }
        value = default;
        return false;
    }

    /// <inheritdoc/>
    public bool TryFormat(DateTime value, Span<char> destination, out int charsWritten)
    {
        if (_formats.Length == 0)
        {
            charsWritten = 0;
            return true;
        }
        return value.TryFormat(destination, out charsWritten, _formats[0], _culture);
    }
}
