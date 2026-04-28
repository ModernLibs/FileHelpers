// MA0048: Multiple fixture types in one file is intentional for phase-scoped tests.
#pragma warning disable MA0048

namespace ModernLibs.FileHelpers.Tests.Phase27;

[DelimitedRecord("|", MultilineMode = MultilineMode.AllowForRead)]
internal sealed class Phase27MasterRecord
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2)] public string Id { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldQuoted] public string Notes { get; set; } = string.Empty;
}

[DelimitedRecord("|", MultilineMode = MultilineMode.AllowForRead)]
internal sealed class Phase27DetailRecord
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2)] public string MasterId { get; set; } = string.Empty;
    [FieldOrder(3)] public int Quantity { get; set; }
    [FieldOrder(4)] [FieldQuoted] public string Notes { get; set; } = string.Empty;
}

internal static class MasterDetailIoFixtures
{
    public static MasterDetailEngine<Phase27MasterRecord, Phase27DetailRecord> CreateEngine() =>
        new(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

    public static string CreateReadPayload(string newline) =>
        string.Join(
            newline,
            [
                "M|A|\"Master line 1",
                "Master line 2\"",
                "D|A|2|\"Detail A\"",
                "M|B|\"Master B\"",
                "D|B|7|\"Detail B line 1",
                "Detail B line 2\"",
                string.Empty,
            ]);

    public static MasterDetails<Phase27MasterRecord, Phase27DetailRecord>[] CreateGroups() =>
        [
            new(
                new Phase27MasterRecord
                {
                    Kind = "M",
                    Id = "A",
                    Notes = $"Master line 1{Environment.NewLine}Master line 2",
                },
                [
                    new Phase27DetailRecord
                    {
                        Kind = "D",
                        MasterId = "A",
                        Quantity = 2,
                        Notes = "Detail A",
                    },
                ]),
            new(
                new Phase27MasterRecord
                {
                    Kind = "M",
                    Id = "B",
                    Notes = "Master B",
                },
                [
                    new Phase27DetailRecord
                    {
                        Kind = "D",
                        MasterId = "B",
                        Quantity = 7,
                        Notes = $"Detail B line 1{Environment.NewLine}Detail B line 2",
                    },
                ]),
        ];

    public static string CreateExpectedWritePayload(string newline) =>
        string.Join(
            newline,
            [
                "M|A|\"Master line 1",
                "Master line 2\"",
                "D|A|2|\"Detail A\"",
                "M|B|\"Master B\"",
                "D|B|7|\"Detail B line 1",
                "Detail B line 2\"",
                string.Empty,
            ]);
}
