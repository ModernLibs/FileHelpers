// MA0051: the vector loops are kept in one method each, as the library's scanner writes them.
#pragma warning disable MA0051

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;

/// <summary>The datasets of <see cref="VectorLoopShapeBenchmarks"/>.</summary>
public enum ScanDataset
{
    /// <summary>10,000 lines of the seeded quoted set (40 to 90 characters, quotes and delimiters).</summary>
    Quoted,

    /// <summary>5,000 lines of 505 characters, 8 columns, no quotes.</summary>
    LongLines,
}

/// <summary>
/// The delimiter-and-quote scan loop of <c>QuoteBalanceScanner.ScanSingleQuote</c>
/// (<c>QuoteBalanceScanner.cs:139-170</c>) in two coding patterns (design 2.5 item 1): the <c>LoadUnsafe</c> form the
/// library uses, and the same loop written with <c>Vector256.Create(span.Slice(i, n))</c>.
/// </summary>
/// <remarks>
/// Both loops are reduced to counting: at each delimiter or quote they add its position + 1 to a checksum instead of
/// advancing the quote walk, so the checksum counts the hits and checks where they are. The setup compares it with a
/// scalar reference; <c>--check-prototypes</c> checks that both forms agree on every line. The span form still
/// needs one <c>MemoryMarshal.Cast&lt;char, ushort&gt;</c> per line, because <c>Vector256.Create</c> has no
/// <c>char</c> overload; inside the loop it uses no <c>Unsafe</c> and no <c>MemoryMarshal</c>.
/// </remarks>
[MemoryDiagnoser]
public class VectorLoopShapeBenchmarks
{
    private string[] _lines = [];

    [Params(ScanDataset.Quoted, ScanDataset.LongLines)]
    public ScanDataset Dataset { get; set; }

    private void Prepare()
    {
        string dir = ComponentData.DataDir();
        _lines = Dataset == ScanDataset.Quoted
            ? DataCheck.DataLines(DatasetGenerator.EnsureQuotedFieldsRandom(dir, 10_000), hasHeader: true)
            : DataCheck.DataLines(DatasetGenerator.EnsureLongLines(dir, 5_000), hasHeader: false);
    }

    private void Check(string name, long checksum)
    {
        long expected = 0;
        foreach (string line in _lines)
            expected += ScanReference(line, ',', '"');
        DataCheck.That(expected > 0, name + ": the dataset has no delimiter");
        DataCheck.Equal(expected, checksum, name + " checksum");
    }

    [GlobalSetup(Target = nameof(Scan_LoadUnsafe))]
    public void SetupLoadUnsafe()
    {
        Prepare();
        Check(nameof(Scan_LoadUnsafe), Scan_LoadUnsafe());
    }

    [GlobalSetup(Target = nameof(Scan_SpanCreate))]
    public void SetupSpanCreate()
    {
        Prepare();
        Check(nameof(Scan_SpanCreate), Scan_SpanCreate());
    }

    [Benchmark(Baseline = true)]
    public long Scan_LoadUnsafe()
    {
        long sum = 0;
        foreach (string line in _lines)
            sum += ScanLoadUnsafe(line, ',', '"');
        return sum;
    }

    [Benchmark]
    public long Scan_SpanCreate()
    {
        long sum = 0;
        foreach (string line in _lines)
            sum += ScanSpanCreate(line, ',', '"');
        return sum;
    }

    /// <summary>The sum of position + 1 over every delimiter and quote of <paramref name="line"/>, one character at a time.</summary>
    internal static long ScanReference(string line, char delimiter, char quote)
    {
        long sum = 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == delimiter || line[i] == quote)
                sum += i + 1;
        }
        return sum;
    }

    /// <summary>The loop of <c>QuoteBalanceScanner.cs:139-170</c>: <c>LoadUnsafe</c> from a <c>ref ushort</c>.</summary>
    internal static long ScanLoadUnsafe(ReadOnlySpan<char> line, char delimiter, char quote)
    {
        long sum = 0;
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
                    sum += at + 1;
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
                    sum += at + 1;
                }
            }
        }

        for (; position < length; position++)
        {
            char c = line[position];
            if (c == delimiter || c == quote)
                sum += position + 1;
        }
        return sum;
    }

    /// <summary>The same loop with <c>Vector256.Create(span.Slice(position, n))</c> and no <c>Unsafe</c> in the loop.</summary>
    internal static long ScanSpanCreate(ReadOnlySpan<char> line, char delimiter, char quote)
    {
        long sum = 0;
        ReadOnlySpan<ushort> units = MemoryMarshal.Cast<char, ushort>(line);
        int length = units.Length;
        int position = 0;
        if (Vector256.IsHardwareAccelerated && length >= Vector256<ushort>.Count)
        {
            var delimiters = Vector256.Create((ushort)delimiter);
            var quoteChars = Vector256.Create((ushort)quote);
            for (; position <= length - Vector256<ushort>.Count; position += Vector256<ushort>.Count)
            {
                var chars = Vector256.Create(units.Slice(position, Vector256<ushort>.Count));
                uint mask = (Vector256.Equals(chars, delimiters) | Vector256.Equals(chars, quoteChars)).ExtractMostSignificantBits();
                while (mask != 0)
                {
                    int at = position + BitOperations.TrailingZeroCount(mask);
                    mask &= mask - 1;
                    sum += at + 1;
                }
            }
        }

        if (Vector128.IsHardwareAccelerated && length - position >= Vector128<ushort>.Count)
        {
            var delimiters = Vector128.Create((ushort)delimiter);
            var quoteChars = Vector128.Create((ushort)quote);
            for (; position <= length - Vector128<ushort>.Count; position += Vector128<ushort>.Count)
            {
                var chars = Vector128.Create(units.Slice(position, Vector128<ushort>.Count));
                uint mask = (Vector128.Equals(chars, delimiters) | Vector128.Equals(chars, quoteChars)).ExtractMostSignificantBits();
                while (mask != 0)
                {
                    int at = position + BitOperations.TrailingZeroCount(mask);
                    mask &= mask - 1;
                    sum += at + 1;
                }
            }
        }

        for (; position < length; position++)
        {
            ushort c = units[position];
            if (c == delimiter || c == quote)
                sum += position + 1;
        }
        return sum;
    }
}
