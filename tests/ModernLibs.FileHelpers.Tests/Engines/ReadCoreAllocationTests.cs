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
}
