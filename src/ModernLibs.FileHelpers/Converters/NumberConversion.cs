using System.Buffers;
using System.Globalization;
using System.Numerics;

namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Shared FileHelpers 3.x numeric parsing rules (<c>ConvertHelpers.RemoveBlanks</c> +
/// <c>CultureConverter</c>) for the built-in numeric converters.
/// </summary>
internal static class NumberConversion
{
    /// <summary>3.x styles for every numeric converter except <see cref="byte"/> / <see cref="sbyte"/>.</summary>
    internal const NumberStyles Styles = NumberStyles.Number | NumberStyles.AllowExponent;

    /// <summary>3.x styles for <see cref="byte"/> / <see cref="sbyte"/> (no exponent).</summary>
    internal const NumberStyles ByteStyles = NumberStyles.Number;

    private const int StackallocLimit = 128;

    /// <summary>Invariant numbers with <c>','</c> as decimal and <c>'.'</c> as group separator.</summary>
    private static readonly NumberFormatInfo CommaDecimal = CreateCommaDecimal();

    /// <summary>
    /// Resolves a 3.x <c>decimalSepOrCultureName</c> argument: <c>"."</c> (decimal point, <c>','</c>
    /// groups), <c>","</c> (decimal comma, <c>'.'</c> groups) — both otherwise invariant — or a culture name.
    /// </summary>
    /// <exception cref="BadUsageException">The value is neither a separator nor a known culture name.</exception>
    internal static IFormatProvider ResolveSeparator(string decimalSeparatorOrCultureName)
    {
        ArgumentNullException.ThrowIfNull(decimalSeparatorOrCultureName);
        switch (decimalSeparatorOrCultureName)
        {
            case ".":
                return NumberFormatInfo.InvariantInfo;
            case ",":
                return CommaDecimal;
            default:
                try
                {
                    return CultureInfo.GetCultureInfo(decimalSeparatorOrCultureName);
                }
                catch (CultureNotFoundException ex)
                {
                    throw new BadUsageException(
                        "The decimal separator or culture name '" + decimalSeparatorOrCultureName +
                        "' is invalid. Use '.' or ',' as decimal separator, or a culture name such as 'en-US'.",
                        ex);
                }
        }
    }

    /// <summary>
    /// Parses <paramref name="span"/> like 3.x: leading blanks and blanks between a leading sign and
    /// the digits are removed (<c>" +  21 "</c> → <c>"+21 "</c>), then <typeparamref name="T"/> is
    /// parsed with <paramref name="styles"/> and <paramref name="provider"/>. Allocation-free except
    /// for inputs longer than 128 characters with a detached sign (pooled buffer).
    /// </summary>
    internal static bool TryParse<T>(ReadOnlySpan<char> span, NumberStyles styles, IFormatProvider provider, out T value)
        where T : struct, INumberBase<T>
    {
        // The fast path stays small and inlinable; the 3.x slow path (with its stackalloc) lives in a
        // separate method so the common case pays no frame or stack-probe cost.
        return TryParseSimple(span, provider, out value) || TryParseFull(span, styles, provider, out value);
    }

    private static bool TryParseFull<T>(ReadOnlySpan<char> span, NumberStyles styles, IFormatProvider provider, out T value)
        where T : struct, INumberBase<T>
    {
        span = span.TrimStart();
        if (span.Length < 2 || (span[0] != '+' && span[0] != '-') || !char.IsWhiteSpace(span[1]))
            return T.TryParse(span, styles, provider, out value);

        ReadOnlySpan<char> digits = span[1..].TrimStart();
        int length = digits.Length + 1;
        char[]? rented = null;
        Span<char> joined = length <= StackallocLimit
            ? stackalloc char[StackallocLimit]
            : (rented = ArrayPool<char>.Shared.Rent(length));
        joined[0] = span[0];
        digits.CopyTo(joined[1..]);
        bool ok = T.TryParse(joined[..length], styles, provider, out value);
        if (rented is not null)
            ArrayPool<char>.Shared.Return(rented);
        return ok;
    }

    /// <summary>Powers of ten exactly representable as <see cref="double"/> (Clinger fast path).</summary>
    private static ReadOnlySpan<double> Pow10 =>
    [
        1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11,
        1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22,
    ];

    /// <summary>
    /// Performance fast path for the common shape <c>[blanks][+|-]digits[.digits][blanks]</c> with the
    /// invariant number format. It yields exactly the value the 3.x-compatible slow path produces
    /// (same styles accept the text; integer and decimal results are exact, doubles use the correctly
    /// rounded Clinger fast path) and declines — returning <see langword="false"/> so the caller falls back
    /// to the full parse — for anything else: other cultures or separators, group separators, exponents,
    /// too many digits, signed zero for floating types, or a sign on unsigned types.
    /// </summary>
    private static bool TryParseSimple<T>(ReadOnlySpan<char> span, IFormatProvider provider, out T value)
        where T : struct, INumberBase<T>
    {
        value = default;
        if (!ReferenceEquals(provider, CultureInfo.InvariantCulture) && !ReferenceEquals(provider, NumberFormatInfo.InvariantInfo))
            return false;

        if (typeof(T) == typeof(int) || typeof(T) == typeof(long) || typeof(T) == typeof(short) || typeof(T) == typeof(sbyte))
        {
            // The digit limit guarantees the value fits, so truncation is exact.
            if (!TryScanInteger(span, MaxSimpleDigits<T>(), out ulong magnitude, out bool negative))
                return false;
            value = T.CreateTruncating(negative ? -(long)magnitude : (long)magnitude);
            return true;
        }

        if (typeof(T) == typeof(uint) || typeof(T) == typeof(ulong) || typeof(T) == typeof(ushort) || typeof(T) == typeof(byte))
        {
            if (!TryScanInteger(span, MaxSimpleDigits<T>(), out ulong magnitude, out bool negative) || negative)
                return false;
            value = T.CreateTruncating(magnitude);
            return true;
        }

        return TryParseSimpleFloating(span, out value);
    }

    /// <summary>Fast path for <see cref="decimal"/>, <see cref="double"/> and <see cref="float"/> (see <see cref="TryParseSimple{T}"/>).</summary>
    private static bool TryParseSimpleFloating<T>(ReadOnlySpan<char> span, out T value)
        where T : struct, INumberBase<T>
    {
        value = default;
        int maxDigits = MaxSimpleDigits<T>();
        if (maxDigits == 0 || !TryScanFraction(span, maxDigits, out ulong accumulator, out bool negative, out int scale))
            return false;
        if (accumulator == 0 && negative)
            return false; // signed zero: leave to the full parser

        if (typeof(T) == typeof(decimal))
        {
            var d = new decimal((int)(uint)accumulator, (int)(uint)(accumulator >> 32), 0, negative, (byte)scale);
            value = (T)(object)d;
            return true;
        }
        if (typeof(T) == typeof(double))
        {
            if (scale >= Pow10.Length) return false;
            double v = accumulator / Pow10[scale];
            value = (T)(object)(negative ? -v : v);
            return true;
        }
        if (typeof(T) == typeof(float))
        {
            if (scale > 10) return false;
            float v = accumulator / (float)Pow10[scale];
            value = (T)(object)(negative ? -v : v);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Scans <c>[blanks][+|-]digits[blanks]</c> with at most <paramref name="maxDigits"/> digits; blanks are the
    /// characters accepted by <c>NumberStyles.AllowLeadingWhite</c> / <c>AllowTrailingWhite</c> (U+0009..U+000D, U+0020).
    /// </summary>
    private static bool TryScanInteger(ReadOnlySpan<char> span, int maxDigits, out ulong magnitude, out bool negative)
    {
        magnitude = 0;
        negative = false;
        int start = 0;
        int end = span.Length;
        while (start < end && IsNumberWhite(span[start])) start++;
        while (end > start && IsNumberWhite(span[end - 1])) end--;
        if (start == end) return false;

        char first = span[start];
        if (first is '-' or '+')
        {
            negative = first == '-';
            start++;
        }

        if ((uint)(end - start - 1) >= (uint)maxDigits)
            return false; // no digits, or too many to rule out overflow

        ulong accumulator = 0;
        for (int i = start; i < end; i++)
        {
            uint d = (uint)(span[i] - '0');
            if (d > 9) return false;
            accumulator = accumulator * 10 + d;
        }

        magnitude = accumulator;
        return true;
    }

    /// <summary>
    /// Scans <c>[blanks][+|-]digits[.digits][blanks]</c> (at least one digit, at most <paramref name="maxDigits"/>).
    /// </summary>
    private static bool TryScanFraction(ReadOnlySpan<char> span, int maxDigits, out ulong accumulator, out bool negative, out int scale)
    {
        accumulator = 0;
        negative = false;
        scale = -1;
        int start = 0;
        int end = span.Length;
        while (start < end && IsNumberWhite(span[start])) start++;
        while (end > start && IsNumberWhite(span[end - 1])) end--;
        if (start == end) return false;

        char first = span[start];
        if (first is '-' or '+')
        {
            negative = first == '-';
            start++;
        }

        int digits = 0;
        for (int i = start; i < end; i++)
        {
            char c = span[i];
            uint d = (uint)(c - '0');
            if (d <= 9)
            {
                if (++digits > maxDigits) return false;
                accumulator = accumulator * 10 + d;
                if (scale >= 0) scale++;
            }
            else if (c == '.' && scale < 0)
            {
                scale = 0;
            }
            else
            {
                return false;
            }
        }

        if (scale < 0) scale = 0;
        return digits > 0;
    }

    /// <summary>Digit count below which the accumulated value cannot overflow <typeparamref name="T"/> or lose precision.</summary>
    private static int MaxSimpleDigits<T>()
    {
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint)) return 9;
        if (typeof(T) == typeof(long)) return 18;
        if (typeof(T) == typeof(ulong)) return 19;
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort)) return 4;
        if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte)) return 2;
        if (typeof(T) == typeof(decimal)) return 18;
        if (typeof(T) == typeof(double)) return 15;
        if (typeof(T) == typeof(float)) return 7;
        return 0;
    }

    private static bool IsNumberWhite(char c) => c == ' ' || (c >= '\t' && c <= '\r');

    private static NumberFormatInfo CreateCommaDecimal()
    {
        var info = (NumberFormatInfo)NumberFormatInfo.InvariantInfo.Clone();
        info.NumberDecimalSeparator = ",";
        info.NumberGroupSeparator = ".";
        return NumberFormatInfo.ReadOnly(info);
    }
}
