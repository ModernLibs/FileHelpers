// MA0048: Multiple fixture types in one file is intentional for phase-scoped tests.
#pragma warning disable MA0048

namespace ModernLibs.FileHelpers.Tests.MasterDetail;

[DelimitedRecord("|", MultilineMode = MultilineMode.AllowForBoth)]
internal sealed class MasterDetailIoMaster
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2)] public string Id { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldQuoted] public string Notes { get; set; } = string.Empty;
}

[DelimitedRecord("|", MultilineMode = MultilineMode.AllowForBoth)]
internal sealed class MasterDetailIoDetail
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2)] public string MasterId { get; set; } = string.Empty;
    [FieldOrder(3)] public int Quantity { get; set; }
    [FieldOrder(4)] [FieldQuoted] public string Notes { get; set; } = string.Empty;
}

internal static class MasterDetailIoFixtures
{
    public static MasterDetailEngine<MasterDetailIoMaster, MasterDetailIoDetail> CreateEngine() =>
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

    public static MasterDetails<MasterDetailIoMaster, MasterDetailIoDetail>[] CreateGroups() =>
        [
            new(
                new MasterDetailIoMaster
                {
                    Kind = "M",
                    Id = "A",
                    Notes = $"Master line 1\r\nMaster line 2",
                },
                [
                    new MasterDetailIoDetail
                    {
                        Kind = "D",
                        MasterId = "A",
                        Quantity = 2,
                        Notes = "Detail A",
                    },
                ]),
            new(
                new MasterDetailIoMaster
                {
                    Kind = "M",
                    Id = "B",
                    Notes = "Master B",
                },
                [
                    new MasterDetailIoDetail
                    {
                        Kind = "D",
                        MasterId = "B",
                        Quantity = 7,
                        Notes = $"Detail B line 1\r\nDetail B line 2",
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
