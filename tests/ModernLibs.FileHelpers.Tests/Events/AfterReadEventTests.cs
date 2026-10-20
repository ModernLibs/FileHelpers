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
public sealed class AfterReadEventTests
{
    [Test]
    [DisplayName("AfterReadRecord fires once for each successfully parsed record")]
    public async Task AfterReadRecord_FiringCount_EqualsSuccessfulParses()
    {
        // Arrange: Create engine with AfterReadRecord event handler counting firings
        var engine = new FileHelperEngine<CsvOrder>();
        int fireCount = 0;
        engine.AfterReadRecord += (sender, e) => fireCount++;

        // Act: Parse input with header + 2 data rows
        engine.ReadString("Header\r\n1,Alice,100.50\r\n2,Bob,200.00");

        // Assert: Event fired exactly twice (once per successful parse)
        await Assert.That(fireCount).IsEqualTo(2);
    }

    [Test]
    [DisplayName("AfterReadRecord with SkipThisRecord=true excludes the parsed record from results")]
    public async Task AfterReadRecord_SkipThisRecord_ExcludesParsedRecord()
    {
        // Arrange: Create engine with handler that skips records with OrderId=1
        var engine = new FileHelperEngine<CsvOrder>();
        engine.AfterReadRecord += (sender, e) =>
        {
            if (((CsvOrder)(object)e.Record!).OrderId == 1) e.SkipThisRecord = true;
        };

        // Act: Parse input with 2 records, one will be skipped
        var records = engine.ReadString("Header\r\n1,Alice,100.50\r\n2,Bob,200.00");

        // Assert: Only one record returned (OrderId=2), OrderId=1 was skipped
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].OrderId).IsEqualTo(2);
    }
}
