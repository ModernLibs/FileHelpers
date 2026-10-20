namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// Quote-aware scanning of delimited lines, shared by multiline assembly and field-count validation.
/// </summary>
/// <remarks>
/// <para>FileHelpers 3.x rules: only a <see cref="FieldQuotedAttribute"/> field can be quoted, each with its own
/// quote character; quote characters in other fields are literal text. A quote character only opens a quoted
/// value at the start of the field (line start or right after a delimiter). Inside a quoted value a doubled
/// quote is an escaped quote; the next single quote closes the value. Quote characters anywhere else
/// (<c>5" ruler</c>, text after a closing quote) are literal.</para>
/// <para>Trim characters (<see cref="FieldDescriptor.TrimChars"/>, spaces/tabs by default) before the opening quote are skipped only for fields whose
/// <see cref="FieldDescriptor.TrimMode"/> trims the start (<see cref="TrimMode.Both"/> or
/// <see cref="TrimMode.Left"/>), because 3.x trims such a field before looking for the quote. For any other
/// field a quote after blanks is literal text.</para>
/// </remarks>
internal static class QuoteBalanceScanner
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="line"/> ends inside an open quoted field.
    /// </summary>
    /// <param name="line">The physical line (no terminator).</param>
    /// <param name="startsInsideQuotes">
    /// <see langword="true"/> when the line continues a quoted field opened on a previous line.
    /// </param>
    /// <param name="delimiter">Field delimiter (non-empty).</param>
    /// <param name="quotes">
    /// Per field index: the quote character, or <c>'\0'</c> when the field is not quoted. Fields past the end
    /// of the span are not quoted.
    /// </param>
    /// <param name="trimStart">
    /// Per field index: the characters skipped before an opening quote (the field's [FieldTrim] chars when it
    /// trims its start), or <see langword="null"/> when none are skipped. Fields past the end of the span do
    /// not skip any.
    /// </param>
    /// <param name="field">
    /// In: index of the field the line starts in (the open quoted field when
    /// <paramref name="startsInsideQuotes"/> is set, otherwise usually 0). Out: index of the field the line
    /// ends in.
    /// </param>
    /// <param name="fieldDelimiters">
    /// Per field index: the delimiter that follows the field (its [FieldDelimiter], as in 3.x), or
    /// <see langword="null"/> for <paramref name="delimiter"/>. Empty (the default) or fields past the end of
    /// the span use <paramref name="delimiter"/>.
    /// </param>
    public static bool EndsInsideQuotes(ReadOnlySpan<char> line, bool startsInsideQuotes, ReadOnlySpan<char> delimiter, ReadOnlySpan<char> quotes, ReadOnlySpan<string?> trimStart, ref int field, ReadOnlySpan<string?> fieldDelimiters = default)
    {
        field = Scan(line, startsInsideQuotes, delimiter, quotes, trimStart, fieldDelimiters, field, out bool endsInside, out _);
        return endsInside;
    }

    /// <summary>Counts the fields of a complete logical record (delimiters outside quoted fields + 1).</summary>
    /// <param name="line">The complete logical record.</param>
    /// <param name="delimiter">Field delimiter (non-empty).</param>
    /// <param name="quotes">Per field index: quote character, <c>'\0'</c> when not quoted.</param>
    /// <param name="trimStart">Per field index: characters skipped before an opening quote (<see langword="null"/>: none).</param>
    /// <param name="fieldDelimiters">Per field index: the delimiter that follows the field (<see langword="null"/>: <paramref name="delimiter"/>).</param>
    public static int CountFields(ReadOnlySpan<char> line, ReadOnlySpan<char> delimiter, ReadOnlySpan<char> quotes, ReadOnlySpan<string?> trimStart, ReadOnlySpan<string?> fieldDelimiters = default) =>
        Scan(line, startsInsideQuotes: false, delimiter, quotes, trimStart, fieldDelimiters, 0, out _, out _) + 1;

    /// <summary>
    /// When the field <paramref name="line"/> ends in is a quoted value that closes on the line, the index after
    /// its closing quote; otherwise -1. Parameters as for <see cref="CountFields"/>.
    /// </summary>
    public static int QuotedValueEnd(ReadOnlySpan<char> line, ReadOnlySpan<char> delimiter, ReadOnlySpan<char> quotes, ReadOnlySpan<string?> trimStart, ReadOnlySpan<string?> fieldDelimiters = default)
    {
        _ = Scan(line, startsInsideQuotes: false, delimiter, quotes, trimStart, fieldDelimiters, 0, out bool endsInside, out int quotedEnd);
        return endsInside ? -1 : quotedEnd;
    }

    private static char QuoteOf(ReadOnlySpan<char> quotes, int field) =>
        (uint)field < (uint)quotes.Length ? quotes[field] : '\0';

    private static ReadOnlySpan<char> DelimiterOf(ReadOnlySpan<char> delimiter, ReadOnlySpan<string?> fieldDelimiters, int field) =>
        (uint)field < (uint)fieldDelimiters.Length && fieldDelimiters[field] is { } own ? own : delimiter;

    /// <summary>
    /// Scans <paramref name="line"/>; returns the index of the field the line ends in. <paramref name="quotedEnd"/>
    /// is the index after the closing quote of that field's quoted value, or -1 when it has none.
    /// </summary>
    private static int Scan(ReadOnlySpan<char> line, bool startsInsideQuotes, ReadOnlySpan<char> recordDelimiter, ReadOnlySpan<char> quotes, ReadOnlySpan<string?> trimStart, ReadOnlySpan<string?> fieldDelimiters, int field, out bool endsInside, out int quotedEnd)
    {
        var delimiter = DelimiterOf(recordDelimiter, fieldDelimiters, field);
        char quote = QuoteOf(quotes, field);
        bool inside = startsInsideQuotes && quote != '\0';
        bool atFieldStart = !inside;
        int i = 0;
        int length = line.Length;
        quotedEnd = -1;

        while (i < length)
        {
            if (inside)
            {
                // After the closing quote, anything up to the next delimiter is literal.
                i = SkipQuotedContent(line, i, quote, out inside);
                if (!inside)
                    quotedEnd = i;
                atFieldStart = false;
                continue;
            }

            if (atFieldStart)
            {
                atFieldStart = false;
                quote = QuoteOf(quotes, field);
                if (quote != '\0')
                {
                    int start = i;
                    if ((uint)field < (uint)trimStart.Length && trimStart[field] is { } trimChars)
                    {
                        while (start < length && trimChars.Contains(line[start], StringComparison.Ordinal) && !line[start..].StartsWith(delimiter, StringComparison.Ordinal))
                            start++;
                    }

                    if (start < length && line[start] == quote)
                    {
                        inside = true;
                        i = start + 1;
                        continue;
                    }
                }
            }

            int d = delimiter.Length == 1 ? line[i..].IndexOf(delimiter[0]) : line[i..].IndexOf(delimiter, StringComparison.Ordinal);
            if (d < 0)
            {
                i = length;
                break;
            }

            i += d + delimiter.Length;
            field++;
            quotedEnd = -1;
            delimiter = DelimiterOf(recordDelimiter, fieldDelimiters, field);
            atFieldStart = true;
        }

        endsInside = inside;
        return field;
    }

    /// <summary>
    /// Scans quoted content from <paramref name="start"/>; returns the index after the closing quote, or the
    /// line length with <paramref name="stillInside"/> set when the field does not close on this line.
    /// </summary>
    private static int SkipQuotedContent(ReadOnlySpan<char> line, int start, char quote, out bool stillInside)
    {
        int i = start;
        while (true)
        {
            int q = line[i..].IndexOf(quote);
            if (q < 0)
            {
                stillInside = true;
                return line.Length;
            }

            i += q;
            if (i + 1 < line.Length && line[i + 1] == quote)
            {
                i += 2; // escaped quote
                continue;
            }

            stillInside = false;
            return i + 1;
        }
    }
}
