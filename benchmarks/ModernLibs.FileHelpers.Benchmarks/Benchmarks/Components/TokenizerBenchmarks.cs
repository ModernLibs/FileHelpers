// MA0051: the vector loops are kept in one method each, as the library's scanner writes them.
#pragma warning disable MA0051

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
#if !FILEHELPERS_BASELINE
using ModernLibs.FileHelpers.Parsing;
#endif

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
/// <see cref="Tokenizer_Runtime"/> is the shipped <c>RecordTokenizer.TokenizePlain</c> (T15) as the runtime calls it
/// (<c>vectors: true</c>, so the default loop of T21), into an <c>int[]</c> of <c>FieldCount + 1</c> entries as the
/// runtime's <c>TokenBuffer</c> holds; the runtime then checks the count, as here. The other <c>Tokenizer_Runtime_*</c>
/// methods call the loop overload of the same shipped code with one <c>TokenizerLoop</c> and load form each (T21 step
/// 1): <c>_IndexOfHop</c> (one <c>IndexOf</c> per field), <c>_Vector128</c>, <c>_Vector256</c>, <c>_Vector512</c>,
/// <c>_Vector256_Packed</c> (design 2.5 item 16) and <c>_Vector256_LoadUnsafe</c> (against <c>_Vector256</c>, the span
/// form). A method marked <see cref="RequiresVectorAttribute"/> is not listed or run on a machine that does not
/// accelerate its width (<see cref="BenchConfig"/>). <see cref="TokenizerDataset.Columns5"/> is the short-record case of
/// freeze S3.
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
    private int[] _runtimeEnds = [];
    private int _fieldCount;

    [Params(TokenizerDataset.Columns5, TokenizerDataset.Columns25, TokenizerDataset.Columns150, TokenizerDataset.LongLines)]
    public TokenizerDataset Dataset { get; set; }

    private void Prepare()
    {
        _lines = ComponentData.Lines(Dataset, ComponentData.DataDir());
        _fieldCount = ComponentData.FieldCount(Dataset);
        _ends = new int[_fieldCount];
        _runtimeEnds = new int[_fieldCount + 1];
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

#if !FILEHELPERS_BASELINE
    // RecordTokenizer is new in 4.10: a build against the 4.9.1 package or source (round.sh baselines,
    // FILEHELPERS_BASELINE) has none of the Tokenizer_Runtime methods (setups and benchmarks).
    [GlobalSetup(Target = nameof(Tokenizer_Runtime))]
    public void SetupRuntime()
    {
        Prepare();
        Check(nameof(Tokenizer_Runtime), Tokenizer_Runtime());
    }

    [GlobalSetup(Target = nameof(Tokenizer_Runtime_IndexOfHop))]
    public void SetupRuntimeIndexOfHop()
    {
        Prepare();
        Check(nameof(Tokenizer_Runtime_IndexOfHop), Tokenizer_Runtime_IndexOfHop());
    }

    [GlobalSetup(Target = nameof(Tokenizer_Runtime_Vector128))]
    public void SetupRuntimeVector128()
    {
        Prepare();
        Check(nameof(Tokenizer_Runtime_Vector128), Tokenizer_Runtime_Vector128());
    }

    [GlobalSetup(Target = nameof(Tokenizer_Runtime_Vector256))]
    public void SetupRuntimeVector256()
    {
        Prepare();
        Check(nameof(Tokenizer_Runtime_Vector256), Tokenizer_Runtime_Vector256());
    }

    [GlobalSetup(Target = nameof(Tokenizer_Runtime_Vector512))]
    public void SetupRuntimeVector512()
    {
        Prepare();
        Check(nameof(Tokenizer_Runtime_Vector512), Tokenizer_Runtime_Vector512());
    }

    [GlobalSetup(Target = nameof(Tokenizer_Runtime_Vector256_Packed))]
    public void SetupRuntimeVector256Packed()
    {
        Prepare();
        Check(nameof(Tokenizer_Runtime_Vector256_Packed), Tokenizer_Runtime_Vector256_Packed());
    }

    [GlobalSetup(Target = nameof(Tokenizer_Runtime_Vector256_LoadUnsafe))]
    public void SetupRuntimeVector256LoadUnsafe()
    {
        Prepare();
        Check(nameof(Tokenizer_Runtime_Vector256_LoadUnsafe), Tokenizer_Runtime_Vector256_LoadUnsafe());
    }
#endif

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

#if !FILEHELPERS_BASELINE
    /// <summary>The shipped tokenizer as the runtime calls it (<c>vectors: true</c>): <c>RecordTokenizer.PlainLoop</c>.</summary>
    [Benchmark]
    public long Tokenizer_Runtime()
    {
        long sum = 0;
        int[] ends = _runtimeEnds;
        int fieldCount = _fieldCount;
        foreach (string line in _lines)
        {
            int n = RecordTokenizer.TokenizePlain(line, ',', ends, vectors: true);
            if (n != fieldCount)
                ThrowFieldCount(n);
            sum += ends[n >> 1];
        }
        return sum;
    }

    /// <summary>One <c>IndexOf</c> per field: the switch, the no-SIMD path and the Arm64 default.</summary>
    [Benchmark]
    public long Tokenizer_Runtime_IndexOfHop() => RunLoop(TokenizerLoop.IndexOfHop, unsafeLoad: false);

    /// <summary><c>Vector128</c> blocks, span loads: the x64 default without AVX2.</summary>
    [Benchmark, RequiresVector(128)]
    public long Tokenizer_Runtime_Vector128() => RunLoop(TokenizerLoop.Vector128, unsafeLoad: false);

    /// <summary><c>Vector256</c> blocks, span loads: the x64 default with AVX2.</summary>
    [Benchmark, RequiresVector(256)]
    public long Tokenizer_Runtime_Vector256() => RunLoop(TokenizerLoop.Vector256, unsafeLoad: false);

    /// <summary><c>Vector512</c> blocks, span loads: the AVX-512 candidate (gate: at least 3 % on <c>LongLines</c>, <c>fh-intel</c>).</summary>
    [Benchmark, RequiresVector(512)]
    public long Tokenizer_Runtime_Vector512() => RunLoop(TokenizerLoop.Vector512, unsafeLoad: false);

    /// <summary>
    /// <c>Vector256</c> with the packed pre-test per 32 chars (design 2.5 item 16). The decision reads
    /// <c>LongLines</c> (at least 3 %) and <c>Columns25</c> (no loss); the other datasets are informative.
    /// </summary>
    [Benchmark, RequiresVector(256)]
    public long Tokenizer_Runtime_Vector256_Packed() => RunLoop(TokenizerLoop.Vector256Packed, unsafeLoad: false);

    /// <summary><c>Vector256</c> blocks loaded with <c>LoadUnsafe</c>, against <see cref="Tokenizer_Runtime_Vector256"/> (<c>UseUnsafeLoad</c>, freeze F-8, F5).</summary>
    [Benchmark, RequiresVector(256)]
    public long Tokenizer_Runtime_Vector256_LoadUnsafe() => RunLoop(TokenizerLoop.Vector256, unsafeLoad: true);

    // Inlined into each benchmark, so the loop and the load form are constants there, as PlainLoop and UseUnsafeLoad
    // are in the runtime's call.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long RunLoop(TokenizerLoop loop, bool unsafeLoad)
    {
        long sum = 0;
        int[] ends = _runtimeEnds;
        int fieldCount = _fieldCount;
        foreach (string line in _lines)
        {
            int n = RecordTokenizer.TokenizePlain(line, ',', ends, loop, unsafeLoad);
            if (n != fieldCount)
                ThrowFieldCount(n);
            sum += ends[n >> 1];
        }
        return sum;
    }
#endif

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
