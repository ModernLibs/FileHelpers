// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple fixture types live with the behavior tests that use them.
#pragma warning disable MA0048

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase22;

[DelimitedRecord("|")]
internal sealed class MultiHeader
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public string Batch { get; set; } = "";
}

[DelimitedRecord("|")]
internal sealed class MultiDetail
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public int Id { get; set; }
    [FieldOrder(3)] public decimal Amount { get; set; }
}

[Category("Phase22")]
[Category("MultiRecord")]
public sealed class MultiRecordEngineTests
{
    [Test]
    [DisplayName("MultiRecordEngine reads mixed records with explicit selectors")]
    public async Task ReadString_MixedRecords_UsesSelectors()
    {
        // Arrange: create a closed set of generated record descriptors.
        var engine = new MultiRecordEngine(
            MultiRecordType.For<MultiHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<MultiDetail>(line => line.StartsWith("D|", StringComparison.Ordinal)));

        // Act: read a file that contains both record types.
        var records = engine.ReadString("H|B001\nD|10|42.5");

        // Assert: each line is dispatched to the matching generated descriptor.
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0]).IsTypeOf<MultiHeader>();
        await Assert.That(((MultiDetail)records[1]).Amount).IsEqualTo(42.5m);
    }

    [Test]
    [DisplayName("MultiRecordEngine writes mixed records using generated descriptors")]
    public async Task WriteString_MixedRecords_FormatsEachType()
    {
        // Arrange: create a multi-record engine and mixed record sequence.
        var engine = new MultiRecordEngine(
            MultiRecordType.For<MultiHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<MultiDetail>(line => line.StartsWith("D|", StringComparison.Ordinal)));
        object[] records =
        [
            new MultiHeader { Kind = "H", Batch = "B001" },
            new MultiDetail { Kind = "D", Id = 10, Amount = 42.5m },
        ];

        // Act: write both record types.
        string output = engine.WriteString(records);

        // Assert: output preserves mixed record order.
        await Assert.That(output).IsEqualTo($"H|B001{Environment.NewLine}D|10|42.5{Environment.NewLine}");
    }
}
