#if !FILEHELPERS_BASELINE
// RecordTokenizer, TokenBuffer and TokenizerLoop are new in 4.10: a build against the 4.9.1 package or source
// (round.sh baselines, FILEHELPERS_BASELINE) has no class here.
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;

/// <summary>
/// The quoted tokenizer (T20, <c>RecordTokenizer.TokenizeQuoted</c>) with each walk loop (T21 step 1): the field end
/// entries of every line of 10,000 lines of the seeded quoted set (Id plain, the four text fields
/// <c>[FieldQuoted]</c>), written into one <c>TokenBuffer</c> of the record type, as the runtime does. Each method calls
/// the loop overload of the shipped code; a line without a quote char takes the plain tokenizer with the same loop.
/// </summary>
/// <remarks>
/// Each benchmark returns the sum of the entry of the middle field (Description: its end with the quoted and
/// doubled-quote bits) over all lines; its setup compares that with a value computed with
/// <see cref="ComponentData.QuotedFields"/>. <c>--check-prototypes</c> checks every entry of every line for each loop.
/// A method marked <see cref="RequiresVectorAttribute"/> is not listed or run on a machine that does not accelerate its
/// width.
/// </remarks>
[MemoryDiagnoser]
public class QuotedTokenizerBenchmarks
{
    /// <summary>The fields of the seeded quoted set: Id, then Name, Description, Notes and Category quoted with '"'.</summary>
    internal const int FieldCount = 5;

    /// <summary>The quote char of each field, as <c>RecordReadCore</c> passes it.</summary>
    internal const string Quotes = "\0\"\"\"\"";

    private string[] _lines = [];
    private TokenBuffer _buffer = new(FieldCount);

    /// <summary>The data lines of the seeded quoted set (10,000 rows), generated in <paramref name="dataDir"/> when missing.</summary>
    /// <param name="dataDir">The data folder.</param>
    /// <returns>The lines, header skipped.</returns>
    internal static string[] Lines(string dataDir) =>
        DataCheck.DataLines(DatasetGenerator.EnsureQuotedFieldsRandom(dataDir, 10_000), hasHeader: true);

    /// <summary>The entry the tokenizer writes for <paramref name="field"/> of <paramref name="line"/>, from the reference split.</summary>
    /// <param name="line">A line of the seeded quoted set.</param>
    /// <param name="field">The field index.</param>
    /// <returns>The end, with <c>TokenBuffer.QuotedBit</c> and <c>TokenBuffer.DoubledBit</c> as the value has them.</returns>
    internal static int ExpectedEntry(string line, int field)
    {
        var (start, end, quoted) = ComponentData.QuotedFields(line)[field];
        if (!quoted)
            return end;
        bool doubled = line.AsSpan(start + 1, end - start - 2).Contains("\"\"", StringComparison.Ordinal);
        return end | TokenBuffer.QuotedBit | (doubled ? TokenBuffer.DoubledBit : 0);
    }

    private void Prepare()
    {
        _lines = Lines(ComponentData.DataDir());
        _buffer = new TokenBuffer(FieldCount);
    }

    private void Check(string name, long checksum)
    {
        long expected = 0;
        bool anyQuoted = false;
        foreach (string line in _lines)
        {
            DataCheck.Equal(FieldCount, ComponentData.QuotedFields(line).Count, name + " reference field count");
            int entry = ExpectedEntry(line, FieldCount >> 1);
            anyQuoted |= entry < 0;
            expected += entry;
        }
        DataCheck.That(anyQuoted, name + ": no quoted Description in the dataset");
        DataCheck.Equal(expected, checksum, name + " checksum");
    }

    [GlobalSetup(Target = nameof(TokenizerQuoted_IndexOfHop))]
    public void SetupIndexOfHop()
    {
        Prepare();
        Check(nameof(TokenizerQuoted_IndexOfHop), TokenizerQuoted_IndexOfHop());
    }

    [GlobalSetup(Target = nameof(TokenizerQuoted_Vector128))]
    public void SetupVector128()
    {
        Prepare();
        Check(nameof(TokenizerQuoted_Vector128), TokenizerQuoted_Vector128());
    }

    [GlobalSetup(Target = nameof(TokenizerQuoted_Vector256))]
    public void SetupVector256()
    {
        Prepare();
        Check(nameof(TokenizerQuoted_Vector256), TokenizerQuoted_Vector256());
    }

    /// <summary>One <c>IndexOfAny</c> per delimiter or quote position: the switch, the no-SIMD path and the Arm64 default.</summary>
    [Benchmark(Baseline = true)]
    public long TokenizerQuoted_IndexOfHop() => Run(TokenizerLoop.IndexOfHop);

    /// <summary><c>Vector128</c> blocks: the x64 default without AVX2.</summary>
    [Benchmark, RequiresVector(128)]
    public long TokenizerQuoted_Vector128() => Run(TokenizerLoop.Vector128);

    /// <summary><c>Vector256</c> blocks, then <c>Vector128</c>: the x64 default with AVX2.</summary>
    [Benchmark, RequiresVector(256)]
    public long TokenizerQuoted_Vector256() => Run(TokenizerLoop.Vector256);

    // Inlined into each benchmark, so the loop is a constant there.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long Run(TokenizerLoop loop)
    {
        long sum = 0;
        var buffer = _buffer;
        int[] ends = buffer.Ends;
        foreach (string line in _lines)
        {
            int n = RecordTokenizer.TokenizeQuoted(line, ',', '"', Quotes, buffer, loop, out _, out _);
            if (n != FieldCount)
                ThrowFieldCount(n);
            sum += ends[n >> 1];
        }
        return sum;
    }

    private static void ThrowFieldCount(int n) =>
        throw new InvalidOperationException($"Benchmark correctness check failed: a line has {n} fields.");
}
#endif
