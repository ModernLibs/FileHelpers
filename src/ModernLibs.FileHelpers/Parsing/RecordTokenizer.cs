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
