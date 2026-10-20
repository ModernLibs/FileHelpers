// MA0048: File name intentionally does not match type names — all float converters grouped in one file.
#pragma warning disable MA0048
using System.Globalization;

namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="decimal"/> fields with the FileHelpers 3.x <c>DecimalConverter</c> rules: blanks before
/// and after a leading sign are removed, then the text is parsed with
/// <c>NumberStyles.Number | NumberStyles.AllowExponent</c> (no currency symbols, no parentheses).
/// Invariant culture unless a decimal separator or culture is configured; formatting uses the same
/// separator so written values read back. Zero allocations.
/// </summary>
public sealed class DecimalConverter : ISpanConverter<decimal>
{
    /// <summary>Shared singleton instance using <see cref="CultureInfo.InvariantCulture"/>.</summary>
    public static readonly DecimalConverter Instance = new();

    private readonly IFormatProvider _culture;

    /// <summary>Initialises a <see cref="DecimalConverter"/> that uses <see cref="CultureInfo.InvariantCulture"/>.</summary>
    public DecimalConverter()
        : this((IFormatProvider?)null)
    {
    }

    /// <summary>Initialises a <see cref="DecimalConverter"/> with the specified culture (<see langword="null"/> means <see cref="CultureInfo.InvariantCulture"/>).</summary>
    /// <param name="culture">The culture or number format to parse and format with.</param>
    public DecimalConverter(IFormatProvider? culture)
        => _culture = culture ?? CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a converter from the 3.x <c>decimalSepOrCultureName</c> argument
    /// (<c>[FieldConverter(ConverterKind.Decimal, ",")]</c>): <c>"."</c> or <c>","</c> selects the decimal
    /// separator (the other one becomes the group separator); any other value is a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    public DecimalConverter(string decimalSeparatorOrCultureName)
        => _culture = NumberConversion.ResolveSeparator(decimalSeparatorOrCultureName);

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out decimal value)
        => NumberConversion.TryParse(span, NumberConversion.Styles, _culture, out value);

    /// <inheritdoc/>
    public bool TryFormat(decimal value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _culture);
}

/// <summary>
/// Converts <see cref="double"/> fields with the FileHelpers 3.x <c>DoubleConverter</c> rules: blanks before
/// and after a leading sign are removed, then the text is parsed with
/// <c>NumberStyles.Number | NumberStyles.AllowExponent</c> (no currency symbols, no parentheses).
/// Invariant culture unless a decimal separator or culture is configured; formatting uses the same
/// separator so written values read back. Zero allocations.
/// </summary>
public sealed class DoubleConverter : ISpanConverter<double>
{
    /// <summary>Shared singleton instance using <see cref="CultureInfo.InvariantCulture"/>.</summary>
    public static readonly DoubleConverter Instance = new();

    private readonly IFormatProvider _culture;

    /// <summary>Initialises a <see cref="DoubleConverter"/> that uses <see cref="CultureInfo.InvariantCulture"/>.</summary>
    public DoubleConverter()
        : this((IFormatProvider?)null)
    {
    }

    /// <summary>Initialises a <see cref="DoubleConverter"/> with the specified culture (<see langword="null"/> means <see cref="CultureInfo.InvariantCulture"/>).</summary>
    /// <param name="culture">The culture or number format to parse and format with.</param>
    public DoubleConverter(IFormatProvider? culture)
        => _culture = culture ?? CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a converter from the 3.x <c>decimalSepOrCultureName</c> argument
    /// (<c>[FieldConverter(ConverterKind.Double, ",")]</c>): <c>"."</c> or <c>","</c> selects the decimal
    /// separator (the other one becomes the group separator); any other value is a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    public DoubleConverter(string decimalSeparatorOrCultureName)
        => _culture = NumberConversion.ResolveSeparator(decimalSeparatorOrCultureName);

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out double value)
        => NumberConversion.TryParse(span, NumberConversion.Styles, _culture, out value);

    /// <inheritdoc/>
    public bool TryFormat(double value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _culture);
}

/// <summary>
/// Converts <see cref="float"/> fields with the FileHelpers 3.x <c>SingleConverter</c> rules: blanks before
/// and after a leading sign are removed, then the text is parsed with
/// <c>NumberStyles.Number | NumberStyles.AllowExponent</c> (no currency symbols, no parentheses).
/// Invariant culture unless a decimal separator or culture is configured; formatting uses the same
/// separator so written values read back. Zero allocations.
/// </summary>
public sealed class SingleConverter : ISpanConverter<float>
{
    /// <summary>Shared singleton instance using <see cref="CultureInfo.InvariantCulture"/>.</summary>
    public static readonly SingleConverter Instance = new();

    private readonly IFormatProvider _culture;

    /// <summary>Initialises a <see cref="SingleConverter"/> that uses <see cref="CultureInfo.InvariantCulture"/>.</summary>
    public SingleConverter()
        : this((IFormatProvider?)null)
    {
    }

    /// <summary>Initialises a <see cref="SingleConverter"/> with the specified culture (<see langword="null"/> means <see cref="CultureInfo.InvariantCulture"/>).</summary>
    /// <param name="culture">The culture or number format to parse and format with.</param>
    public SingleConverter(IFormatProvider? culture)
        => _culture = culture ?? CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a converter from the 3.x <c>decimalSepOrCultureName</c> argument
    /// (<c>[FieldConverter(ConverterKind.Single, ",")]</c>): <c>"."</c> or <c>","</c> selects the decimal
    /// separator (the other one becomes the group separator); any other value is a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    public SingleConverter(string decimalSeparatorOrCultureName)
        => _culture = NumberConversion.ResolveSeparator(decimalSeparatorOrCultureName);

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out float value)
        => NumberConversion.TryParse(span, NumberConversion.Styles, _culture, out value);

    /// <inheritdoc/>
    public bool TryFormat(float value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _culture);
}

/// <summary>
/// Converts <see cref="double"/> fields with the behaviour of the FileHelpers 3.5.2
/// <c>PercentDoubleConverter</c>, which parses and formats exactly like <see cref="DoubleConverter"/>:
/// <c>"50"</c> reads as <c>50</c>, and <c>0.5</c> is written as <c>"0.5"</c>.
/// </summary>
/// <remarks>
/// FileHelpers 3.x documented that <c>"50%"</c> reads as <c>0.5</c>, but its parser rejects any value that
/// contains <c>%</c>. This converter keeps the 3.5.2 behaviour so files read and written by 3.x are handled
/// the same way: a value with <c>%</c> fails to convert and the engine raises <see cref="ConvertException"/>.
/// </remarks>
public sealed class PercentDoubleConverter : ISpanConverter<double>
{
    /// <summary>Shared singleton instance (invariant culture).</summary>
    public static readonly PercentDoubleConverter Instance = new();

    private readonly IFormatProvider _culture = CultureInfo.InvariantCulture;

    private PercentDoubleConverter() { }

    /// <summary>Initializes a converter using <paramref name="culture"/> (<see langword="null"/> means invariant).</summary>
    public PercentDoubleConverter(IFormatProvider? culture) => _culture = culture ?? CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a converter from the 3.x <c>decimalSepOrCultureName</c> argument
    /// (<c>[FieldConverter(ConverterKind.PercentDouble, ",")]</c>): <c>"."</c> or <c>","</c> selects the
    /// decimal separator (the other one becomes the group separator); any other value is a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    public PercentDoubleConverter(string decimalSeparatorOrCultureName)
        => _culture = NumberConversion.ResolveSeparator(decimalSeparatorOrCultureName);

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out double value)
        => NumberConversion.TryParse(span, NumberConversion.Styles, _culture, out value);

    /// <inheritdoc/>
    public bool TryFormat(double value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _culture);
}
