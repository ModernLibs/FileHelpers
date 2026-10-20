// MA0048: File name intentionally does not match type names — DateTime, DateOnly, TimeOnly, and DateMultiFormat converters grouped in one file.
#pragma warning disable MA0048
using System.Globalization;

namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="DateTime"/> values with the FileHelpers 3.x <c>DateTimeConverter</c> semantics:
/// the input is trimmed and parsed exactly with the configured format.
/// </summary>
/// <remarks>
/// Without an explicit format the converter uses <see cref="ConverterBase.DefaultDateTimeFormat"/>
/// (3.x default <c>"ddMMyyyy"</c>), read on every call. The culture is
/// <see cref="CultureInfo.InvariantCulture"/> unless one is configured — the current culture is never used.
/// </remarks>
public sealed class DateTimeConverter : ISpanConverter<DateTime>
{
    /// <summary>Singleton instance using <see cref="ConverterBase.DefaultDateTimeFormat"/> and InvariantCulture.</summary>
    public static readonly DateTimeConverter Instance = new();

    private readonly string? _format;
    private readonly IFormatProvider _culture;

    /// <summary>Initializes a new instance with an optional custom format and culture.</summary>
    /// <param name="format">
    /// Exact format string (3.x <c>[FieldConverter(ConverterKind.Date, format)]</c>);
    /// <see langword="null"/> means <see cref="ConverterBase.DefaultDateTimeFormat"/>.
    /// </param>
    /// <param name="culture">Culture used to parse and format; <see langword="null"/> means invariant.</param>
    /// <exception cref="BadUsageException"><paramref name="format"/> is empty or not a valid date format.</exception>
    public DateTimeConverter(string? format = null, CultureInfo? culture = null)
    {
        if (format is not null)
            DateFormat.Validate(format);
        _format = format;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    private string Format => _format ?? ConverterBase.DefaultDateTimeFormat;

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out DateTime value)
        => DateTime.TryParseExact(span.Trim(), Format, _culture, DateTimeStyles.None, out value);

    /// <inheritdoc/>
    public bool TryFormat(DateTime value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, Format, _culture);
}

/// <summary>
/// Converts <see cref="DateOnly"/> values using a configurable format string.
/// Default format is <c>"MM/dd/yyyy"</c>. The input is trimmed before parsing; the culture is
/// <see cref="CultureInfo.InvariantCulture"/> unless one is configured.
/// </summary>
public sealed class DateOnlyConverter : ISpanConverter<DateOnly>
{
    private const string DefaultFormat = "MM/dd/yyyy";

    /// <summary>Singleton instance using the default format and InvariantCulture.</summary>
    public static readonly DateOnlyConverter Instance = new();

    private readonly string _format;
    private readonly IFormatProvider _culture;

    /// <summary>Initializes a new instance with an optional custom format and culture.</summary>
    /// <exception cref="BadUsageException"><paramref name="format"/> is empty or not a valid date format.</exception>
    public DateOnlyConverter(string? format = null, CultureInfo? culture = null)
    {
        if (format is not null)
            DateFormat.Validate(format);
        _format = format ?? DefaultFormat;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out DateOnly value)
        => DateOnly.TryParseExact(span.Trim(), _format, _culture, DateTimeStyles.None, out value);

    /// <inheritdoc/>
    public bool TryFormat(DateOnly value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, _format, _culture);
}

/// <summary>
/// Converts <see cref="TimeOnly"/> values using a configurable format string.
/// Default format is <c>"HH:mm:ss"</c>. The input is trimmed before parsing; the culture is
/// <see cref="CultureInfo.InvariantCulture"/> unless one is configured.
/// </summary>
public sealed class TimeOnlyConverter : ISpanConverter<TimeOnly>
{
    private const string DefaultFormat = "HH:mm:ss";

    /// <summary>Singleton instance using the default format and InvariantCulture.</summary>
    public static readonly TimeOnlyConverter Instance = new();

    private readonly string _format;
    private readonly IFormatProvider _culture;

    /// <summary>Initializes a new instance with an optional custom format and culture.</summary>
    /// <exception cref="BadUsageException"><paramref name="format"/> is empty or not a valid time format.</exception>
    public TimeOnlyConverter(string? format = null, CultureInfo? culture = null)
    {
        if (format is not null)
            DateFormat.Validate(format);
        _format = format ?? DefaultFormat;
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out TimeOnly value)
        => TimeOnly.TryParseExact(span.Trim(), _format, _culture, DateTimeStyles.None, out value);

    /// <inheritdoc/>
    public bool TryFormat(TimeOnly value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, _format, _culture);
}

/// <summary>
/// Converts <see cref="DateTime"/> values by trying multiple format strings in order
/// (FileHelpers 3.x <c>DateTimeMultiFormatConverter</c>). The input is trimmed before parsing.
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
    /// <exception cref="BadUsageException">No format is given, or a format is empty or invalid.</exception>
    public DateMultiFormatConverter(string[] formats, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(formats);
        if (formats.Length == 0)
            throw new BadUsageException("The DateMultiFormat Converter needs at least one format.");
        foreach (string format in formats)
            DateFormat.Validate(format);

        _formats = (string[])formats.Clone();
        _culture = culture ?? CultureInfo.InvariantCulture;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out DateTime value)
    {
        ReadOnlySpan<char> trimmed = span.Trim();
        foreach (string fmt in _formats)
        {
            if (DateTime.TryParseExact(trimmed, fmt, _culture, DateTimeStyles.None, out value))
                return true;
        }
        value = default;
        return false;
    }

    /// <inheritdoc/>
    public bool TryFormat(DateTime value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, _formats[0], _culture);
}

/// <summary>3.x date format validation shared by the date converters and <see cref="ConverterBase.DefaultDateTimeFormat"/>.</summary>
internal static class DateFormat
{
    /// <summary>The FileHelpers 3.x default date format.</summary>
    internal const string LegacyDefault = "ddMMyyyy";

    /// <summary>Throws <see cref="BadUsageException"/> when <paramref name="format"/> is null, empty or invalid (3.x messages).</summary>
    internal static void Validate(string? format)
    {
        if (string.IsNullOrEmpty(format))
            throw new BadUsageException("The format of the DateTime Converter cannot be null or empty.");

        try
        {
            _ = DateTime.MinValue.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException ex)
        {
            throw new BadUsageException("The format: '" + format + "' is invalid for the DateTime Converter.", ex);
        }
    }
}
