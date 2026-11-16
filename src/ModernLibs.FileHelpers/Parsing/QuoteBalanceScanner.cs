using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

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

    /// <summary>
    /// <see cref="EndsInsideQuotes(ReadOnlySpan{char}, bool, ReadOnlySpan{char}, ReadOnlySpan{char}, ReadOnlySpan{string?}, ref int, ReadOnlySpan{string?})"/>
    /// with the shortcuts of <paramref name="plan"/>, made by <see cref="Plan"/> for the same rules. The result is
    /// the same.
    /// </summary>
    /// <param name="line">The physical line (no terminator).</param>
    /// <param name="startsInsideQuotes"><see langword="true"/> when the line continues an open quoted field.</param>
    /// <param name="delimiter">Field delimiter (non-empty).</param>
    /// <param name="quotes">Per field index: the quote character, or <c>'\0'</c>.</param>
    /// <param name="trimStart">Per field index: the characters skipped before an opening quote, or <see langword="null"/>.</param>
    /// <param name="field">In: the field the line starts in. Out: the field the line ends in.</param>
    /// <param name="fieldDelimiters">Per field index: the delimiter that follows the field, or <see langword="null"/>.</param>
    /// <param name="plan">The plan for these rules.</param>
    public static bool EndsInsideQuotes(ReadOnlySpan<char> line, bool startsInsideQuotes, ReadOnlySpan<char> delimiter, ReadOnlySpan<char> quotes, ReadOnlySpan<string?> trimStart, ref int field, ReadOnlySpan<string?> fieldDelimiters, QuoteScanPlan plan)
    {
        if (plan.Quote != '\0')
        {
            // A quote only opens or closes a quoted value, and every quoted field uses plan.Quote: without it the
            // line stays in the quoted value it starts in, or else every delimiter ends a field.
            if (!line.Contains(plan.Quote))
            {
                if (startsInsideQuotes && QuoteOf(quotes, field) != '\0')
                    return true;
                field += delimiter.Length == 1 ? line.Count(delimiter[0]) : line.Count(delimiter);
                return false;
            }

            if (plan.Vectorized)
            {
                field = ScanSingleQuote(line, startsInsideQuotes, delimiter[0], plan.Quote, quotes, field, out bool inside);
                return inside;
            }
        }

        return EndsInsideQuotes(line, startsInsideQuotes, delimiter, quotes, trimStart, ref field, fieldDelimiters);
    }

    /// <summary>The shortcuts that the scan of lines under these rules may take.</summary>
    /// <param name="delimiter">Field delimiter (non-empty).</param>
    /// <param name="quotes">Per field index: the quote character, or <c>'\0'</c>.</param>
    /// <param name="trimStart">Per field index: the characters skipped before an opening quote, or <see langword="null"/>.</param>
    /// <param name="fieldDelimiters">Per field index: the delimiter that follows the field, or <see langword="null"/>.</param>
    public static QuoteScanPlan Plan(ReadOnlySpan<char> delimiter, ReadOnlySpan<char> quotes, ReadOnlySpan<string?> trimStart, ReadOnlySpan<string?> fieldDelimiters)
    {
        foreach (var own in fieldDelimiters)
        {
            if (own is not null)
                return default;
        }

        char quote = '\0';
        bool skipsTrim = false;
        for (int i = 0; i < quotes.Length; i++)
        {
            if (quotes[i] == '\0')
                continue;
            if (quote != '\0' && quotes[i] != quote)
                return default;
            quote = quotes[i];
            skipsTrim |= i < trimStart.Length && trimStart[i] is not null;
        }

        if (quote == '\0' || delimiter.IsEmpty)
            return default;

        return new QuoteScanPlan(quote, Vectorized: delimiter.Length == 1 && delimiter[0] != quote && !skipsTrim);
    }

    /// <summary>
    /// <see cref="Scan"/> for one single-character delimiter, one quote character and no trim characters before
    /// opening quotes. It visits only the positions of the delimiter and the quote, found 8 or 16 characters at
    /// a time, and applies the rules of <see cref="Scan"/> there.
    /// </summary>
    private static int ScanSingleQuote(ReadOnlySpan<char> line, bool startsInsideQuotes, char delimiter, char quote, ReadOnlySpan<char> quotes, int field, out bool endsInside)
    {
        var walk = new QuoteWalk(field);
        var none = default(NoRecorder);
        if (startsInsideQuotes && QuoteOf(quotes, field) != '\0')
            walk.Inside = true;
        else
            walk.OpenAt(line, 0, quotes, ref none);

        int length = line.Length;
        ref ushort start = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(line));
        int position = 0;
        if (Vector256.IsHardwareAccelerated && length >= Vector256<ushort>.Count)
        {
            var delimiters = Vector256.Create((ushort)delimiter);
            var quoteChars = Vector256.Create((ushort)quote);
            for (; position <= length - Vector256<ushort>.Count; position += Vector256<ushort>.Count)
            {
                var chars = Vector256.LoadUnsafe(ref start, (nuint)position);
                uint mask = (Vector256.Equals(chars, delimiters) | Vector256.Equals(chars, quoteChars)).ExtractMostSignificantBits();
                while (mask != 0)
                {
                    int at = position + BitOperations.TrailingZeroCount(mask);
                    mask &= mask - 1;
                    walk.Visit(line, at, delimiter, quote, quotes, ref none);
                }
            }
        }

        if (Vector128.IsHardwareAccelerated && length - position >= Vector128<ushort>.Count)
        {
            var delimiters = Vector128.Create((ushort)delimiter);
            var quoteChars = Vector128.Create((ushort)quote);
            for (; position <= length - Vector128<ushort>.Count; position += Vector128<ushort>.Count)
            {
                var chars = Vector128.LoadUnsafe(ref start, (nuint)position);
                uint mask = (Vector128.Equals(chars, delimiters) | Vector128.Equals(chars, quoteChars)).ExtractMostSignificantBits();
                while (mask != 0)
                {
                    int at = position + BitOperations.TrailingZeroCount(mask);
                    mask &= mask - 1;
                    walk.Visit(line, at, delimiter, quote, quotes, ref none);
                }
            }
        }

        for (; position < length; position++)
        {
            char c = line[position];
            if (c == delimiter || c == quote)
                walk.Visit(line, position, delimiter, quote, quotes, ref none);
        }

        endsInside = walk.Inside;
        return walk.Field;
    }

    /// <summary>
    /// The state of <see cref="ScanSingleQuote"/> and <see cref="RecordTokenizer.TokenizeQuoted"/>, advanced at each
    /// delimiter or quote position in increasing order. The transitions read and write <see cref="Field"/>,
    /// <see cref="Inside"/> and <c>_next</c> only; they tell the recorder what they did and never read anything
    /// back from it, so the scan (<see cref="NoRecorder"/>) and the tokenizer (<see cref="TokenRecorder"/>) end in the
    /// same <c>(Field, Inside)</c> for the same line. The recorder is a type argument, so the JIT compiles the walk
    /// once per recorder and the scan's copy holds none of the recording code (T20, review D1).
    /// </summary>
    /// <param name="field">The field the walk starts in.</param>
    internal struct QuoteWalk(int field)
    {
        /// <summary>The field the walk is in.</summary>
        public int Field = field;

        /// <summary>Inside the quoted value of <see cref="Field"/>.</summary>
        public bool Inside;

        /// <summary>Positions before this one were already handled (an opening quote, the second quote of a pair).</summary>
        private int _next;

        /// <summary>A quoted field opens when its quote is the first character of the field.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OpenAt<TRecorder>(ReadOnlySpan<char> line, int fieldStart, ReadOnlySpan<char> quotes, ref TRecorder recorder)
            where TRecorder : struct, IWalkRecorder, allows ref struct
        {
            char q = QuoteOf(quotes, Field);
            if (q != '\0' && fieldStart < line.Length && line[fieldStart] == q)
            {
                Inside = true;
                _next = fieldStart + 1;
                recorder.Opened();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Visit<TRecorder>(ReadOnlySpan<char> line, int at, char delimiter, char quote, ReadOnlySpan<char> quotes, ref TRecorder recorder)
            where TRecorder : struct, IWalkRecorder, allows ref struct
        {
            if (at < _next)
                return;

            if (Inside)
            {
                // Delimiters inside a quoted value are text; a doubled quote is an escaped quote.
                if (line[at] != quote)
                    return;
                if (at + 1 < line.Length && line[at + 1] == quote)
                {
                    _next = at + 2;
                    recorder.Doubled();
                    return;
                }

                Inside = false;
                recorder.Closed(at);
                return;
            }

            // Outside a quoted value a quote is literal text: only a delimiter matters.
            if (line[at] != delimiter)
                return;
            recorder.End(Field, at);
            Field++;
            OpenAt(line, at + 1, quotes, ref recorder);
        }
    }

    /// <summary>What <see cref="QuoteWalk"/> tells its recorder; no member returns anything to the walk.</summary>
    internal interface IWalkRecorder
    {
        /// <summary>The current field opened a quoted value.</summary>
        void Opened();

        /// <summary>The quoted value has a doubled quote.</summary>
        void Doubled();

        /// <summary>The quoted value closed at <paramref name="at"/>.</summary>
        void Closed(int at);

        /// <summary>
        /// Field <paramref name="field"/> ends at <paramref name="at"/> (its delimiter). The walk calls it before it moves
        /// to the next field; the tokenizer calls it once at the record end, never inside an open quoted value.
        /// </summary>
        void End(int field, int at);
    }

    /// <summary>The recorder of the scan: records nothing.</summary>
    internal struct NoRecorder : IWalkRecorder
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Opened()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Doubled()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Closed(int at)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void End(int field, int at)
        {
        }
    }

    /// <summary>
    /// The recorder of the tokenizer: writes the end entry of each field into <paramref name="ends"/> while there is
    /// room (the <see cref="TokenBuffer"/> layout, flags included), and notes a quoted value followed by text.
    /// </summary>
    /// <param name="ends">The end entries; empty when the record is too long to tokenize.</param>
    internal ref struct TokenRecorder(Span<int> ends) : IWalkRecorder
    {
        private readonly Span<int> _ends = ends;

        /// <summary>The flags of the end entry of the current field: <see cref="TokenBuffer.QuotedBit"/>, <see cref="TokenBuffer.DoubledBit"/>.</summary>
        private int _entry;

        /// <summary>The position of the closing quote of the last quoted value.</summary>
        private int _closedAt;

        /// <summary>A quoted value is followed by text before its delimiter or the record end: no tokens.</summary>
        public bool Refused { get; private set; }

        /// <summary>A quoted value of the record has a doubled quote.</summary>
        public bool AnyDoubled { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Opened() => _entry = TokenBuffer.QuotedBit;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Doubled()
        {
            _entry |= TokenBuffer.DoubledBit;
            AnyDoubled = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Closed(int at) => _closedAt = at;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void End(int field, int at)
        {
            // A field with QuotedBit that is not inside its value any more left it through the close: _closedAt is its own.
            if (_entry != 0 && _closedAt + 1 != at)
                Refused = true;
            if ((uint)field < (uint)_ends.Length)
                _ends[field] = at | _entry;
            _entry = 0;
        }
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
