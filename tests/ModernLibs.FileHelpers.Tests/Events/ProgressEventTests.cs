// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional.
#pragma warning disable MA0048

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Events;

[Category("Engine")]
public sealed class ProgressEventTests
{
    [Test]
    [DisplayName("Progress event fires once per parsed record with CurrentRecord incrementing from 1")]
    public async Task Progress_FiresOncePerParsedRecord_WithCorrectCurrentRecord()
    {
        // Arrange: Create engine with Progress event handler capturing CurrentRecord values
        var engine = new FileHelperEngine<CsvOrder>();
        var progressValues = new List<int>();
        engine.Progress += (sender, e) => progressValues.Add(e.CurrentRecord);

        // Act: Parse input with header + 2 data rows
        engine.ReadString("Header\r\n1,Alice,100.50\r\n2,Bob,200.00");

        // Assert: Progress fired twice with CurrentRecord=1 then CurrentRecord=2
        await Assert.That(progressValues.Count).IsEqualTo(2);
        await Assert.That(progressValues[0]).IsEqualTo(1);
        await Assert.That(progressValues[1]).IsEqualTo(2);
    }

    [Test]
    [DisplayName("Progress event TotalRecords equals -1 in batch mode (unknown total)")]
    public async Task Progress_TotalRecords_IsMinusOneForBatchMode()
    {
        // Arrange: Create engine with Progress event handler capturing TotalRecords
        var engine = new FileHelperEngine<CsvOrder>();
        int? totalSeen = null;
        engine.Progress += (sender, e) => totalSeen = e.TotalRecords;

        // Act: Parse single record input
        engine.ReadString("Header\r\n1,Alice,100.50");

        // Assert: TotalRecords is -1 (unknown in batch/streaming mode)
        await Assert.That(totalSeen).IsEqualTo(-1);
    }
}
