using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// Finds the field ends of a record for <see cref="RecordTokens"/> (design 4.5, "Tokenizer implementation notes";
/// internal, may change in any patch).
/// </summary>
internal static class RecordTokenizer
{
    /// <summary>
    /// Writes the end of each field of a record without quoted fields into <paramref name="ends"/>: the offset of
    /// the delimiter after it, or the record length for the last field. Never throws.
    /// </summary>
    /// <param name="record">The record text.</param>
    /// <param name="delimiter">The record delimiter.</param>
    /// <param name="ends">The end entries; the runtime passes <c>FieldCount + 1</c> of them, so one extra field is detectable.</param>
    /// <param name="vectors">
    /// Use the vector loop when the hardware accelerates <c>Vector128</c>. <see langword="false"/> (the
    /// <c>DisableVectorizedTokenizer</c> switch, <c>EnginePaths.ScalarTokenizer</c>): one
    /// <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, T)"/> per field, which the BCL may vectorize (freeze F-7, F-9).
    /// </param>
    /// <returns>The fields found, at most <c>ends.Length</c>; <c>ends.Length</c> also when the record has more.</returns>
    public static int TokenizePlain(ReadOnlySpan<char> record, char delimiter, Span<int> ends, bool vectors)
    {
        if (ends.IsEmpty)
            return 0;
        if (!vectors || !Vector128.IsHardwareAccelerated)
            return TokenizeIndexOf(record, delimiter, ends);

        // Bounds-checked span loads (freeze F-8). Vector128/256 do not take char, so one MemoryMarshal.Cast reinterprets
        // the record as ushort before the loop (lead ruling on T15: the only MemoryMarshal call; no Unsafe, no
        // GetReference, no LoadUnsafe, which is T21's UseUnsafeLoad variant). The cast keeps the bounds.
        ReadOnlySpan<ushort> units = MemoryMarshal.Cast<char, ushort>(record);
        int length = units.Length;
        int position = 0;
        int n = 0;
        if (Vector256.IsHardwareAccelerated && length >= Vector256<ushort>.Count)
        {
            var delimiters = Vector256.Create((ushort)delimiter);
            for (; position <= length - Vector256<ushort>.Count; position += Vector256<ushort>.Count)
            {
                uint mask = Vector256.Equals(Vector256.Create(units.Slice(position)), delimiters).ExtractMostSignificantBits();
                n = WriteEnds(mask, position, ends, n);
                if (n == ends.Length)
                    return n;
            }
        }

        if (length - position >= Vector128<ushort>.Count)
        {
            var delimiters = Vector128.Create((ushort)delimiter);
            for (; position <= length - Vector128<ushort>.Count; position += Vector128<ushort>.Count)
            {
                uint mask = Vector128.Equals(Vector128.Create(units.Slice(position)), delimiters).ExtractMostSignificantBits();
                n = WriteEnds(mask, position, ends, n);
                if (n == ends.Length)
                    return n;
            }
        }

        // The tail after the last block, and records shorter than one vector: scalar.
        for (; position < length; position++)
        {
            if (units[position] != delimiter)
                continue;
            ends[n++] = position;
            if (n == ends.Length)
                return n;
        }

        ends[n++] = length;
        return n;
    }

    /// <summary>
    /// Writes the end entry of each field of a record whose quoted fields all use <paramref name="quote"/> into
    /// <paramref name="buffer"/> (<see cref="TokenBuffer.QuotedBit"/> and <see cref="TokenBuffer.DoubledBit"/> set as
    /// found), with the one walk of <see cref="QuoteBalanceScanner"/> (<c>QuoteWalk</c>), and calls
    /// <see cref="TokenBuffer.EnsureScratch"/> with the record length when a value has a doubled quote. Never throws.
    /// </summary>
    /// <param name="record">The record text.</param>
    /// <param name="delimiter">The record delimiter (not <paramref name="quote"/>).</param>
    /// <param name="quote">The quote char of every quoted field.</param>
    /// <param name="quotes">Per field index: <paramref name="quote"/> for a quoted field, <c>'\0'</c> otherwise; fields past the end are not quoted.</param>
    /// <param name="buffer">The buffer of the record type.</param>
    /// <param name="vectors">As for <see cref="TokenizePlain"/>; <see langword="false"/>: one <c>IndexOfAny</c> per delimiter or quote position.</param>
    /// <param name="lastField">
    /// The field the record ends in, counted over the whole record (also past the capacity, and when the result is
    /// -1): always what <see cref="QuoteBalanceScanner.EndsInsideQuotes(ReadOnlySpan{char}, bool, ReadOnlySpan{char}, ReadOnlySpan{char}, ReadOnlySpan{string?}, ref int, ReadOnlySpan{string?})"/>
    /// gives for the same rules.
    /// </param>
    /// <param name="endsInside">The record ends inside an open quoted value; same source as <paramref name="lastField"/>.</param>
    /// <returns>
    /// -1 outside the success shape ("no tokens": a quote that never closes, text between a closing quote and the
    /// next delimiter or the record end, a record longer than <see cref="TokenBuffer.MaxRecordLength"/>); otherwise
    /// the fields found, at most <c>buffer.Ends.Length</c>.
    /// </returns>
    public static int TokenizeQuoted(ReadOnlySpan<char> record, char delimiter, char quote, ReadOnlySpan<char> quotes,
        TokenBuffer buffer, bool vectors, out int lastField, out bool endsInside)
    {
        bool fits = record.Length <= TokenBuffer.MaxRecordLength;
        Span<int> ends = fits ? buffer.Ends : default;

        // No quote char: no quoted value, so the plain tokenizer writes the same entries (the same shortcut as
        // QuoteBalanceScanner.EndsInsideQuotes with a plan, which counts the delimiters there).
        if (fits && !record.Contains(quote))
        {
            int n = TokenizePlain(record, delimiter, ends, vectors);
            lastField = n < ends.Length ? n - 1 : record.Count(delimiter);
            endsInside = false;
            return n;
        }

        var walk = new QuoteBalanceScanner.QuoteWalk(0);
        var recorder = new QuoteBalanceScanner.TokenRecorder(ends);
        walk.OpenAt(record, 0, quotes, ref recorder);
        if (vectors && Vector128.IsHardwareAccelerated)
            WalkBlocks(record, delimiter, quote, quotes, ref walk, ref recorder);
        else
            WalkIndexOfAny(record, delimiter, quote, quotes, ref walk, ref recorder);

        // The last field ends at the record end; an open quoted value has no end (the result is -1 anyway).
        if (!walk.Inside)
            recorder.End(walk.Field, record.Length);

        lastField = walk.Field;
        endsInside = walk.Inside;
        if (!fits || walk.Inside || recorder.Refused)
            return -1;
        if (recorder.AnyDoubled)
            buffer.EnsureScratch(record.Length);
        return Math.Min(walk.Field + 1, ends.Length);
    }

    /// <summary>
    /// Visits every delimiter and quote position of <paramref name="record"/> in increasing order: blocks of
    /// <c>Vector256</c> or <c>Vector128</c> with bounds-checked span loads (freeze F-8), then a scalar tail. No early
    /// exit: the walk counts every field.
    /// </summary>
    private static void WalkBlocks(ReadOnlySpan<char> record, char delimiter, char quote, ReadOnlySpan<char> quotes, ref QuoteBalanceScanner.QuoteWalk walk, ref QuoteBalanceScanner.TokenRecorder recorder)
    {
        ReadOnlySpan<ushort> units = MemoryMarshal.Cast<char, ushort>(record);
        int length = units.Length;
        int position = 0;
        if (Vector256.IsHardwareAccelerated && length >= Vector256<ushort>.Count)
        {
            var delimiters = Vector256.Create((ushort)delimiter);
            var quoteChars = Vector256.Create((ushort)quote);
            for (; position <= length - Vector256<ushort>.Count; position += Vector256<ushort>.Count)
            {
                var chars = Vector256.Create(units.Slice(position));
                uint mask = (Vector256.Equals(chars, delimiters) | Vector256.Equals(chars, quoteChars)).ExtractMostSignificantBits();
                while (mask != 0)
                {
                    walk.Visit(record, position + BitOperations.TrailingZeroCount(mask), delimiter, quote, quotes, ref recorder);
                    mask &= mask - 1;
                }
            }
        }

        if (length - position >= Vector128<ushort>.Count)
        {
            var delimiters = Vector128.Create((ushort)delimiter);
            var quoteChars = Vector128.Create((ushort)quote);
            for (; position <= length - Vector128<ushort>.Count; position += Vector128<ushort>.Count)
            {
                var chars = Vector128.Create(units.Slice(position));
                uint mask = (Vector128.Equals(chars, delimiters) | Vector128.Equals(chars, quoteChars)).ExtractMostSignificantBits();
                while (mask != 0)
                {
                    walk.Visit(record, position + BitOperations.TrailingZeroCount(mask), delimiter, quote, quotes, ref recorder);
                    mask &= mask - 1;
                }
            }
        }

        for (; position < length; position++)
        {
            char c = record[position];
            if (c == delimiter || c == quote)
                walk.Visit(record, position, delimiter, quote, quotes, ref recorder);
        }
    }

    /// <summary>Without our vector code: one <c>IndexOfAny</c> per delimiter or quote position (freeze F-7, F-9).</summary>
    private static void WalkIndexOfAny(ReadOnlySpan<char> record, char delimiter, char quote, ReadOnlySpan<char> quotes, ref QuoteBalanceScanner.QuoteWalk walk, ref QuoteBalanceScanner.TokenRecorder recorder)
    {
        int position = 0;
        while (true)
        {
            int at = record.Slice(position).IndexOfAny(delimiter, quote);
            if (at < 0)
                return;
            walk.Visit(record, position + at, delimiter, quote, quotes, ref recorder);
            position += at + 1;
        }
    }

    /// <summary>
    /// The bit walk of one block: writes the offset of each set bit of <paramref name="mask"/> (block start
    /// <paramref name="position"/>) after entry <paramref name="n"/>, while there is room; returns the new count.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteEnds(uint mask, int position, Span<int> ends, int n)
    {
        while (mask != 0 && (uint)n < (uint)ends.Length)
        {
            ends[n++] = position + BitOperations.TrailingZeroCount(mask);
            mask &= mask - 1;
        }

        return n;
    }

    /// <summary>One <c>IndexOf</c> per field: the 4.9 work without the <c>Count</c> pass (freeze F-7).</summary>
    private static int TokenizeIndexOf(ReadOnlySpan<char> record, char delimiter, Span<int> ends)
    {
        int n = 0;
        int position = 0;
        while ((uint)n < (uint)ends.Length)
        {
            int at = record.Slice(position).IndexOf(delimiter);
            if (at < 0)
            {
                ends[n++] = record.Length;
                return n;
            }

            ends[n++] = position + at;
            position += at + 1;
        }

        return n;
    }
}
