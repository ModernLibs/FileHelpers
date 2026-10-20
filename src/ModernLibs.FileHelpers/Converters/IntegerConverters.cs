// MA0048: File name intentionally does not match type names — all integer converters grouped in one file.
#pragma warning disable MA0048
using System.Globalization;

namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="byte"/> fields with the FileHelpers 3.x <c>ByteConverter</c> rules: blanks before
/// and after a leading sign are removed, then the text is parsed with <c>NumberStyles.Number</c> (no exponent)
/// (thousands separators, leading/trailing sign and a zero fraction are accepted). Invariant culture unless
/// a decimal separator or culture is configured. Zero allocations.
/// </summary>
public sealed class ByteConverter : ISpanConverter<byte>
{
    /// <summary>Shared singleton instance (invariant culture: <c>'.'</c> decimal, <c>','</c> group separator).</summary>
    public static readonly ByteConverter Instance = new();

    private readonly IFormatProvider _provider = CultureInfo.InvariantCulture;

    private ByteConverter() { }

    /// <summary>Initializes a converter using <paramref name="culture"/> (<see langword="null"/> means invariant).</summary>
    public ByteConverter(IFormatProvider? culture) => _provider = culture ?? CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a converter from the 3.x <c>decimalSepOrCultureName</c> argument
    /// (<c>[FieldConverter(ConverterKind.Byte, ",")]</c>): <c>"."</c> or <c>","</c> selects the decimal
    /// separator (the other one becomes the group separator); any other value is a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    public ByteConverter(string decimalSeparatorOrCultureName)
        => _provider = NumberConversion.ResolveSeparator(decimalSeparatorOrCultureName);

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out byte value)
        => NumberConversion.TryParse(span, NumberConversion.ByteStyles, _provider, out value);

    /// <inheritdoc/>
    public bool TryFormat(byte value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _provider);
}

/// <summary>
/// Converts <see cref="sbyte"/> fields with the FileHelpers 3.x <c>SByteConverter</c> rules: blanks before
/// and after a leading sign are removed, then the text is parsed with <c>NumberStyles.Number</c> (no exponent)
/// (thousands separators, leading/trailing sign and a zero fraction are accepted). Invariant culture unless
/// a decimal separator or culture is configured. Zero allocations.
/// </summary>
public sealed class SByteConverter : ISpanConverter<sbyte>
{
    /// <summary>Shared singleton instance (invariant culture: <c>'.'</c> decimal, <c>','</c> group separator).</summary>
    public static readonly SByteConverter Instance = new();

    private readonly IFormatProvider _provider = CultureInfo.InvariantCulture;

    private SByteConverter() { }

    /// <summary>Initializes a converter using <paramref name="culture"/> (<see langword="null"/> means invariant).</summary>
    public SByteConverter(IFormatProvider? culture) => _provider = culture ?? CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a converter from the 3.x <c>decimalSepOrCultureName</c> argument
    /// (<c>[FieldConverter(ConverterKind.SByte, ",")]</c>): <c>"."</c> or <c>","</c> selects the decimal
    /// separator (the other one becomes the group separator); any other value is a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    public SByteConverter(string decimalSeparatorOrCultureName)
        => _provider = NumberConversion.ResolveSeparator(decimalSeparatorOrCultureName);

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out sbyte value)
        => NumberConversion.TryParse(span, NumberConversion.ByteStyles, _provider, out value);

    /// <inheritdoc/>
    public bool TryFormat(sbyte value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _provider);
}

/// <summary>
/// Converts <see cref="short"/> fields with the FileHelpers 3.x <c>Int16Converter</c> rules: blanks before
/// and after a leading sign are removed, then the text is parsed with <c>NumberStyles.Number | NumberStyles.AllowExponent</c>
/// (thousands separators, leading/trailing sign and a zero fraction are accepted). Invariant culture unless
/// a decimal separator or culture is configured. Zero allocations.
/// </summary>
public sealed class Int16Converter : ISpanConverter<short>
{
    /// <summary>Shared singleton instance (invariant culture: <c>'.'</c> decimal, <c>','</c> group separator).</summary>
    public static readonly Int16Converter Instance = new();

    private readonly IFormatProvider _provider = CultureInfo.InvariantCulture;

    private Int16Converter() { }

    /// <summary>Initializes a converter using <paramref name="culture"/> (<see langword="null"/> means invariant).</summary>
    public Int16Converter(IFormatProvider? culture) => _provider = culture ?? CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a converter from the 3.x <c>decimalSepOrCultureName</c> argument
    /// (<c>[FieldConverter(ConverterKind.Int16, ",")]</c>): <c>"."</c> or <c>","</c> selects the decimal
    /// separator (the other one becomes the group separator); any other value is a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    public Int16Converter(string decimalSeparatorOrCultureName)
        => _provider = NumberConversion.ResolveSeparator(decimalSeparatorOrCultureName);

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out short value)
        => NumberConversion.TryParse(span, NumberConversion.Styles, _provider, out value);

    /// <inheritdoc/>
    public bool TryFormat(short value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _provider);
}

/// <summary>
/// Converts <see cref="int"/> fields with the FileHelpers 3.x <c>Int32Converter</c> rules: blanks before
/// and after a leading sign are removed, then the text is parsed with <c>NumberStyles.Number | NumberStyles.AllowExponent</c>
/// (thousands separators, leading/trailing sign and a zero fraction are accepted). Invariant culture unless
/// a decimal separator or culture is configured. Zero allocations.
/// </summary>
public sealed class Int32Converter : ISpanConverter<int>
{
    /// <summary>Shared singleton instance (invariant culture: <c>'.'</c> decimal, <c>','</c> group separator).</summary>
    public static readonly Int32Converter Instance = new();

    private readonly IFormatProvider _provider = CultureInfo.InvariantCulture;

    private Int32Converter() { }

    /// <summary>Initializes a converter using <paramref name="culture"/> (<see langword="null"/> means invariant).</summary>
    public Int32Converter(IFormatProvider? culture) => _provider = culture ?? CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a converter from the 3.x <c>decimalSepOrCultureName</c> argument
    /// (<c>[FieldConverter(ConverterKind.Int32, ",")]</c>): <c>"."</c> or <c>","</c> selects the decimal
    /// separator (the other one becomes the group separator); any other value is a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    public Int32Converter(string decimalSeparatorOrCultureName)
        => _provider = NumberConversion.ResolveSeparator(decimalSeparatorOrCultureName);

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out int value)
        => NumberConversion.TryParse(span, NumberConversion.Styles, _provider, out value);

    /// <inheritdoc/>
    public bool TryFormat(int value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _provider);
}

/// <summary>
/// Converts <see cref="long"/> fields with the FileHelpers 3.x <c>Int64Converter</c> rules: blanks before
/// and after a leading sign are removed, then the text is parsed with <c>NumberStyles.Number | NumberStyles.AllowExponent</c>
/// (thousands separators, leading/trailing sign and a zero fraction are accepted). Invariant culture unless
/// a decimal separator or culture is configured. Zero allocations.
/// </summary>
public sealed class Int64Converter : ISpanConverter<long>
{
    /// <summary>Shared singleton instance (invariant culture: <c>'.'</c> decimal, <c>','</c> group separator).</summary>
    public static readonly Int64Converter Instance = new();

    private readonly IFormatProvider _provider = CultureInfo.InvariantCulture;

    private Int64Converter() { }

    /// <summary>Initializes a converter using <paramref name="culture"/> (<see langword="null"/> means invariant).</summary>
    public Int64Converter(IFormatProvider? culture) => _provider = culture ?? CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a converter from the 3.x <c>decimalSepOrCultureName</c> argument
    /// (<c>[FieldConverter(ConverterKind.Int64, ",")]</c>): <c>"."</c> or <c>","</c> selects the decimal
    /// separator (the other one becomes the group separator); any other value is a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    public Int64Converter(string decimalSeparatorOrCultureName)
        => _provider = NumberConversion.ResolveSeparator(decimalSeparatorOrCultureName);

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out long value)
        => NumberConversion.TryParse(span, NumberConversion.Styles, _provider, out value);

    /// <inheritdoc/>
    public bool TryFormat(long value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _provider);
}

/// <summary>
/// Converts <see cref="ushort"/> fields with the FileHelpers 3.x <c>UInt16Converter</c> rules: blanks before
/// and after a leading sign are removed, then the text is parsed with <c>NumberStyles.Number | NumberStyles.AllowExponent</c>
/// (thousands separators, leading/trailing sign and a zero fraction are accepted). Invariant culture unless
/// a decimal separator or culture is configured. Zero allocations.
/// </summary>
public sealed class UInt16Converter : ISpanConverter<ushort>
{
    /// <summary>Shared singleton instance (invariant culture: <c>'.'</c> decimal, <c>','</c> group separator).</summary>
    public static readonly UInt16Converter Instance = new();

    private readonly IFormatProvider _provider = CultureInfo.InvariantCulture;

    private UInt16Converter() { }

    /// <summary>Initializes a converter using <paramref name="culture"/> (<see langword="null"/> means invariant).</summary>
    public UInt16Converter(IFormatProvider? culture) => _provider = culture ?? CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a converter from the 3.x <c>decimalSepOrCultureName</c> argument
    /// (<c>[FieldConverter(ConverterKind.UInt16, ",")]</c>): <c>"."</c> or <c>","</c> selects the decimal
    /// separator (the other one becomes the group separator); any other value is a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    public UInt16Converter(string decimalSeparatorOrCultureName)
        => _provider = NumberConversion.ResolveSeparator(decimalSeparatorOrCultureName);

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out ushort value)
        => NumberConversion.TryParse(span, NumberConversion.Styles, _provider, out value);

    /// <inheritdoc/>
    public bool TryFormat(ushort value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _provider);
}

/// <summary>
/// Converts <see cref="uint"/> fields with the FileHelpers 3.x <c>UInt32Converter</c> rules: blanks before
/// and after a leading sign are removed, then the text is parsed with <c>NumberStyles.Number | NumberStyles.AllowExponent</c>
/// (thousands separators, leading/trailing sign and a zero fraction are accepted). Invariant culture unless
/// a decimal separator or culture is configured. Zero allocations.
/// </summary>
public sealed class UInt32Converter : ISpanConverter<uint>
{
    /// <summary>Shared singleton instance (invariant culture: <c>'.'</c> decimal, <c>','</c> group separator).</summary>
    public static readonly UInt32Converter Instance = new();

    private readonly IFormatProvider _provider = CultureInfo.InvariantCulture;

    private UInt32Converter() { }

    /// <summary>Initializes a converter using <paramref name="culture"/> (<see langword="null"/> means invariant).</summary>
    public UInt32Converter(IFormatProvider? culture) => _provider = culture ?? CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a converter from the 3.x <c>decimalSepOrCultureName</c> argument
    /// (<c>[FieldConverter(ConverterKind.UInt32, ",")]</c>): <c>"."</c> or <c>","</c> selects the decimal
    /// separator (the other one becomes the group separator); any other value is a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    public UInt32Converter(string decimalSeparatorOrCultureName)
        => _provider = NumberConversion.ResolveSeparator(decimalSeparatorOrCultureName);

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out uint value)
        => NumberConversion.TryParse(span, NumberConversion.Styles, _provider, out value);

    /// <inheritdoc/>
    public bool TryFormat(uint value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _provider);
}

/// <summary>
/// Converts <see cref="ulong"/> fields with the FileHelpers 3.x <c>UInt64Converter</c> rules: blanks before
/// and after a leading sign are removed, then the text is parsed with <c>NumberStyles.Number | NumberStyles.AllowExponent</c>
/// (thousands separators, leading/trailing sign and a zero fraction are accepted). Invariant culture unless
/// a decimal separator or culture is configured. Zero allocations.
/// </summary>
public sealed class UInt64Converter : ISpanConverter<ulong>
{
    /// <summary>Shared singleton instance (invariant culture: <c>'.'</c> decimal, <c>','</c> group separator).</summary>
    public static readonly UInt64Converter Instance = new();

    private readonly IFormatProvider _provider = CultureInfo.InvariantCulture;

    private UInt64Converter() { }

    /// <summary>Initializes a converter using <paramref name="culture"/> (<see langword="null"/> means invariant).</summary>
    public UInt64Converter(IFormatProvider? culture) => _provider = culture ?? CultureInfo.InvariantCulture;

    /// <summary>
    /// Initializes a converter from the 3.x <c>decimalSepOrCultureName</c> argument
    /// (<c>[FieldConverter(ConverterKind.UInt64, ",")]</c>): <c>"."</c> or <c>","</c> selects the decimal
    /// separator (the other one becomes the group separator); any other value is a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    public UInt64Converter(string decimalSeparatorOrCultureName)
        => _provider = NumberConversion.ResolveSeparator(decimalSeparatorOrCultureName);

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out ulong value)
        => NumberConversion.TryParse(span, NumberConversion.Styles, _provider, out value);

    /// <inheritdoc/>
    public bool TryFormat(ulong value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, _provider);
}
