// MA0048: File name intentionally does not match type names — all float converters grouped in one file.
#pragma warning disable MA0048
using System.Globalization;

namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="decimal"/> fields using InvariantCulture by default.
/// Accepts an optional <see cref="IFormatProvider"/> in the constructor for locale overrides.
/// </summary>
public sealed class DecimalConverter : ISpanConverter<decimal>
{
    /// <summary>Shared singleton instance using <see cref="CultureInfo.InvariantCulture"/>.</summary>
    public static readonly DecimalConverter Instance = new();

    private readonly IFormatProvider _culture;

    /// <summary>Initialises a <see cref="DecimalConverter"/> with the specified culture (default: <see cref="CultureInfo.InvariantCulture"/>).</summary>
    public DecimalConverter(IFormatProvider? culture = null)
        => _culture = culture ?? CultureInfo.InvariantCulture;

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out decimal value)
        => decimal.TryParse(span, NumberStyles.Any, _culture, out value);

    /// <inheritdoc/>
    public bool TryFormat(decimal value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _culture);
}

/// <summary>
/// Converts <see cref="double"/> fields using InvariantCulture by default.
/// Accepts an optional <see cref="IFormatProvider"/> in the constructor for locale overrides.
/// </summary>
public sealed class DoubleConverter : ISpanConverter<double>
{
    /// <summary>Shared singleton instance using <see cref="CultureInfo.InvariantCulture"/>.</summary>
    public static readonly DoubleConverter Instance = new();

    private readonly IFormatProvider _culture;

    /// <summary>Initialises a <see cref="DoubleConverter"/> with the specified culture (default: <see cref="CultureInfo.InvariantCulture"/>).</summary>
    public DoubleConverter(IFormatProvider? culture = null)
        => _culture = culture ?? CultureInfo.InvariantCulture;

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out double value)
        => double.TryParse(span, NumberStyles.Any, _culture, out value);

    /// <inheritdoc/>
    public bool TryFormat(double value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _culture);
}

/// <summary>
/// Converts <see cref="float"/> fields using InvariantCulture by default.
/// Accepts an optional <see cref="IFormatProvider"/> in the constructor for locale overrides.
/// </summary>
public sealed class SingleConverter : ISpanConverter<float>
{
    /// <summary>Shared singleton instance using <see cref="CultureInfo.InvariantCulture"/>.</summary>
    public static readonly SingleConverter Instance = new();

    private readonly IFormatProvider _culture;

    /// <summary>Initialises a <see cref="SingleConverter"/> with the specified culture (default: <see cref="CultureInfo.InvariantCulture"/>).</summary>
    public SingleConverter(IFormatProvider? culture = null)
        => _culture = culture ?? CultureInfo.InvariantCulture;

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out float value)
        => float.TryParse(span, NumberStyles.Any, _culture, out value);

    /// <inheritdoc/>
    public bool TryFormat(float value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _culture);
}

/// <summary>
/// Converts <see cref="double"/> fields that may include a trailing <c>%</c> character.
/// On parse: strips the trailing <c>%</c> (if present) then parses as double (InvariantCulture).
/// On format: writes the double value followed by <c>%</c>.
/// </summary>
public sealed class PercentDoubleConverter : ISpanConverter<double>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly PercentDoubleConverter Instance = new();

    private PercentDoubleConverter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out double value)
    {
        ReadOnlySpan<char> trimmed = span.TrimEnd();
        if (!trimmed.IsEmpty && trimmed[trimmed.Length - 1] == '%')
            trimmed = trimmed[..^1];
        return double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    /// <inheritdoc/>
    public bool TryFormat(double value, Span<char> destination, out int charsWritten)
    {
        Span<char> tmp = stackalloc char[24];
        if (!value.TryFormat(tmp, out int cw, default, CultureInfo.InvariantCulture))
        {
            charsWritten = 0;
            return false;
        }

        if (cw + 1 > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        tmp[..cw].CopyTo(destination);
        destination[cw] = '%';
        charsWritten = cw + 1;
        return true;
    }
}
