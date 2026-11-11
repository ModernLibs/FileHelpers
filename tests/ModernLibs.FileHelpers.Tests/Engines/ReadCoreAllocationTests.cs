// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Text;
using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// The shared read pipeline makes no per-record (or per-line) heap allocation for struct records on the
/// callback and streaming paths: allocated bytes do not grow with the number of records.
/// </summary>
[Category("ReadCore")]
[NotInParallel("Allocation")]
public sealed class ReadCoreAllocationTests
{
    private static string Build(int records)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < records; i++)
            sb.Append(i).Append(',').Append(i * 2).Append("\r\n");
        return sb.ToString();
    }

    private static long Measure(Action action)
    {
        action(); // warm-up (JIT, pools)
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Test]
    [DisplayName("ReadString with a by-ref callback allocates the same for 100 and 10,000 struct records")]
    public async Task RefCallback_StructRecords_NoPerRecordAllocation()
    {
        // Arrange: small and large inputs, one engine and one callback
        var engine = new FileHelperEngine<RcStruct>();
        string small = Build(100);
        string large = Build(10_000);
        long sum = 0;
        SpanRecordCallback<RcStruct> callback = (ref RcStruct r) => sum += r.Value;

        // Act: measure both reads
        long smallBytes = Measure(() => engine.ReadString(small, callback));
        long largeBytes = Measure(() => engine.ReadString(large, callback));

        // Assert: constant overhead only
        await Assert.That(largeBytes - smallBytes).IsLessThan(1024L);
        await Assert.That(largeBytes).IsLessThan(4096L);
        await Assert.That(sum).IsGreaterThan(0L);
    }

    [Test]
    [DisplayName("ReadString with a by-ref callback: [FieldDelimiter] struct records allocate nothing per record")]
    public async Task RefCallback_PerFieldDelimiterStruct_NoPerRecordAllocation()
    {
        // Arrange: "id,value|qty" lines (validated field count with per-field delimiters)
        static string BuildMixed(int records)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < records; i++)
                sb.Append(i).Append(',').Append(i * 2).Append('|').Append(i).Append("\r\n");
            return sb.ToString();
        }

        var engine = new FileHelperEngine<ModernLibs.FileHelpers.Tests.Engine.FieldDelimiterStruct>();
        string small = BuildMixed(100);
        string large = BuildMixed(10_000);
        long sum = 0;
        SpanRecordCallback<ModernLibs.FileHelpers.Tests.Engine.FieldDelimiterStruct> callback =
            (ref ModernLibs.FileHelpers.Tests.Engine.FieldDelimiterStruct r) => sum += r.Value;

        // Act
        long smallBytes = Measure(() => engine.ReadString(small, callback));
        long largeBytes = Measure(() => engine.ReadString(large, callback));

        // Assert: constant overhead only
        await Assert.That(largeBytes - smallBytes).IsLessThan(1024L);
        await Assert.That(sum).IsGreaterThan(0L);
    }

    [Test]
    [DisplayName("ReadSync over a TextReader allocates the same for 100 and 10,000 struct records")]
    public async Task ReadSync_StructRecords_NoPerRecordAllocation()
    {
        // Arrange: small and large inputs
        var engine = new FileHelperEngine<RcStruct>();
        string small = Build(100);
        string large = Build(10_000);
        long sum = 0;

        // Act: stream both through ReadSync
        long smallBytes = Measure(() =>
        {
            foreach (var r in engine.ReadSync(new StringReader(small)))
                sum += r.Value;
        });
        long largeBytes = Measure(() =>
        {
            foreach (var r in engine.ReadSync(new StringReader(large)))
                sum += r.Value;
        });

        // Assert: no per-line string, no per-record allocation
        await Assert.That(largeBytes - smallBytes).IsLessThan(1024L);
        await Assert.That(sum).IsGreaterThan(0L);
    }

    [Test]
    [DisplayName("ReadSync with [IgnoreLast] over a TextReader allocates the same for 100 and 10,000 struct records")]
    public async Task ReadSync_IgnoreLast_NoPerLineAllocation()
    {
        // Arrange: unknown-length input, so the footer lines are held back in the lookahead ring
        var engine = new FileHelperEngine<RcFooterStruct>();
        string small = Build(100);
        string large = Build(10_000);
        long sum = 0;

        // Act: stream both through ReadSync
        long smallBytes = Measure(() =>
        {
            foreach (var r in engine.ReadSync(new StringReader(small)))
                sum += r.Value;
        });
        long largeBytes = Measure(() =>
        {
            foreach (var r in engine.ReadSync(new StringReader(large)))
                sum += r.Value;
        });

        // Assert: the ring reuses its buffers (no per-line string)
        await Assert.That(largeBytes - smallBytes).IsLessThan(1024L);
        await Assert.That(engine.FooterText).IsEqualTo("9999,19998");
        await Assert.That(sum).IsGreaterThan(0L);
    }

    // Allocation budgets (T13). Measured on the 4.9.1 read core: bytes per row, identical on net10.0 and
    // net11.0 (runtime async did not change it), identical across three runs. Budget = measured * 1.10, rounded up.

    private static string BuildFiveInts(int rows)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < rows; i++)
            sb.Append(i).Append(',').Append(i + 1).Append(',').Append(i + 2).Append(',').Append(i + 3).Append(',').Append(i + 4).Append("\r\n");
        return sb.ToString();
    }

    private static string BuildMixed(int rows)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < rows; i++)
            sb.Append("alpha").Append(i).Append(',').Append(i).Append(",beta").Append(i).Append(',').Append(i * 2).Append(",gamma").Append(i).Append("\r\n");
        return sb.ToString();
    }

    // Half the rows carry a doubled quote ("") inside the first field.
    private static string BuildQuoted(int rows)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < rows; i++)
        {
            if (i % 2 == 0)
                sb.Append("\"say \"\"hi\"\" ").Append(i).Append("\",\"plain ").Append(i).Append('"');
            else
                sb.Append("\"first ").Append(i).Append("\",\"second ").Append(i).Append('"');
            sb.Append("\r\n");
        }
        return sb.ToString();
    }

    private static long MeasureStable(Action action)
    {
        GC.Collect();
        action(); // warm-up (JIT, pools)
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>Heap bytes per row: (bytes for 10,000 rows - bytes for 100 rows) / 9,900.</summary>
    private static long BytesPerRow(Action<string> read, Func<int, string> build)
    {
        string small = build(100);
        string large = build(10_000);
        long smallBytes = MeasureStable(() => read(small));
        long largeBytes = MeasureStable(() => read(large));
        return (largeBytes - smallBytes) / 9_900;
    }

    private static long ReadArray<T>(Func<int, string> build)
        where T : new()
    {
        var engine = new FileHelperEngine<T>();
        return BytesPerRow(s => engine.ReadString(s), build);
    }

    private static long Callback<T>(Func<int, string> build)
        where T : new()
    {
        var engine = new FileHelperEngine<T>();
        long count = 0;
        return BytesPerRow(s => engine.ReadString(s, (T _) => count++), build);
    }

    private static long Streaming<T>(Func<int, string> build)
        where T : new()
    {
        var engine = new FileHelperEngine<T>();
        long count = 0;
        return BytesPerRow(s =>
        {
            foreach (var _ in engine.ReadSync(s))
                count++;
        }, build);
    }

    public static IEnumerable<Func<(string Name, Func<long> BytesPerRow, long Budget)>> BudgetCases()
    {
        // measured 24
        yield return () => ("5-int struct, array", () => ReadArray<RcFiveInts>(BuildFiveInts), 27L);
        // measured 0
        yield return () => ("5-int struct, callback", () => Callback<RcFiveInts>(BuildFiveInts), 0L);
        // measured 0
        yield return () => ("5-int struct, ReadSync", () => Streaming<RcFiveInts>(BuildFiveInts), 0L);
        // measured 176
        yield return () => ("3 strings + 2 ints, array", () => ReadArray<RcMixed>(BuildMixed), 194L);
        // measured 168
        yield return () => ("3 strings + 2 ints, callback", () => Callback<RcMixed>(BuildMixed), 185L);
        // measured 168
        yield return () => ("3 strings + 2 ints, ReadSync", () => Streaming<RcMixed>(BuildMixed), 185L);
        // measured 135
        yield return () => ("2 quoted strings, array", () => ReadArray<RcQuotedPair>(BuildQuoted), 149L);
        // measured 127
        yield return () => ("2 quoted strings, callback", () => Callback<RcQuotedPair>(BuildQuoted), 140L);
        // measured 127
        yield return () => ("2 quoted strings, ReadSync", () => Streaming<RcQuotedPair>(BuildQuoted), 140L);
    }

    [Test]
    [MethodDataSource(nameof(BudgetCases))]
    public async Task Read_Allocates_At_Most_Its_Budget_Per_Row(string name, Func<long> bytesPerRow, long budget)
    {
        long actual = bytesPerRow();
        await Assert.That(actual).IsLessThanOrEqualTo(budget).Because($"{name}: {actual} bytes per row, budget {budget}");
    }
}
