using System.ComponentModel;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Read helpers called by source-generated <see cref="IRecordDescriptor{T}.Parse(ReadOnlySpan{char}, int)"/> implementations.
/// </summary>
/// <remarks>
/// Infrastructure for generated code, not intended for direct use; generated code in consumer assemblies binds
/// to these signatures, which therefore stay stable in 4.x.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class RecordParsing
{
    /// <summary>
    /// Finds the line break that ends the first physical line of a delimited
    /// <see cref="FieldInNewLineAttribute"/> record: the first <c>\r</c> or <c>\n</c> that is not inside a quoted
    /// value. A quoted value of a <see cref="FieldQuotedAttribute"/> field whose multiline mode allows reading
    /// may contain line breaks (FileHelpers 3.x continues such a value on the next line).
    /// </summary>
    /// <param name="text">The rest of the record, starting at the first field of the line.</param>
    /// <param name="lineFields">The fields of this physical line, in order.</param>
    /// <param name="recordDelimiter">The record delimiter (used by fields without [FieldDelimiter]).</param>
    /// <returns>The index of the line break, or -1 when the line runs to the end of <paramref name="text"/>.</returns>
    public static int IndexOfLineBreak(ReadOnlySpan<char> text, ReadOnlySpan<FieldDescriptor> lineFields, string recordDelimiter)
    {
        ArgumentNullException.ThrowIfNull(recordDelimiter);
        int firstBreak = text.IndexOfAny('\r', '\n');
        if (firstBreak < 0 || !AnyQuoted(lineFields))
            return firstBreak;

        int field = 0;
        int i = 0;
        bool atFieldStart = true;
        while (i < text.Length)
        {
            var current = (uint)field < (uint)lineFields.Length ? lineFields[field] : default;
            if (atFieldStart)
            {
                atFieldStart = false;
                if (current.QuoteChar != '\0' && OpeningQuote(text, i, current, recordDelimiter) is var quote and >= 0)
                {
                    int end = QuotedEnd(text, quote + 1, current.QuoteChar);
                    bool multiline = current.QuoteMultiline is MultilineMode.AllowForRead or MultilineMode.AllowForBoth;
                    int innerBreak = text[(quote + 1)..].IndexOfAny('\r', '\n');
                    innerBreak = innerBreak < 0 ? -1 : innerBreak + quote + 1;

                    // An unclosed value, or a line break the field may not contain, ends the line at that break;
                    // the parser reports the unclosed quote.
                    if (innerBreak >= 0 && (end < 0 || (innerBreak < end && !multiline)))
                        return innerBreak;
                    if (end < 0)
                        return -1;
                    i = end;
                    continue;
                }
            }

            var delimiter = current.Delimiter ?? recordDelimiter;
            var rest = text[i..];
            int lineBreak = rest.IndexOfAny('\r', '\n');
            int next = delimiter.Length == 1 ? rest.IndexOf(delimiter[0]) : rest.IndexOf(delimiter, StringComparison.Ordinal);
            if (lineBreak >= 0 && (next < 0 || lineBreak < next))
                return i + lineBreak;
            if (next < 0)
                return -1;

            i += next + delimiter.Length;
            field++;
            atFieldStart = true;
        }

        return -1;
    }

    private static bool AnyQuoted(ReadOnlySpan<FieldDescriptor> fields)
    {
        foreach (ref readonly var field in fields)
        {
            if (field.QuoteChar != '\0')
                return true;
        }

        return false;
    }

    /// <summary>Index of the opening quote of <paramref name="field"/> at <paramref name="start"/>, or -1.</summary>
    private static int OpeningQuote(ReadOnlySpan<char> text, int start, FieldDescriptor field, string recordDelimiter)
    {
        int i = start;
        if (field.TrimMode is TrimMode.Both or TrimMode.Left)
        {
            // 3.x trims the start of the field before looking for the quote.
            string trimChars = field.TrimChars ?? " \t";
            string delimiter = field.Delimiter ?? recordDelimiter;
            while (i < text.Length && trimChars.Contains(text[i], StringComparison.Ordinal) && !text[i..].StartsWith(delimiter, StringComparison.Ordinal))
                i++;
        }

        return i < text.Length && text[i] == field.QuoteChar ? i : -1;
    }

    /// <summary>Index after the closing quote (doubled quotes are escapes), or -1 when the value does not close.</summary>
    private static int QuotedEnd(ReadOnlySpan<char> text, int start, char quote)
    {
        int i = start;
        while (true)
        {
            int q = text[i..].IndexOf(quote);
            if (q < 0)
                return -1;

            i += q;
            if (i + 1 < text.Length && text[i + 1] == quote)
            {
                i += 2;
                continue;
            }

            return i + 1;
        }
    }
}
