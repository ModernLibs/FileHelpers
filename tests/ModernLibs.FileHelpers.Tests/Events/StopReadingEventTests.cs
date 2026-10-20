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
public sealed class StopReadingEventTests
{
    [Test]
    [DisplayName("BeforeReadRecord with StopReading=true halts iteration after first record")]
    public async Task StopReading_InBeforeReadRecord_HaltsIteration()
    {
        // Arrange: Create engine with handler that stops reading at line 3
        var engine = new FileHelperEngine<CsvOrder>();
        engine.BeforeReadRecord += (sender, e) =>
        {
            if (e.LineNumber == 3) e.StopReading = true;
        };

        // Act: Parse input with header + 3 data rows (only first data row should be read)
        var records = engine.ReadString("Header\r\n1,Alice,100.50\r\n2,Bob,200.00\r\n3,Carol,300.00");

        // Assert: Only one record returned before StopReading triggered
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].OrderId).IsEqualTo(1);
    }
}
