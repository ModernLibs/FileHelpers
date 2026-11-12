// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using System.Text;
using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

[DelimitedRecord(",")]
public sealed class McRow
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
}

[DelimitedRecord(",")]
[IgnoreFirst(1)]
[IgnoreLast(1)]
public sealed class McFramedRow
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
}

/// <summary>
/// A read limited by maxRecords sizes its result for the records it can return, not for every line of the input;
/// the records, the header and the footer are unchanged.
/// </summary>
[Category("ReadCore")]
public sealed class MaxRecordsCapacityTests
{
    private static string Rows(int count, string? header = null, string? footer = null)
    {
        var sb = new StringBuilder();
        if (header is not null)
            sb.Append(header).Append("\r\n");
        for (int i = 1; i <= count; i++)
            sb.Append(i).Append(",n").Append(i).Append("\r\n");
        if (footer is not null)
            sb.Append(footer).Append("\r\n");
        return sb.ToString();
    }

    [Test]
    [DisplayName("ReadString with maxRecords 1 over 100,000 lines does not allocate a result for every line")]
    public async Task MaxRecords_CapsResultCapacity()
    {
        // Arrange: warm up the engine
        var engine = new FileHelperEngine<McRow>();
        var content = Rows(100_000);
        _ = engine.ReadString(content, 1);

        // Act
        // Smallest of several windows: one noisy window (allocation-context refill, parallel GC) cannot fail it.
        McRow[] records = null!;
        long allocated = AllocationProbe.MinAllocated(() => records = engine.ReadString(content, 1));

        // Assert: one record, and far less than the 800 KB a result array for 100,000 lines takes
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Name).IsEqualTo("n1");
        await Assert.That(allocated).IsLessThan(16 * 1024);
    }

    [Test]
    [DisplayName("maxRecords: limits below, at and above the record count, and 0 or less for no limit")]
    public async Task MaxRecords_Limits()
    {
        // Arrange
        var engine = new FileHelperEngine<McRow>();
        var content = Rows(5);

        // Act and Assert
        await Assert.That(engine.ReadString(content, 2).Select(r => r.Id)).IsEquivalentTo([1, 2]);
        await Assert.That(engine.ReadString(content, 5).Length).IsEqualTo(5);
        await Assert.That(engine.ReadString(content, 50).Length).IsEqualTo(5);
        await Assert.That(engine.ReadString(content, 0).Length).IsEqualTo(5);
        await Assert.That(engine.ReadString(content, -1).Length).IsEqualTo(5);
    }

    [Test]
    [DisplayName("maxRecords with [IgnoreFirst] and [IgnoreLast]: the header and the footer are still read")]
    public async Task MaxRecords_KeepsHeaderAndFooter()
    {
        // Arrange
        var engine = new FileHelperEngine<McFramedRow>();
        var content = Rows(10, header: "id,name", footer: "total,10");

        // Act
        var records = engine.ReadString(content, 1);

        // Assert
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Id).IsEqualTo(1);
        await Assert.That(engine.HeaderText).IsEqualTo("id,name" + Environment.NewLine);
        await Assert.That(engine.FooterText).IsEqualTo("total,10" + Environment.NewLine);
    }
}
