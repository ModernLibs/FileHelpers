using System.Globalization;

namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// A date format made only of fixed-width numeric parts (<c>yyyy</c>, <c>MM</c>, <c>dd</c>, <c>HH</c>,
/// <c>mm</c>, <c>ss</c>) and separator characters, such as the FileHelpers 3.x default <c>ddMMyyyy</c>,
/// <c>yyyy-MM-dd</c> or the sortable pattern <c>s</c>. It parses a value in place, without the general
/// format engine of <see cref="DateTime.TryParseExact(ReadOnlySpan{char}, ReadOnlySpan{char}, IFormatProvider?, DateTimeStyles, out DateTime)"/>.
/// </summary>
/// <remarks>
/// This is a fast path only. <see cref="TryParse"/> accepts a strict subset of what
/// <c>DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None)</c> accepts
/// (exact widths, ASCII digits, a date in range) and then gives the same value with
/// <see cref="DateTimeKind.Unspecified"/>. For anything else it returns <see langword="false"/> and the
/// caller runs <c>TryParseExact</c>, so a rejected value still gets the framework's answer.
/// </remarks>
internal sealed class FixedDateFormat
{
    private const byte Literal = 0;
    private const byte Year = 1;
    private const byte Month = 2;
    private const byte Day = 3;
    private const byte Hour = 4;
    private const byte Minute = 5;
    private const byte Second = 6;

    /// <summary>For each character of a matching value: the part it belongs to, or <see cref="Literal"/>.</summary>
    private readonly byte[] _parts;

    /// <summary>The pattern; its character at each <see cref="Literal"/> position is the expected one.</summary>
    private readonly string _literals;

    private FixedDateFormat(byte[] parts, string literals)
    {
        _parts = parts;
        _literals = literals;
    }

    /// <summary>
    /// The plan for <paramref name="format"/> with <paramref name="provider"/>, or <see langword="null"/>
    /// when the format or provider is not supported (the caller then always uses <c>TryParseExact</c>).
    /// </summary>
    internal static FixedDateFormat? TryCreate(string format, IFormatProvider provider)
    {
        // Separators '/' and ':' are culture placeholders; only the invariant culture is known to map them to themselves.
        if (!ReferenceEquals(provider, CultureInfo.InvariantCulture))
            return null;

        string pattern = string.Equals(format, "s", StringComparison.Ordinal) ? "yyyy-MM-ddTHH:mm:ss" : format;
        if (pattern.Length < 8 || pattern.Length > 32)
            return null;

        var parts = new byte[pattern.Length];
        int seen = 0;
        for (int i = 0; i < pattern.Length;)
        {
            char c = pattern[i];
            int run = 1;
            while (i + run < pattern.Length && pattern[i + run] == c)
                run++;

            byte part = (c, run) switch
            {
                ('y', 4) => Year,
                ('M', 2) => Month,
                ('d', 2) => Day,
                ('H', 2) => Hour,
                ('m', 2) => Minute,
                ('s', 2) => Second,
                _ => Literal,
            };

            if (part == Literal)
            {
                // Only plain separators: letters, quotes and escapes have format meaning. A blank matches only itself here.
                // 'T' is not a custom format specifier, so it is a literal (as in the expansion of "s").
                if (c is not ('-' or '/' or ':' or '.' or 'T' or '_' or ' '))
                    return null;
                i++;
                continue;
            }

            int bit = 1 << part;
            if ((seen & bit) != 0)
                return null; // a part twice
            seen |= bit;
            parts.AsSpan(i, run).Fill(part);
            i += run;
        }

        // Without a full date TryParseExact fills in today's date; leave those formats to it.
        const int fullDate = (1 << Year) | (1 << Month) | (1 << Day);
        if ((seen & fullDate) != fullDate)
            return null;

        return new FixedDateFormat(parts, pattern);
    }

    /// <summary>
    /// Parses <paramref name="text"/> (already trimmed) when it has exactly the shape of the format;
    /// <see langword="false"/> means "not decided here", not "invalid".
    /// </summary>
    internal bool TryParse(ReadOnlySpan<char> text, out DateTime value)
    {
        value = default;
        byte[] parts = _parts;
        if (text.Length != parts.Length)
            return false;

        int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
        string literals = _literals;
        for (int i = 0; i < parts.Length; i++)
        {
            char c = text[i];
            byte part = parts[i];
            if (part == Literal)
            {
                if (c != literals[i])
                    return false;
                continue;
            }

            uint digit = (uint)(c - '0');
            if (digit > 9)
                return false;

            switch (part)
            {
                case Year: year = (year * 10) + (int)digit; break;
                case Month: month = (month * 10) + (int)digit; break;
                case Day: day = (day * 10) + (int)digit; break;
                case Hour: hour = (hour * 10) + (int)digit; break;
                case Minute: minute = (minute * 10) + (int)digit; break;
                default: second = (second * 10) + (int)digit; break;
            }
        }

        if (year < 1 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)
            || hour > 23 || minute > 59 || second > 59)
            return false;

        value = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        return true;
    }
}
