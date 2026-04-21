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
public sealed class AfterWriteEventTests
{
    [Test]
    [DisplayName("AfterWriteRecord with modified RecordLine writes the overridden line to output")]
    public async Task AfterWriteRecord_MutatesRecordLine_WritesModifiedLine()
    {
        // Arrange: Create engine with handler that overrides RecordLine to "OVERRIDDEN"
        var engine = new FileHelperEngine<CsvOrder>();
        engine.AfterWriteRecord += (sender, e) =>
        {
            e.RecordLine = "OVERRIDDEN";
        };
        var records = new[] { new CsvOrder { OrderId = 1, CustomerName = "Alice", Amount = 100m } };

        // Act: Write record with overridden line
        var output = engine.WriteString(records);

        // Assert: Output contains the overridden text instead of the formatted record
        await Assert.That(output).Contains("OVERRIDDEN");
    }
}
