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
public sealed class BeforeReadEventTests
{
    [Test]
    [DisplayName("BeforeReadRecord fires once for each data line (2) before parsing")]
    public async Task BeforeReadRecord_FiringCount_EqualsDataLines()
    {
        // Arrange: Create engine with BeforeReadRecord event handler counting firings
        var engine = new FileHelperEngine<CsvOrder>();
        int fireCount = 0;
        engine.BeforeReadRecord += (sender, e) => fireCount++;

        // Act: Parse input with header + 2 data rows
        engine.ReadString("Header\r\n1,Alice,100.50\r\n2,Bob,200.00");

        // Assert: Event fired exactly twice (once per data line, header excluded)
        await Assert.That(fireCount).IsEqualTo(2);
    }

    [Test]
    [DisplayName("BeforeReadRecord with SkipThisRecord=true drops the line from parsing")]
    public async Task BeforeReadRecord_SkipThisRecord_DropsLine()
    {
        // Arrange: Create engine with handler that skips line number 2
        var engine = new FileHelperEngine<CsvOrder>();
        engine.BeforeReadRecord += (sender, e) =>
        {
            if (e.LineNumber == 2) e.SkipThisRecord = true;
        };

        // Act: Parse input with 2 data rows, first will be skipped
        var records = engine.ReadString("Header\r\n1,Alice,100.50\r\n2,Bob,200.00");

        // Assert: Only one record returned (line 3, OrderId=2), line 2 was skipped
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].OrderId).IsEqualTo(2);
    }
}
