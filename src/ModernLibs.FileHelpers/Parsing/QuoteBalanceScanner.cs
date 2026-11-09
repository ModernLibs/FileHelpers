using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

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
                if (delimiter[0] != '\0' && TryScanMasked(line, startsInsideQuotes, delimiter[0], plan.Quote, quotes, ref field, out bool masked))
                    return masked;
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
    /// <see cref="Scan"/> for one single-character delimiter (not <c>'\0'</c>), one quote character and no trim
    /// characters before opening quotes, 64 characters at a time without visiting each delimiter: the quote
    /// characters toggle the quote state, so the prefix XOR of the quote mask is the mask of the positions
    /// inside quoted values, and the delimiters outside it end fields. A doubled quote toggles twice and
    /// stays inside. That holds when every quote opens a value at the start of a quoted field, closes one, or
    /// escapes the quote before it; returns <see langword="false"/> (and leaves <paramref name="field"/>)
    /// when a quote anywhere else is literal text under the 3.x rules, for <see cref="ScanSingleQuote"/>.
    /// </summary>
    [SkipLocalsInit]
    internal static bool TryScanMasked(ReadOnlySpan<char> line, bool startsInsideQuotes, char delimiter, char quote, ReadOnlySpan<char> quotes, ref int field, out bool endsInside)
    {
        var state = new MaskState(field, startsInsideQuotes && QuoteOf(quotes, field) != '\0');
        Span<char> tail = stackalloc char[64];
        int length = line.Length;
        for (int start = 0; start < length; start += 64)
        {
            int count = Math.Min(64, length - start);
            scoped ReadOnlySpan<char> block;
            if (count == 64)
            {
                block = line.Slice(start, 64);
            }
            else
            {
                // Padding with '\0' adds no quote and no delimiter.
                line.Slice(start, count).CopyTo(tail);
                tail.Slice(count).Clear();
                block = tail;
            }

            var (quoteBits, delimiterBits) = Masks(block, quote, delimiter);
            if (!state.Advance(quoteBits, delimiterBits, quotes))
            {
                endsInside = false;
                return false;
            }
        }

        field = state.Field;
        endsInside = state.Inside;
        return true;
    }

    /// <summary>The quote state of <see cref="TryScanMasked"/> between two 64-character blocks.</summary>
    private struct MaskState(int field, bool inside)
    {
        /// <summary>The field the scan is in.</summary>
        public int Field = field;

        /// <summary>Inside a quoted value after the last block.</summary>
        public bool Inside = inside;

        // Bit 0 of a block sees the last character of the block before: a delimiter outside quotes (the line
        // start outside a quoted value counts as one) or a closing quote.
        private ulong _carryFieldStart = inside ? 0UL : 1UL;
        private ulong _carryClose;

        /// <summary>Applies one block; <see langword="false"/> when one of its quotes is literal text under the 3.x rules.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Advance(ulong quoteBits, ulong delimiterBits, ReadOnlySpan<char> quotes)
        {
            ulong insideBits = PrefixXor(quoteBits) ^ (Inside ? ulong.MaxValue : 0UL);
            ulong opens = quoteBits & insideBits;
            ulong closes = quoteBits & ~insideBits;
            ulong fieldEnds = delimiterBits & ~insideBits;
            ulong fieldStarts = (fieldEnds << 1) | _carryFieldStart;
            ulong afterClose = (closes << 1) | _carryClose;

            // An opening toggle is an opening quote at a field start or the second quote of a doubled quote;
            // anywhere else the quote is literal text, which the toggles do not model.
            if ((opens & ~(fieldStarts | afterClose)) != 0)
                return false;

            // A quote at the start of a field opens a value only when the field is quoted.
            ulong openings = opens & fieldStarts;
            while (openings != 0)
            {
                int at = BitOperations.TrailingZeroCount(openings);
                openings &= openings - 1;
                if (QuoteOf(quotes, Field + BitOperations.PopCount(fieldEnds & ((1UL << at) - 1))) == '\0')
                    return false;
            }

            Field += BitOperations.PopCount(fieldEnds);
            Inside = (insideBits >> 63) != 0;
            _carryFieldStart = fieldEnds >> 63;
            _carryClose = closes >> 63;
            return true;
        }
    }

    /// <summary>Bit <c>i</c>: the XOR of bits 0 to <c>i</c> of <paramref name="bits"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong PrefixXor(ulong bits)
    {
        if (Pclmulqdq.IsSupported)
            return Pclmulqdq.CarrylessMultiply(Vector128.CreateScalar(bits), Vector128.Create(ulong.MaxValue), 0).ToScalar();

        bits ^= bits << 1;
        bits ^= bits << 2;
        bits ^= bits << 4;
        bits ^= bits << 8;
        bits ^= bits << 16;
        bits ^= bits << 32;
        return bits;
    }

    /// <summary>The positions of <paramref name="quote"/> and <paramref name="delimiter"/> in a 64-character block.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (ulong Quotes, ulong Delimiters) Masks(ReadOnlySpan<char> block, char quote, char delimiter)
    {
        ref ushort first = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(block));
        ulong quoteBits = 0, delimiterBits = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            var quotes = Vector256.Create((ushort)quote);
            var delimiters = Vector256.Create((ushort)delimiter);
            for (int k = 0; k < 64; k += Vector256<ushort>.Count)
            {
                var chars = Vector256.LoadUnsafe(ref first, (nuint)k);
                quoteBits |= (ulong)Vector256.Equals(chars, quotes).ExtractMostSignificantBits() << k;
                delimiterBits |= (ulong)Vector256.Equals(chars, delimiters).ExtractMostSignificantBits() << k;
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            var quotes = Vector128.Create((ushort)quote);
            var delimiters = Vector128.Create((ushort)delimiter);
            for (int k = 0; k < 64; k += Vector128<ushort>.Count)
            {
                var chars = Vector128.LoadUnsafe(ref first, (nuint)k);
                quoteBits |= (ulong)Vector128.Equals(chars, quotes).ExtractMostSignificantBits() << k;
                delimiterBits |= (ulong)Vector128.Equals(chars, delimiters).ExtractMostSignificantBits() << k;
            }
        }
        else
        {
            for (int k = 0; k < 64; k++)
            {
                char c = block[k];
                quoteBits |= (c == quote ? 1UL : 0UL) << k;
                delimiterBits |= (c == delimiter ? 1UL : 0UL) << k;
            }
        }

        return (quoteBits, delimiterBits);
    }

    /// <summary>
    /// <see cref="Scan"/> for one single-character delimiter, one quote character and no trim characters before
    /// opening quotes. It visits only the positions of the delimiter and the quote, found 8 or 16 characters at
    /// a time, and applies the rules of <see cref="Scan"/> there.
    /// </summary>
    private static int ScanSingleQuote(ReadOnlySpan<char> line, bool startsInsideQuotes, char delimiter, char quote, ReadOnlySpan<char> quotes, int field, out bool endsInside)
    {
        var walk = new QuoteWalk(field);
        if (startsInsideQuotes && QuoteOf(quotes, field) != '\0')
            walk.Inside = true;
        else
            walk.OpenAt(line, 0, quotes);

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
                    walk.Visit(line, at, delimiter, quote, quotes);
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
                    walk.Visit(line, at, delimiter, quote, quotes);
                }
            }
        }

        for (; position < length; position++)
        {
            char c = line[position];
            if (c == delimiter || c == quote)
                walk.Visit(line, position, delimiter, quote, quotes);
        }

        endsInside = walk.Inside;
        return walk.Field;
    }

    /// <summary>The state of <see cref="ScanSingleQuote"/>, advanced at each delimiter or quote position.</summary>
    private struct QuoteWalk(int field)
    {
        /// <summary>The field the scan is in.</summary>
        public int Field = field;

        /// <summary>Inside the quoted value of <see cref="Field"/>.</summary>
        public bool Inside;

        /// <summary>Positions before this one were already handled (an opening quote, the second quote of a pair).</summary>
        private int _next;

        /// <summary>A quoted field opens when its quote is the first character of the field.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OpenAt(ReadOnlySpan<char> line, int fieldStart, ReadOnlySpan<char> quotes)
        {
            char q = QuoteOf(quotes, Field);
            if (q != '\0' && fieldStart < line.Length && line[fieldStart] == q)
            {
                Inside = true;
                _next = fieldStart + 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Visit(ReadOnlySpan<char> line, int at, char delimiter, char quote, ReadOnlySpan<char> quotes)
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
                    return;
                }

                Inside = false;
                return;
            }

            // Outside a quoted value a quote is literal text: only a delimiter matters.
            if (line[at] != delimiter)
                return;
            Field++;
            OpenAt(line, at + 1, quotes);
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
