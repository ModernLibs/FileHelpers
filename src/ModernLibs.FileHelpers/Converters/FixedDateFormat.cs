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

    /// <summary>Indexes of the <see cref="Literal"/> characters.</summary>
    private readonly int[] _literalPositions;

    /// <summary>First index of each part in a matching value; -1 for a time part the format does not have.</summary>
    private readonly int _yearAt, _monthAt, _dayAt, _hourAt, _minuteAt, _secondAt;

    private FixedDateFormat(byte[] parts, string literals)
    {
        _parts = parts;
        _literals = literals;
        _literalPositions = Enumerable.Range(0, parts.Length).Where(i => parts[i] == Literal).ToArray();
        _yearAt = Array.IndexOf(parts, Year);
        _monthAt = Array.IndexOf(parts, Month);
        _dayAt = Array.IndexOf(parts, Day);
        _hourAt = Array.IndexOf(parts, Hour);
        _minuteAt = Array.IndexOf(parts, Minute);
        _secondAt = Array.IndexOf(parts, Second);
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
        if (text.Length != _parts.Length)
            return false;

        string literals = _literals;
        foreach (int i in _literalPositions)
        {
            if (text[i] != literals[i])
                return false;
        }

        if (!TryDigits4(text, _yearAt, out int year) || !TryDigits2(text, _monthAt, out int month)
            || !TryDigits2(text, _dayAt, out int day))
            return false;

        int hour = 0, minute = 0, second = 0;
        if ((_hourAt >= 0 && !TryDigits2(text, _hourAt, out hour))
            || (_minuteAt >= 0 && !TryDigits2(text, _minuteAt, out minute))
            || (_secondAt >= 0 && !TryDigits2(text, _secondAt, out second)))
            return false;

        if (year < 1 || month is < 1 or > 12 || day < 1 || (day > 28 && day > DateTime.DaysInMonth(year, month))
            || hour > 23 || minute > 59 || second > 59)
            return false;

        value = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
        return true;
    }

    /// <summary>The two ASCII digits at <paramref name="at"/>; <see langword="false"/> when either is not one.</summary>
    private static bool TryDigits2(ReadOnlySpan<char> text, int at, out int value)
    {
        uint tens = (uint)(text[at] - '0');
        uint ones = (uint)(text[at + 1] - '0');
        value = (int)((tens * 10) + ones);
        return tens <= 9 && ones <= 9;
    }

    /// <summary>The four ASCII digits at <paramref name="at"/>; <see langword="false"/> when any is not one.</summary>
    private static bool TryDigits4(ReadOnlySpan<char> text, int at, out int value)
    {
        uint d0 = (uint)(text[at] - '0');
        uint d1 = (uint)(text[at + 1] - '0');
        uint d2 = (uint)(text[at + 2] - '0');
        uint d3 = (uint)(text[at + 3] - '0');
        value = (int)((d0 * 1000) + (d1 * 100) + (d2 * 10) + d3);
        return d0 <= 9 && d1 <= 9 && d2 <= 9 && d3 <= 9;
    }
}
