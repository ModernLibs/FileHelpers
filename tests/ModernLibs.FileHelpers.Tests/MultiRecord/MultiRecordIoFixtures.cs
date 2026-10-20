// MA0048: Multiple fixture types are intentionally co-located.
#pragma warning disable MA0048

namespace ModernLibs.FileHelpers.Tests.MultiRecord;

[DelimitedRecord("|")]
internal sealed class MultiRecordIoHeader
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public string BatchId { get; set; } = "";
}

[DelimitedRecord("|")]
internal sealed class MultiRecordIoDetail
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public int ItemId { get; set; }
    [FieldOrder(3)] public decimal Amount { get; set; }
}

internal static class MultiRecordIoFixtures
{
    internal static readonly string CanonicalInput =
        string.Join(
            Environment.NewLine,
            "H|B001",
            "D|10|42.5",
            "D|11|7.25",
            "H|B002",
            "D|20|99.9");

    internal static readonly string CanonicalOutput =
        string.Join(
                "\r\n",
                "H|B001",
                "D|10|42.5",
                "D|11|7.25",
                "H|B002",
                "D|20|99.9")
            + "\r\n";

    internal static MultiRecordEngine CreateEngine() =>
        new(
            MultiRecordType.For<MultiRecordIoHeader>(line =>
                line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<MultiRecordIoDetail>(line =>
                line.StartsWith("D|", StringComparison.Ordinal)));
}
