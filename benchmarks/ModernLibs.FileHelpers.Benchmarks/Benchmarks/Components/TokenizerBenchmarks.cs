// MA0051: the vector loops are kept in one method each, as the library's scanner writes them.
#pragma warning disable MA0051

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;

/// <summary>
/// Plain-record tokenizers (design B3, G1/R5/R7, 2.5 item 3): the field ends of every line written into one
/// preallocated <c>int[]</c>, on the 25-column, the 150-column and the long-line sets.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Tokenizer_CountPlusIndexOf"/> is today's work for a record without quoted fields: one
/// <c>Count(',')</c> for the field-count check (<c>RecordReadCore.ValidateFieldCount</c>, <c>RecordReadCore.cs:628</c>)
/// and then N - 1 sequential <c>IndexOf(',')</c> in the generated parser (<c>DescriptorEmitter.cs:788</c>). The
/// generated parser is unrolled per field; here it is a loop.
/// </para>
/// <para>
/// The single-pass tokenizers return the field count, or -1 when the line has more fields than the buffer holds;
/// the caller checks the count, as the runtime of design 4.5 does. The vector ones walk the bits of one compare per
/// block of 16 (<c>Vector256</c>) or 8 (<c>Vector128</c>) characters, then finish with a scalar tail, like
/// <c>QuoteBalanceScanner.ScanSingleQuote</c>. <see cref="Tokenizer_SinglePassVector_AnyMatchFirst"/> tests the
/// compare for any match before extracting its bits, which matters where extracting is expensive (Arm64).
/// </para>
/// <para>
/// Each benchmark returns the sum of the end of the middle field over all lines; its setup compares that with a
/// value computed with <see cref="string.Split(char, StringSplitOptions)"/>. <c>--check-prototypes</c> checks that the
/// four tokenizers give the same ends on every line.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class TokenizerBenchmarks
{
    /// <summary>The shape of the four tokenizers: field count, or -1 when the line does not fit <c>ends</c>.</summary>
    internal delegate int Tokenizer(ReadOnlySpan<char> line, Span<int> ends, char delimiter);

    private string[] _lines = [];
    private int[] _ends = [];
    private int _fieldCount;

    [Params(TokenizerDataset.Columns25, TokenizerDataset.Columns150, TokenizerDataset.LongLines)]
    public TokenizerDataset Dataset { get; set; }

    private void Prepare()
    {
        _lines = ComponentData.Lines(Dataset, ComponentData.DataDir());
        _fieldCount = ComponentData.FieldCount(Dataset);
        _ends = new int[_fieldCount];
    }

    private void Check(string name, long checksum)
    {
        long expected = 0;
        int middle = _fieldCount >> 1;
        foreach (string line in _lines)
        {
            string[] fields = line.Split(',');
            DataCheck.Equal(_fieldCount, fields.Length, name + " reference field count");
            int end = middle;
            for (int k = 0; k <= middle; k++)
                end += fields[k].Length;
            expected += end;
        }
        DataCheck.Equal(expected, checksum, name + " checksum");
    }

    [GlobalSetup(Target = nameof(Tokenizer_CountPlusIndexOf))]
    public void SetupCountPlusIndexOf()
    {
        Prepare();
        Check(nameof(Tokenizer_CountPlusIndexOf), Tokenizer_CountPlusIndexOf());
    }

    [GlobalSetup(Target = nameof(Tokenizer_SinglePassScalar))]
    public void SetupSinglePassScalar()
    {
        Prepare();
        Check(nameof(Tokenizer_SinglePassScalar), Tokenizer_SinglePassScalar());
    }

    [GlobalSetup(Target = nameof(Tokenizer_SinglePassVector))]
    public void SetupSinglePassVector()
    {
        Prepare();
        Check(nameof(Tokenizer_SinglePassVector), Tokenizer_SinglePassVector());
    }

    [GlobalSetup(Target = nameof(Tokenizer_SinglePassVector_AnyMatchFirst))]
    public void SetupSinglePassVectorAnyMatchFirst()
    {
        Prepare();
        Check(nameof(Tokenizer_SinglePassVector_AnyMatchFirst), Tokenizer_SinglePassVector_AnyMatchFirst());
    }

    [Benchmark(Baseline = true)]
    public long Tokenizer_CountPlusIndexOf()
    {
        long sum = 0;
        int[] ends = _ends;
        foreach (string line in _lines)
        {
            int n = CountPlusIndexOf(line, ends, ',');
            if (n != ends.Length)
                ThrowFieldCount(n);
            sum += ends[n >> 1];
        }
        return sum;
    }

    [Benchmark]
    public long Tokenizer_SinglePassScalar()
    {
        long sum = 0;
        int[] ends = _ends;
        foreach (string line in _lines)
        {
            int n = SinglePassScalar(line, ends, ',');
            if (n != ends.Length)
                ThrowFieldCount(n);
            sum += ends[n >> 1];
        }
        return sum;
    }

    [Benchmark]
    public long Tokenizer_SinglePassVector()
    {
        long sum = 0;
        int[] ends = _ends;
        foreach (string line in _lines)
        {
            int n = SinglePassVector(line, ends, ',');
            if (n != ends.Length)
                ThrowFieldCount(n);
            sum += ends[n >> 1];
        }
        return sum;
    }

    [Benchmark]
    public long Tokenizer_SinglePassVector_AnyMatchFirst()
    {
        long sum = 0;
        int[] ends = _ends;
        foreach (string line in _lines)
        {
            int n = SinglePassVectorAnyMatchFirst(line, ends, ',');
            if (n != ends.Length)
                ThrowFieldCount(n);
            sum += ends[n >> 1];
        }
        return sum;
    }

    private static void ThrowFieldCount(int n) =>
        throw new InvalidOperationException($"Benchmark correctness check failed: a line has {n} fields.");

    /// <summary>
    /// Today's work: the vectorised <c>Count</c> of the field-count check, then one <c>IndexOf</c> per field but the
    /// last. Returns <c>ends.Length</c>, or -1 when the count differs (every field of these records is mandatory).
    /// </summary>
    internal static int CountPlusIndexOf(ReadOnlySpan<char> line, Span<int> ends, char delimiter)
    {
        int count = line.Count(delimiter) + 1;
        if (count != ends.Length)
            return -1;

        int position = 0;
        for (int k = 0; k < ends.Length - 1; k++)
        {
            int at = line.Slice(position).IndexOf(delimiter);
            ends[k] = position + at;
            position += at + 1;
        }
        ends[^1] = line.Length;
        return count;
    }

    /// <summary>One scalar pass that writes the end of each field as it meets the delimiter.</summary>
    internal static int SinglePassScalar(ReadOnlySpan<char> line, Span<int> ends, char delimiter)
    {
        int n = 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] != delimiter)
                continue;
            if ((uint)n >= (uint)ends.Length)
                return -1;
            ends[n++] = i;
        }
        if ((uint)n >= (uint)ends.Length)
            return -1;
        ends[n++] = line.Length;
        return n;
    }

    /// <summary>One vector pass: compare a block, extract its bits, walk them.</summary>
    internal static int SinglePassVector(ReadOnlySpan<char> line, Span<int> ends, char delimiter)
    {
        int length = line.Length;
        ref ushort start = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(line));
        int position = 0;
        int n = 0;
        if (Vector256.IsHardwareAccelerated && length >= Vector256<ushort>.Count)
        {
            var delimiters = Vector256.Create((ushort)delimiter);
            for (; position <= length - Vector256<ushort>.Count; position += Vector256<ushort>.Count)
            {
                uint mask = Vector256.Equals(Vector256.LoadUnsafe(ref start, (nuint)position), delimiters).ExtractMostSignificantBits();
                while (mask != 0)
                {
                    if ((uint)n >= (uint)ends.Length)
                        return -1;
                    ends[n++] = position + BitOperations.TrailingZeroCount(mask);
                    mask &= mask - 1;
                }
            }
        }

        if (Vector128.IsHardwareAccelerated && length - position >= Vector128<ushort>.Count)
        {
            var delimiters = Vector128.Create((ushort)delimiter);
            for (; position <= length - Vector128<ushort>.Count; position += Vector128<ushort>.Count)
            {
                uint mask = Vector128.Equals(Vector128.LoadUnsafe(ref start, (nuint)position), delimiters).ExtractMostSignificantBits();
                while (mask != 0)
                {
                    if ((uint)n >= (uint)ends.Length)
                        return -1;
                    ends[n++] = position + BitOperations.TrailingZeroCount(mask);
                    mask &= mask - 1;
                }
            }
        }

        for (; position < length; position++)
        {
            if (line[position] != delimiter)
                continue;
            if ((uint)n >= (uint)ends.Length)
                return -1;
            ends[n++] = position;
        }

        if ((uint)n >= (uint)ends.Length)
            return -1;
        ends[n++] = length;
        return n;
    }

    /// <summary><see cref="SinglePassVector"/> with a test for any match before the bits are extracted.</summary>
    internal static int SinglePassVectorAnyMatchFirst(ReadOnlySpan<char> line, Span<int> ends, char delimiter)
    {
        int length = line.Length;
        ref ushort start = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(line));
        int position = 0;
        int n = 0;
        if (Vector256.IsHardwareAccelerated && length >= Vector256<ushort>.Count)
        {
            var delimiters = Vector256.Create((ushort)delimiter);
            for (; position <= length - Vector256<ushort>.Count; position += Vector256<ushort>.Count)
            {
                var matches = Vector256.Equals(Vector256.LoadUnsafe(ref start, (nuint)position), delimiters);
                if (matches == Vector256<ushort>.Zero)
                    continue;
                uint mask = matches.ExtractMostSignificantBits();
                while (mask != 0)
                {
                    if ((uint)n >= (uint)ends.Length)
                        return -1;
                    ends[n++] = position + BitOperations.TrailingZeroCount(mask);
                    mask &= mask - 1;
                }
            }
        }

        if (Vector128.IsHardwareAccelerated && length - position >= Vector128<ushort>.Count)
        {
            var delimiters = Vector128.Create((ushort)delimiter);
            for (; position <= length - Vector128<ushort>.Count; position += Vector128<ushort>.Count)
            {
                var matches = Vector128.Equals(Vector128.LoadUnsafe(ref start, (nuint)position), delimiters);
                if (matches == Vector128<ushort>.Zero)
                    continue;
                uint mask = matches.ExtractMostSignificantBits();
                while (mask != 0)
                {
                    if ((uint)n >= (uint)ends.Length)
                        return -1;
                    ends[n++] = position + BitOperations.TrailingZeroCount(mask);
                    mask &= mask - 1;
                }
            }
        }

        for (; position < length; position++)
        {
            if (line[position] != delimiter)
                continue;
            if ((uint)n >= (uint)ends.Length)
                return -1;
            ends[n++] = position;
        }

        if ((uint)n >= (uint)ends.Length)
            return -1;
        ends[n++] = length;
        return n;
    }
}
