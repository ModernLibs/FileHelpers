using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
    private static readonly string[] s_oneChar = CreateOneCharStrings();

    private static string[] CreateOneCharStrings()
    {
        var strings = new string[128];
        for (int c = 0; c < strings.Length; c++)
            strings[c] = ((char)c).ToString();
        return strings;
    }

    /// <summary>
    /// Creates the <see cref="string"/> of a field value: <see cref="string.Empty"/> for no characters, a cached
    /// instance for one character below 128, <c>new string</c> otherwise.
    /// </summary>
    /// <param name="value">The characters of the field.</param>
    /// <returns>A string equal to <paramref name="value"/>; short values may share one instance.</returns>
    public static string CreateString(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
            return string.Empty;
        if (value.Length == 1 && value[0] < 128)
            return s_oneChar[value[0]];
        return new string(value);
    }

    /// <summary>
    /// Removes the escape of a quoted value: each doubled <paramref name="quote"/> becomes one; a lone one is kept.
    /// Scans left to right, as the 4.9 emitted strip loop does.
    /// </summary>
    /// <param name="inner">The text between the outer quotes.</param>
    /// <param name="quote">The quote character of the field.</param>
    /// <returns><paramref name="inner"/> itself when it holds no doubled quote, otherwise the span of a new array.</returns>
    public static ReadOnlySpan<char> Unquote(ReadOnlySpan<char> inner, char quote)
    {
        int pairs = CountPairs(inner, quote);
        if (pairs == 0)
            return inner;

        var result = new char[inner.Length - pairs];
        _ = CopyUnquoted(inner, quote, result);
        return result;
    }

    /// <summary>
    /// <see cref="Unquote"/> followed by <see cref="CreateString"/>, without an intermediate buffer.
    /// </summary>
    /// <param name="inner">The text between the outer quotes.</param>
    /// <param name="quote">The quote character of the field.</param>
    /// <returns>The unescaped value as a string.</returns>
    public static string UnquoteToString(ReadOnlySpan<char> inner, char quote)
    {
        int pairs = CountPairs(inner, quote);
        if (pairs == 0)
            return CreateString(inner);

        return string.Create(inner.Length - pairs, new UnquoteState(inner, quote), static (destination, state) => _ = CopyUnquoted(state.Source, state.Quote, destination));
    }

    private readonly ref struct UnquoteState(ReadOnlySpan<char> source, char quote)
    {
        public readonly ReadOnlySpan<char> Source = source;
        public readonly char Quote = quote;
    }

    /// <summary>Number of doubled quotes, found left to right: a quote followed by a quote is one pair, then scanning resumes after both.</summary>
    private static int CountPairs(ReadOnlySpan<char> inner, char quote)
    {
        int first = inner.IndexOf(quote);
        if (first < 0)
            return 0;

        int pairs = 0;
        for (int at = first; at >= 0;)
        {
            int next;
            if (at + 1 < inner.Length && inner[at + 1] == quote)
            {
                pairs++;
                next = at + 2;
            }
            else
            {
                next = at + 1;
            }

            int found = inner.Slice(next).IndexOf(quote);
            at = found < 0 ? -1 : next + found;
        }

        return pairs;
    }

    /// <summary>
    /// Copies <paramref name="source"/> to <paramref name="destination"/> with each doubled <paramref name="quote"/>
    /// collapsed (a lone one is kept); returns the chars written. <paramref name="destination"/> holds at least
    /// <c>source.Length</c> chars minus the pairs. Also the unescape of <c>TokenBuffer</c>'s scratch (T15).
    /// </summary>
    internal static int CopyUnquoted(ReadOnlySpan<char> source, char quote, Span<char> destination)
    {
        int written = 0;
        while (true)
        {
            int at = source.IndexOf(quote);
            if (at < 0)
            {
                source.CopyTo(destination.Slice(written));
                return written + source.Length;
            }

            source.Slice(0, at + 1).CopyTo(destination.Slice(written));
            written += at + 1;
            int skip = at + 1 < source.Length && source[at + 1] == quote ? at + 2 : at + 1;
            source = source.Slice(skip);
        }
    }

    /// <summary>
    /// The 1-based column of <paramref name="value"/> in <paramref name="record"/>, or -1 when the value is not part
    /// of it (for example an unescaped copy). An empty value has no characters to overlap, so its position is compared directly.
    /// </summary>
    /// <param name="record">The record text.</param>
    /// <param name="value">A slice of <paramref name="record"/>, or any other span.</param>
    /// <returns>The column, or -1.</returns>
    public static int Column(ReadOnlySpan<char> record, ReadOnlySpan<char> value)
    {
        if (record.Overlaps(value, out int offset))
            return offset + 1;
        if (!value.IsEmpty)
            return -1;
        nint bytes = Unsafe.ByteOffset(
            ref MemoryMarshal.GetReference(record),
            ref MemoryMarshal.GetReference(value));
        return bytes >= 0 && bytes <= (nint)record.Length * sizeof(char) ? (int)(bytes / sizeof(char)) + 1 : -1;
    }

    /// <summary>Builds the exception for a value that a converter could not convert.</summary>
    /// <param name="record">The record text.</param>
    /// <param name="value">The field value.</param>
    /// <param name="type">The destination type.</param>
    /// <param name="field">The field name.</param>
    /// <param name="extra">Extra information, or <see langword="null"/>.</param>
    /// <param name="inner">The underlying exception, or <see langword="null"/>.</param>
    /// <returns>A <see cref="ConvertException"/> with line -1 and the column of <paramref name="value"/>.</returns>
    public static ConvertException ConvertError(ReadOnlySpan<char> record, ReadOnlySpan<char> value, Type type, string field, string? extra, Exception? inner) =>
        new(new string(value), type, field, -1, Column(record, value), extra, inner);

    /// <summary>Builds the exception for an empty value of a value-type field without <c>[FieldNullValue]</c>.</summary>
    /// <param name="record">The record text.</param>
    /// <param name="value">The field value.</param>
    /// <param name="field">The field name.</param>
    /// <param name="recordType">The record type name.</param>
    /// <returns>A <see cref="NullValueNotFoundException"/> with line -1 and the column of <paramref name="value"/>.</returns>
    public static NullValueNotFoundException NullValueNotFound(ReadOnlySpan<char> record, ReadOnlySpan<char> value, string field, string recordType) =>
        new(-1, Column(record, value), "No value found for the value type field: '" + field + "' Class: '" + recordType + "'. You must use the [FieldNullValue] attribute because this is a value type and can't be null or use a Nullable Type instead of the current type.");

    /// <summary>Builds the exception for a custom converter that threw.</summary>
    /// <param name="record">The record text.</param>
    /// <param name="value">The field value.</param>
    /// <param name="type">The destination type.</param>
    /// <param name="field">The field name.</param>
    /// <param name="converter">The converter type name.</param>
    /// <param name="exception">The exception the converter threw.</param>
    /// <returns>A <see cref="ConvertException"/>; a <see cref="ConvertException"/> thrown by the converter keeps its extra text.</returns>
    public static ConvertException ConverterFailure(ReadOnlySpan<char> record, ReadOnlySpan<char> value, Type type, string field, string converter, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is ConvertException convert)
            return ConvertError(record, value, type, field, convert.MessageExtra, convert);
        return ConvertError(record, value, type, field, "Your custom converter: " + converter + " throws an " + exception.GetType().Name + " with the message: " + exception.Message, exception);
    }

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
                if (current.QuoteChar != '\0' && OpeningQuote(text, i, in current, recordDelimiter) is var quote and >= 0)
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
    private static int OpeningQuote(ReadOnlySpan<char> text, int start, in FieldDescriptor field, string recordDelimiter)
    {
        int i = start;
        if (field.TrimMode is TrimMode.Both or TrimMode.Left)
        {
            // 3.x trims the start of the field before looking for the quote.
            string trimChars = field.TrimChars ?? " \t";
            string delimiter = field.Delimiter ?? recordDelimiter;
            // [FieldTrim(TrimGivenChars = true)]: the quote char is trimmed after unquoting, never skipped before the quote.
            bool keepQuote = field.TrimGivenChars;
            while (i < text.Length && !(keepQuote && text[i] == field.QuoteChar) && trimChars.Contains(text[i], StringComparison.Ordinal) && !text[i..].StartsWith(delimiter, StringComparison.Ordinal))
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
