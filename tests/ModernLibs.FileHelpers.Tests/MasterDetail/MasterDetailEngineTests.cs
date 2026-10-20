// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple fixture types live with the behavior tests that use them.
#pragma warning disable MA0048

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.MasterDetail;

[DelimitedRecord("|")]
internal sealed class MasterRecord
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public string Id { get; set; } = "";
}

[DelimitedRecord("|")]
internal sealed class DetailRecord
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public string MasterId { get; set; } = "";
    [FieldOrder(3)] public int Quantity { get; set; }
}

[Category("MasterDetail")]
public sealed class MasterDetailEngineTests
{
    [Test]
    [DisplayName("MasterDetailEngine groups detail rows under the current master")]
    public async Task ReadString_GroupsDetailsUnderMaster()
    {
        // Arrange: create an engine with prefix-based classification.
        var engine = new MasterDetailEngine<MasterRecord, DetailRecord>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: read one group with two details.
        var groups = engine.ReadString("M|A\nD|A|2\nD|A|3");

        // Assert: details are grouped under the active master.
        await Assert.That(groups.Length).IsEqualTo(1);
        await Assert.That(groups[0].Master.Id).IsEqualTo("A");
        await Assert.That(groups[0].Details.Count).IsEqualTo(2);
        await Assert.That(groups[0].Details[1].Quantity).IsEqualTo(3);
    }

    [Test]
    [DisplayName("MasterDetailEngine writes grouped records in master detail order")]
    public async Task WriteString_WritesMasterThenDetails()
    {
        // Arrange: create grouped records.
        var engine = new MasterDetailEngine<MasterRecord, DetailRecord>(_ => RecordAction.Skip);
        var group = new MasterDetails<MasterRecord, DetailRecord>(
            new MasterRecord { Kind = "M", Id = "A" },
            [
                new DetailRecord { Kind = "D", MasterId = "A", Quantity = 2 },
                new DetailRecord { Kind = "D", MasterId = "A", Quantity = 3 },
            ]);

        // Act: write the group.
        string output = engine.WriteString([group]);

        // Assert: master row is followed by detail rows.
        await Assert.That(output).IsEqualTo("M|A\r\nD|A|2\r\nD|A|3\r\n");
    }
}
