// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional.
#pragma warning disable MA0048

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase9;

[Category("Phase9")]
[Category("Engine")]
public sealed class BeforeWriteEventTests
{
    [Test]
    [DisplayName("BeforeWriteRecord with SkipThisRecord=true omits record from output")]
    public async Task BeforeWriteRecord_SkipThisRecord_OmitsRecord()
    {
        // Arrange: Create engine with handler that skips records with OrderId=1
        var engine = new FileHelperEngine<CsvOrder>();
        engine.BeforeWriteRecord += (sender, e) =>
        {
            if (((CsvOrder)(object)e.Record!).OrderId == 1) e.SkipThisRecord = true;
        };
        var records = new[] { new CsvOrder { OrderId = 1, CustomerName = "Alice", Amount = 100m }, new CsvOrder { OrderId = 2, CustomerName = "Bob", Amount = 200m } };

        // Act: Write records with one skipped
        var output = engine.WriteString(records);

        // Assert: Output contains Bob (OrderId=2) but not Alice (OrderId=1)
        await Assert.That(output).Contains("2,Bob,200");
        await Assert.That(output).DoesNotContain("1,Alice");
    }
}
