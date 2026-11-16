// MA0051: the twelve slices are written out one by one, as the generator emits them.
#pragma warning disable MA0051

using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;

/// <summary>The record type <see cref="FixedSliceBenchmarks"/> fills.</summary>
public enum FixedSliceRecord
{
    /// <summary>A class: one allocation per line.</summary>
    Class,

    /// <summary>A struct: no allocation.</summary>
    Struct,
}

/// <summary>
/// Fixed-length slicing only (T30, G5): the twelve field slices of a fixed-length record of 88 characters, with
/// and without the <c>Math.Min</c> the generator emits today.
/// </summary>
/// <remarks>
/// <see cref="Slice_MathMin"/> reproduces the expression of <c>EmitFixedLengthFieldSplitting</c> in
/// <c>DescriptorEmitter.cs</c>: <c>line.Length &gt; offset ? line.Slice(offset, Math.Min(width, line.Length - offset)) :
/// ReadOnlySpan&lt;char&gt;.Empty</c>. <see cref="Slice_Direct"/> is <c>line.Slice(offset, width)</c> after one length test
/// (a line of the wrong width throws before any slice), the shape G5 would emit for <c>ExactLength</c> records
/// without optional fields. Every slice is reduced to one <see cref="int"/> (its length and first character) so the
/// work cannot be removed, and the twelve values fill a record of the kind in <see cref="Record"/>. Each method
/// returns the sum of all twelve values of all records. The setups compare every record with a <c>Substring</c>
/// reference; the comparison of the two shapes with each other lives in <c>--check-prototypes</c>.
/// </remarks>
[MemoryDiagnoser]
public class FixedSliceBenchmarks
{
    internal const int LineLength = 88;
    internal const int LineCount = 10_000;

    internal static readonly int[] Widths = [6, 8, 4, 10, 5, 7, 9, 3, 12, 6, 8, 10];

    private string[] _lines = [];

    [Params(FixedSliceRecord.Class, FixedSliceRecord.Struct)]
    public FixedSliceRecord Record { get; set; }

    /// <summary>Class record of twelve parsed values.</summary>
    internal sealed class Rec
    {
        public Rec(int F0, int F1, int F2, int F3, int F4, int F5, int F6, int F7, int F8, int F9, int F10, int F11)
        {
            this.F0 = F0;
            this.F1 = F1;
            this.F2 = F2;
            this.F3 = F3;
            this.F4 = F4;
            this.F5 = F5;
            this.F6 = F6;
            this.F7 = F7;
            this.F8 = F8;
            this.F9 = F9;
            this.F10 = F10;
            this.F11 = F11;
        }

        public int F0 { get; }
        public int F1 { get; }
        public int F2 { get; }
        public int F3 { get; }
        public int F4 { get; }
        public int F5 { get; }
        public int F6 { get; }
        public int F7 { get; }
        public int F8 { get; }
        public int F9 { get; }
        public int F10 { get; }
        public int F11 { get; }

        public long Sum() => F0 + F1 + F2 + F3 + F4 + F5 + F6 + F7 + F8 + F9 + F10 + F11;

        public int[] ToArray() => [F0, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11];
    }

    /// <summary>Struct record of twelve parsed values.</summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct RecS(int F0, int F1, int F2, int F3, int F4, int F5, int F6, int F7, int F8, int F9, int F10, int F11)
    {
        public long Sum() => F0 + F1 + F2 + F3 + F4 + F5 + F6 + F7 + F8 + F9 + F10 + F11;

        public int[] ToArray() => [F0, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11];
    }

    /// <summary>The seeded lines: exactly <see cref="LineLength"/> characters.</summary>
    internal static string[] MakeLines()
    {
        var random = new Random(410);
        var lines = new string[LineCount];
        for (int i = 0; i < lines.Length; i++)
        {
            var chars = new char[LineLength];
            for (int k = 0; k < chars.Length; k++)
                chars[k] = (char)('A' + random.Next(0, 26));
            lines[i] = new string(chars);
        }
        return lines;
    }

    /// <summary>The twelve values of a line by <c>Substring</c>, the reference of every check.</summary>
    internal static int[] Reference(string line)
    {
        var values = new int[Widths.Length];
        int offset = 0;
        for (int i = 0; i < Widths.Length; i++)
        {
            string text = line.Substring(offset, Widths[i]);
            values[i] = text.Length * 31 + text[0];
            offset += Widths[i];
        }
        return values;
    }

    private static int Value(ReadOnlySpan<char> slice) => slice.Length * 31 + (slice.Length > 0 ? slice[0] : 0);

    internal static Rec MathMinClass(ReadOnlySpan<char> line)
    {
        int v0 = Value(line.Length > 0 ? line.Slice(0, Math.Min(6, line.Length - 0)) : ReadOnlySpan<char>.Empty);
        int v1 = Value(line.Length > 6 ? line.Slice(6, Math.Min(8, line.Length - 6)) : ReadOnlySpan<char>.Empty);
        int v2 = Value(line.Length > 14 ? line.Slice(14, Math.Min(4, line.Length - 14)) : ReadOnlySpan<char>.Empty);
        int v3 = Value(line.Length > 18 ? line.Slice(18, Math.Min(10, line.Length - 18)) : ReadOnlySpan<char>.Empty);
        int v4 = Value(line.Length > 28 ? line.Slice(28, Math.Min(5, line.Length - 28)) : ReadOnlySpan<char>.Empty);
        int v5 = Value(line.Length > 33 ? line.Slice(33, Math.Min(7, line.Length - 33)) : ReadOnlySpan<char>.Empty);
        int v6 = Value(line.Length > 40 ? line.Slice(40, Math.Min(9, line.Length - 40)) : ReadOnlySpan<char>.Empty);
        int v7 = Value(line.Length > 49 ? line.Slice(49, Math.Min(3, line.Length - 49)) : ReadOnlySpan<char>.Empty);
        int v8 = Value(line.Length > 52 ? line.Slice(52, Math.Min(12, line.Length - 52)) : ReadOnlySpan<char>.Empty);
        int v9 = Value(line.Length > 64 ? line.Slice(64, Math.Min(6, line.Length - 64)) : ReadOnlySpan<char>.Empty);
        int v10 = Value(line.Length > 70 ? line.Slice(70, Math.Min(8, line.Length - 70)) : ReadOnlySpan<char>.Empty);
        int v11 = Value(line.Length > 78 ? line.Slice(78, Math.Min(10, line.Length - 78)) : ReadOnlySpan<char>.Empty);
        return new Rec(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11);
    }

    internal static RecS MathMinStruct(ReadOnlySpan<char> line)
    {
        int v0 = Value(line.Length > 0 ? line.Slice(0, Math.Min(6, line.Length - 0)) : ReadOnlySpan<char>.Empty);
        int v1 = Value(line.Length > 6 ? line.Slice(6, Math.Min(8, line.Length - 6)) : ReadOnlySpan<char>.Empty);
        int v2 = Value(line.Length > 14 ? line.Slice(14, Math.Min(4, line.Length - 14)) : ReadOnlySpan<char>.Empty);
        int v3 = Value(line.Length > 18 ? line.Slice(18, Math.Min(10, line.Length - 18)) : ReadOnlySpan<char>.Empty);
        int v4 = Value(line.Length > 28 ? line.Slice(28, Math.Min(5, line.Length - 28)) : ReadOnlySpan<char>.Empty);
        int v5 = Value(line.Length > 33 ? line.Slice(33, Math.Min(7, line.Length - 33)) : ReadOnlySpan<char>.Empty);
        int v6 = Value(line.Length > 40 ? line.Slice(40, Math.Min(9, line.Length - 40)) : ReadOnlySpan<char>.Empty);
        int v7 = Value(line.Length > 49 ? line.Slice(49, Math.Min(3, line.Length - 49)) : ReadOnlySpan<char>.Empty);
        int v8 = Value(line.Length > 52 ? line.Slice(52, Math.Min(12, line.Length - 52)) : ReadOnlySpan<char>.Empty);
        int v9 = Value(line.Length > 64 ? line.Slice(64, Math.Min(6, line.Length - 64)) : ReadOnlySpan<char>.Empty);
        int v10 = Value(line.Length > 70 ? line.Slice(70, Math.Min(8, line.Length - 70)) : ReadOnlySpan<char>.Empty);
        int v11 = Value(line.Length > 78 ? line.Slice(78, Math.Min(10, line.Length - 78)) : ReadOnlySpan<char>.Empty);
        return new RecS(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11);
    }

    internal static Rec DirectClass(ReadOnlySpan<char> line)
    {
        if (line.Length != LineLength)
            throw new InvalidOperationException("Line of the wrong width.");
        int v0 = Value(line.Slice(0, 6));
        int v1 = Value(line.Slice(6, 8));
        int v2 = Value(line.Slice(14, 4));
        int v3 = Value(line.Slice(18, 10));
        int v4 = Value(line.Slice(28, 5));
        int v5 = Value(line.Slice(33, 7));
        int v6 = Value(line.Slice(40, 9));
        int v7 = Value(line.Slice(49, 3));
        int v8 = Value(line.Slice(52, 12));
        int v9 = Value(line.Slice(64, 6));
        int v10 = Value(line.Slice(70, 8));
        int v11 = Value(line.Slice(78, 10));
        return new Rec(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11);
    }

    internal static RecS DirectStruct(ReadOnlySpan<char> line)
    {
        if (line.Length != LineLength)
            throw new InvalidOperationException("Line of the wrong width.");
        int v0 = Value(line.Slice(0, 6));
        int v1 = Value(line.Slice(6, 8));
        int v2 = Value(line.Slice(14, 4));
        int v3 = Value(line.Slice(18, 10));
        int v4 = Value(line.Slice(28, 5));
        int v5 = Value(line.Slice(33, 7));
        int v6 = Value(line.Slice(40, 9));
        int v7 = Value(line.Slice(49, 3));
        int v8 = Value(line.Slice(52, 12));
        int v9 = Value(line.Slice(64, 6));
        int v10 = Value(line.Slice(70, 8));
        int v11 = Value(line.Slice(78, 10));
        return new RecS(v0, v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11);
    }

    internal static int[] ParseMathMin(FixedSliceRecord record, string line)
        => record == FixedSliceRecord.Class ? MathMinClass(line).ToArray() : MathMinStruct(line).ToArray();

    internal static int[] ParseDirect(FixedSliceRecord record, string line)
        => record == FixedSliceRecord.Class ? DirectClass(line).ToArray() : DirectStruct(line).ToArray();

    /// <summary>Checks one shape of the kind selected by <see cref="Record"/> against the reference on every line.</summary>
    private void Check(string name, Func<FixedSliceRecord, string, int[]> parse)
    {
        _lines = MakeLines();
        foreach (string line in _lines)
        {
            int[] expected = Reference(line);
            int[] actual = parse(Record, line);
            for (int i = 0; i < expected.Length; i++)
                DataCheck.Equal(expected[i], actual[i], $"{name} ({Record}) field {i} of '{line}'");
        }
    }

    [GlobalSetup(Target = nameof(Slice_MathMin))]
    public void SetupMathMin() => Check(nameof(Slice_MathMin), ParseMathMin);

    [GlobalSetup(Target = nameof(Slice_Direct))]
    public void SetupDirect() => Check(nameof(Slice_Direct), ParseDirect);

    [Benchmark(Baseline = true)]
    public long Slice_MathMin()
    {
        long sum = 0;
        string[] lines = _lines;
        if (Record == FixedSliceRecord.Class)
        {
            for (int i = 0; i < lines.Length; i++)
                sum += MathMinClass(lines[i]).Sum();
        }
        else
        {
            for (int i = 0; i < lines.Length; i++)
                sum += MathMinStruct(lines[i]).Sum();
        }
        return sum;
    }

    [Benchmark]
    public long Slice_Direct()
    {
        long sum = 0;
        string[] lines = _lines;
        if (Record == FixedSliceRecord.Class)
        {
            for (int i = 0; i < lines.Length; i++)
                sum += DirectClass(lines[i]).Sum();
        }
        else
        {
            for (int i = 0; i < lines.Length; i++)
                sum += DirectStruct(lines[i]).Sum();
        }
        return sum;
    }
}
