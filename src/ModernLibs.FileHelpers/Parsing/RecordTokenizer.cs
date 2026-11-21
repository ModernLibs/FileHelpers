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
    /// The loop of <see cref="TokenizePlain(ReadOnlySpan{char}, char, Span{int}, bool)"/> with <c>vectors: true</c>,
    /// chosen once per process from the switch, the architecture and the vector widths the hardware accelerates (T21
    /// step 2; under Native AOT the row of the architecture). See <see cref="DefaultLoop"/> for the table.
    /// </summary>
    public static readonly TokenizerLoop PlainLoop = DefaultLoop();

    /// <summary>
    /// The walk loop of <see cref="TokenizeQuoted(ReadOnlySpan{char}, char, char, ReadOnlySpan{char}, TokenBuffer, bool, out int, out bool)"/>
    /// with <c>vectors: true</c>; same table as <see cref="PlainLoop"/>; the rounds <c>simd-2026-11-17</c> and <c>noavx2-2026-11-17</c> kept
    /// the same loop for both.
    /// </summary>
    public static readonly TokenizerLoop QuotedLoop = DefaultLoop();

    /// <summary>
    /// The block loops of the plain tokenizer load with <c>Vector*.LoadUnsafe</c> from a <c>ref</c> instead of
    /// bounds-checked span loads (freeze F-8). <see langword="false"/> on every runtime: round <c>simd-2026-11-17</c>
    /// measured both forms and found no dataset on which both VMs agree that the unsafe form is faster, on .NET 10 or
    /// on .NET 11 (finding F5), so it stays <see langword="false"/> (lead ruling P41).
    /// </summary>
    public static readonly bool UseUnsafeLoad;

    /// <summary>
    /// The loop of <see cref="PlainLoop"/> and <see cref="QuotedLoop"/> of 4.10.0. Each row names the decision of
    /// the rounds <c>simd-2026-11-17</c> and <c>noavx2-2026-11-17</c> (gate table of <c>docs/benchmarks/v4.10.0.md</c>).
    /// </summary>
    /// <returns>
    /// <list type="bullet">
    /// <item><c>DisableVectorizedTokenizer</c>: <see cref="TokenizerLoop.IndexOfHop"/>.</item>
    /// <item>No <c>Vector128.IsHardwareAccelerated</c> (for example <c>DOTNET_EnableHWIntrinsic=0</c>):
    /// <see cref="TokenizerLoop.IndexOfHop"/>; there is no vector loop to run.</item>
    /// <item>Not x64 (Arm64 and any other architecture): <see cref="TokenizerLoop.IndexOfHop"/>. Arm64 was not measured
    /// (owner waiver of 2026-11-16, branch B); the existing 4.9 scan <c>QuoteBalanceScanner.ScanSingleQuote</c> keeps
    /// its vector code there.</item>
    /// <item>x64 with <c>Vector256.IsHardwareAccelerated</c> (AVX2), also with AVX-512: <see cref="TokenizerLoop.Vector256"/>.
    /// Round <c>simd-2026-11-17</c> kept it on <c>fh-amd</c> and <c>fh-intel</c>. The <c>Vector512</c> gate passed on
    /// <c>fh-intel</c> (long lines), but the owner deferred <c>Vector512</c> to 4.10.1 or 4.11 (2026-11-21). Under
    /// Native AOT on .NET 10 the round measured this loop 1.172 times slower than <see cref="TokenizerLoop.IndexOfHop"/>
    /// on <c>fh-amd</c>; the owner deferred that change to 4.10.1 or 4.11 as well (2026-11-21).</item>
    /// <item>x64 without AVX2: <see cref="TokenizerLoop.Vector128"/>. Round <c>noavx2-2026-11-17</c> (<c>fh-amd</c> with
    /// <c>DOTNET_EnableAVX2=0</c>) found it slower than <see cref="TokenizerLoop.IndexOfHop"/> on long lines; the owner
    /// kept <c>Vector128</c> (2026-11-21).</item>
    /// </list>
    /// </returns>
    private static TokenizerLoop DefaultLoop()
    {
        if (RuntimeSwitches.DisableVectorizedTokenizer || !Vector128.IsHardwareAccelerated)
            return TokenizerLoop.IndexOfHop;
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            return TokenizerLoop.IndexOfHop;
        return Vector256.IsHardwareAccelerated ? TokenizerLoop.Vector256 : TokenizerLoop.Vector128;
    }

    /// <summary>The loop the <c>bool vectors</c> overload of <c>TokenizePlain</c> runs: <see cref="PlainLoop"/>, or <see cref="TokenizerLoop.IndexOfHop"/>.</summary>
    /// <param name="vectors">As for <see cref="TokenizePlain(ReadOnlySpan{char}, char, Span{int}, bool)"/>.</param>
    /// <returns>The loop.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TokenizerLoop PlainLoopFor(bool vectors) => vectors ? PlainLoop : TokenizerLoop.IndexOfHop;

    /// <summary>The walk loop the <c>bool vectors</c> overload of <c>TokenizeQuoted</c> runs: <see cref="QuotedLoop"/>, or <see cref="TokenizerLoop.IndexOfHop"/>.</summary>
    /// <param name="vectors">As for <see cref="TokenizePlain(ReadOnlySpan{char}, char, Span{int}, bool)"/>.</param>
    /// <returns>The loop.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TokenizerLoop QuotedLoopFor(bool vectors) => vectors ? QuotedLoop : TokenizerLoop.IndexOfHop;

    /// <summary>
    /// Writes the end of each field of a record without quoted fields into <paramref name="ends"/>: the offset of
    /// the delimiter after it, or the record length for the last field. Never throws.
    /// </summary>
    /// <param name="record">The record text.</param>
    /// <param name="delimiter">The record delimiter.</param>
    /// <param name="ends">The end entries; the runtime passes <c>FieldCount + 1</c> of them, so one extra field is detectable.</param>
    /// <param name="vectors">
    /// Run <see cref="PlainLoop"/> (with <see cref="UseUnsafeLoad"/>). <see langword="false"/> (the
    /// <c>DisableVectorizedTokenizer</c> switch, <c>EnginePaths.ScalarTokenizer</c>): <see cref="TokenizerLoop.IndexOfHop"/>,
    /// one <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, T)"/> per field, which the BCL may vectorize (freeze F-7, F-9).
    /// </param>
    /// <returns>The fields found, at most <c>ends.Length</c>; <c>ends.Length</c> also when the record has more.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TokenizePlain(ReadOnlySpan<char> record, char delimiter, Span<int> ends, bool vectors) =>
        TokenizePlain(record, delimiter, ends, PlainLoopFor(vectors), UseUnsafeLoad);

    /// <summary>
    /// <see cref="TokenizePlain(ReadOnlySpan{char}, char, Span{int}, bool)"/> with the loop and the load form named
    /// (T21: the entry of the tests and the benchmarks, so each variant they measure is the shipped code). A vector
    /// loop runs whether or not the hardware accelerates its width; the result is the same, only slower.
    /// </summary>
    /// <param name="record">The record text.</param>
    /// <param name="delimiter">The record delimiter.</param>
    /// <param name="ends">The end entries.</param>
    /// <param name="loop">The loop; a value outside <see cref="TokenizerLoop"/> runs <see cref="TokenizerLoop.Vector256"/>.</param>
    /// <param name="unsafeLoad">Load the blocks with <c>LoadUnsafe</c> instead of bounds-checked span loads (freeze F-8).</param>
    /// <returns>The fields found, at most <c>ends.Length</c>; <c>ends.Length</c> also when the record has more.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TokenizePlain(ReadOnlySpan<char> record, char delimiter, Span<int> ends, TokenizerLoop loop, bool unsafeLoad)
    {
        if (ends.IsEmpty)
            return 0;
        if (loop == TokenizerLoop.IndexOfHop)
            return TokenizeIndexOf(record, delimiter, ends);

        // Vector128/256/512 do not take char, so one MemoryMarshal.Cast reinterprets the record as ushort before the
        // loop (lead ruling on T15); the cast keeps the bounds. The load form is a type argument, so each loop is
        // compiled once per form with no test inside it.
        ReadOnlySpan<ushort> units = MemoryMarshal.Cast<char, ushort>(record);
        return unsafeLoad
            ? Blocks<UnsafeLoad>(units, delimiter, ends, loop)
            : Blocks<SpanLoad>(units, delimiter, ends, loop);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Blocks<TLoad>(ReadOnlySpan<ushort> units, char delimiter, Span<int> ends, TokenizerLoop loop)
        where TLoad : struct, IBlockLoad => loop switch
        {
            TokenizerLoop.Vector128 => Blocks128<TLoad>(units, delimiter, ends, 0, 0),
            TokenizerLoop.Vector512 => Blocks512<TLoad>(units, delimiter, ends),
            TokenizerLoop.Vector256Packed => BlocksPacked<TLoad>(units, delimiter, ends),
            _ => Blocks256<TLoad>(units, delimiter, ends, 0, 0),
        };

    /// <summary><see cref="TokenizerLoop.Vector512"/>: blocks of 32 chars, then <see cref="Blocks256{TLoad}"/>.</summary>
    private static int Blocks512<TLoad>(ReadOnlySpan<ushort> units, char delimiter, Span<int> ends)
        where TLoad : struct, IBlockLoad
    {
        int length = units.Length;
        int position = 0;
        int n = 0;
        if (length >= Vector512<ushort>.Count)
        {
            var delimiters = Vector512.Create((ushort)delimiter);
            for (; position <= length - Vector512<ushort>.Count; position += Vector512<ushort>.Count)
            {
                // 32 lanes: the mask fits in 32 bits.
                uint mask = (uint)Vector512.Equals(TLoad.Load512(units, position), delimiters).ExtractMostSignificantBits();
                n = WriteEnds(mask, position, ends, n);
                if (n == ends.Length)
                    return n;
            }
        }

        return Blocks256<TLoad>(units, delimiter, ends, position, n);
    }

    /// <summary>
    /// <see cref="TokenizerLoop.Vector256Packed"/>: per 32 chars, the low bytes of two <c>Vector256</c> loads
    /// (<c>Vector256.Narrow</c> keeps the low byte of each char) are compared with the delimiter's low byte. A
    /// delimiter always matches there; another char with the same low byte (U+012C, U+2C2C for a comma) does too, and
    /// the exact 16-bit compares after it drop that one. Then <see cref="Blocks256{TLoad}"/> for the rest.
    /// </summary>
    private static int BlocksPacked<TLoad>(ReadOnlySpan<ushort> units, char delimiter, Span<int> ends)
        where TLoad : struct, IBlockLoad
    {
        int length = units.Length;
        int position = 0;
        int n = 0;
        if (length >= 2 * Vector256<ushort>.Count)
        {
            var delimiters = Vector256.Create((ushort)delimiter);
            var lowBytes = Vector256.Create((byte)delimiter);
            for (; position <= length - (2 * Vector256<ushort>.Count); position += 2 * Vector256<ushort>.Count)
            {
                var first = TLoad.Load256(units, position);
                var second = TLoad.Load256(units, position + Vector256<ushort>.Count);
                if (!Vector256.EqualsAny(Vector256.Narrow(first, second), lowBytes))
                    continue;
                n = WriteEnds(Vector256.Equals(first, delimiters).ExtractMostSignificantBits(), position, ends, n);
                if (n == ends.Length)
                    return n;
                n = WriteEnds(Vector256.Equals(second, delimiters).ExtractMostSignificantBits(), position + Vector256<ushort>.Count, ends, n);
                if (n == ends.Length)
                    return n;
            }
        }

        return Blocks256<TLoad>(units, delimiter, ends, position, n);
    }

    /// <summary>
    /// <see cref="TokenizerLoop.Vector256"/>, from <paramref name="position"/> with <paramref name="n"/> entries written:
    /// blocks of 16 chars, then <see cref="Blocks128{TLoad}"/>.
    /// </summary>
    private static int Blocks256<TLoad>(ReadOnlySpan<ushort> units, char delimiter, Span<int> ends, int position, int n)
        where TLoad : struct, IBlockLoad
    {
        int length = units.Length;
        if (length - position >= Vector256<ushort>.Count)
        {
            var delimiters = Vector256.Create((ushort)delimiter);
            for (; position <= length - Vector256<ushort>.Count; position += Vector256<ushort>.Count)
            {
                uint mask = Vector256.Equals(TLoad.Load256(units, position), delimiters).ExtractMostSignificantBits();
                n = WriteEnds(mask, position, ends, n);
                if (n == ends.Length)
                    return n;
            }
        }

        return Blocks128<TLoad>(units, delimiter, ends, position, n);
    }

    /// <summary>
    /// <see cref="TokenizerLoop.Vector128"/>, from <paramref name="position"/> with <paramref name="n"/> entries written:
    /// blocks of 8 chars, then the scalar tail and the end of the last field.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Blocks128<TLoad>(ReadOnlySpan<ushort> units, char delimiter, Span<int> ends, int position, int n)
        where TLoad : struct, IBlockLoad
    {
        int length = units.Length;
        if (length - position >= Vector128<ushort>.Count)
        {
            var delimiters = Vector128.Create((ushort)delimiter);
            for (; position <= length - Vector128<ushort>.Count; position += Vector128<ushort>.Count)
            {
                uint mask = Vector128.Equals(TLoad.Load128(units, position), delimiters).ExtractMostSignificantBits();
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
    /// <param name="vectors">
    /// Walk with <see cref="QuotedLoop"/>, and tokenize a record without the quote char as
    /// <see cref="TokenizePlain(ReadOnlySpan{char}, char, Span{int}, bool)"/> does; <see langword="false"/>: one
    /// <c>IndexOfAny</c> per delimiter or quote position.
    /// </param>
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
        TokenBuffer buffer, bool vectors, out int lastField, out bool endsInside) =>
        TokenizeQuotedCore(record, delimiter, quote, quotes, buffer, PlainLoopFor(vectors), QuotedLoopFor(vectors), out lastField, out endsInside);

    /// <summary>
    /// <see cref="TokenizeQuoted(ReadOnlySpan{char}, char, char, ReadOnlySpan{char}, TokenBuffer, bool, out int, out bool)"/>
    /// with one loop named for the walk and for a record without the quote char (T21: the entry of the tests and the
    /// benchmarks). <see cref="TokenizerLoop.Vector512"/> and <see cref="TokenizerLoop.Vector256Packed"/> walk in
    /// <see cref="TokenizerLoop.Vector256"/> blocks; the load form is <see cref="UseUnsafeLoad"/>.
    /// </summary>
    /// <param name="record">The record text.</param>
    /// <param name="delimiter">The record delimiter (not <paramref name="quote"/>).</param>
    /// <param name="quote">The quote char of every quoted field.</param>
    /// <param name="quotes">Per field index: <paramref name="quote"/> for a quoted field, <c>'\0'</c> otherwise.</param>
    /// <param name="buffer">The buffer of the record type.</param>
    /// <param name="loop">The loop.</param>
    /// <param name="lastField">As for the <c>bool vectors</c> overload.</param>
    /// <param name="endsInside">As for the <c>bool vectors</c> overload.</param>
    /// <returns>As for the <c>bool vectors</c> overload.</returns>
    public static int TokenizeQuoted(ReadOnlySpan<char> record, char delimiter, char quote, ReadOnlySpan<char> quotes,
        TokenBuffer buffer, TokenizerLoop loop, out int lastField, out bool endsInside) =>
        TokenizeQuotedCore(record, delimiter, quote, quotes, buffer, loop, loop, out lastField, out endsInside);

    private static int TokenizeQuotedCore(ReadOnlySpan<char> record, char delimiter, char quote, ReadOnlySpan<char> quotes,
        TokenBuffer buffer, TokenizerLoop plainLoop, TokenizerLoop walkLoop, out int lastField, out bool endsInside)
    {
        bool fits = record.Length <= TokenBuffer.MaxRecordLength;
        Span<int> ends = fits ? buffer.Ends : default;

        // No quote char: no quoted value, so the plain tokenizer writes the same entries (the same shortcut as
        // QuoteBalanceScanner.EndsInsideQuotes with a plan, which counts the delimiters there).
        if (fits && !record.Contains(quote))
        {
            int n = TokenizePlain(record, delimiter, ends, plainLoop, UseUnsafeLoad);
            lastField = n < ends.Length ? n - 1 : record.Count(delimiter);
            endsInside = false;
            return n;
        }

        var walk = new QuoteBalanceScanner.QuoteWalk(0);
        var recorder = new QuoteBalanceScanner.TokenRecorder(ends);
        walk.OpenAt(record, 0, quotes, ref recorder);
        if (walkLoop == TokenizerLoop.IndexOfHop)
            WalkIndexOfAny(record, delimiter, quote, quotes, ref walk, ref recorder);
        else
            WalkBlocks(record, delimiter, quote, quotes, wide: walkLoop != TokenizerLoop.Vector128, ref walk, ref recorder);

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
    /// <c>Vector256</c> (when <paramref name="wide"/>), then of <c>Vector128</c>, with bounds-checked span loads
    /// (freeze F-8), then a scalar tail. No early exit: the walk counts every field.
    /// </summary>
    private static void WalkBlocks(ReadOnlySpan<char> record, char delimiter, char quote, ReadOnlySpan<char> quotes, bool wide, ref QuoteBalanceScanner.QuoteWalk walk, ref QuoteBalanceScanner.TokenRecorder recorder)
    {
        ReadOnlySpan<ushort> units = MemoryMarshal.Cast<char, ushort>(record);
        int length = units.Length;
        int position = 0;
        if (wide && length >= Vector256<ushort>.Count)
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

    /// <summary>The load form of the plain block loops (freeze F-8): a type argument, so the JIT compiles each form apart.</summary>
    private interface IBlockLoad
    {
        static abstract Vector128<ushort> Load128(ReadOnlySpan<ushort> units, int position);

        static abstract Vector256<ushort> Load256(ReadOnlySpan<ushort> units, int position);

        static abstract Vector512<ushort> Load512(ReadOnlySpan<ushort> units, int position);
    }

    /// <summary>Bounds-checked span loads, <c>Vector*.Create(units.Slice(position))</c> (T15, design 2.5 item 1).</summary>
    private readonly struct SpanLoad : IBlockLoad
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Load128(ReadOnlySpan<ushort> units, int position) => Vector128.Create(units.Slice(position));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Load256(ReadOnlySpan<ushort> units, int position) => Vector256.Create(units.Slice(position));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Load512(ReadOnlySpan<ushort> units, int position) => Vector512.Create(units.Slice(position));
    }

    /// <summary>
    /// <c>Vector*.LoadUnsafe</c> from the first unit, no bounds check (<see cref="UseUnsafeLoad"/>): every loop keeps
    /// <c>position + Count &lt;= units.Length</c>.
    /// </summary>
    private readonly struct UnsafeLoad : IBlockLoad
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Load128(ReadOnlySpan<ushort> units, int position) =>
            Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(units), (nuint)position);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Load256(ReadOnlySpan<ushort> units, int position) =>
            Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(units), (nuint)position);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Load512(ReadOnlySpan<ushort> units, int position) =>
            Vector512.LoadUnsafe(ref MemoryMarshal.GetReference(units), (nuint)position);
    }
}
