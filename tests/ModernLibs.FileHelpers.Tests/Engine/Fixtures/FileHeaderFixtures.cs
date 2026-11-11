// MA0048: Multiple fixture types per file — intentional; groups the GetFileHeader fixtures.
#pragma warning disable MA0048
// MA0025: Fixture bodies are populated by source-generated code.
#pragma warning disable MA0025
// CS0649: Fields are assigned by the source-generated descriptor, not in user code.
#pragma warning disable CS0649

using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Engine.Fixtures;

[DelimitedRecord(",")]
internal sealed class HeaderD1
{
    [FieldCaption("Order, ID")] public int Id;
    [FieldQuoted] public string Name = string.Empty;
    public decimal Amount;
}

[DelimitedRecord(",")]
internal sealed class HeaderD2
{
    [FieldDelimiter("|")] public int A;
    public string B = string.Empty;
    [FieldCaption("Sea")] public string C = string.Empty;
}

[DelimitedRecord(",")]
internal sealed class HeaderD3
{
    public int A;
    [FieldDelimiter("|")] public string B = string.Empty;
    public string C = string.Empty;
}

[DelimitedRecord(";")]
internal sealed class HeaderP1
{
    public int Id;
    [FieldCaption("N")] public string Name = string.Empty;
    [FieldHidden] public string H = string.Empty;
}

[DelimitedRecord(",")]
internal class HeaderBase
{
    [FieldCaption("Base Id")] public int BaseId;
    public string BaseName = string.Empty;
}

[DelimitedRecord(",")]
internal sealed class HeaderDerived : HeaderBase
{
    public string Own = string.Empty;
    [FieldCaption("Own 2")] public string Own2 = string.Empty;
}

[DelimitedRecord(",")]
internal sealed class HeaderOrdered
{
    [FieldOrder(3)] [FieldCaption("3rd")] public string C = string.Empty;
    [FieldOrder(1)] [FieldDelimiter("|")] public int First;
    [FieldOrder(2)] [FieldCaption("Second")] public string B = string.Empty;
}

[DelimitedRecord("||")]
internal sealed class HeaderMulti
{
    public int A;
    [FieldCaption("B b")] public string B = string.Empty;
    public string C = string.Empty;
}

[DelimitedRecord(",")]
internal sealed class HeaderEmptyCaption
{
    [FieldCaption("")] public int A;
    public string B = string.Empty;
}

[FixedLengthRecord]
internal sealed class HeaderF1
{
    [FieldFixedLength(5)] public int Id;
    [FieldFixedLength(10)] [FieldCaption("Full Name")] public string Name = string.Empty;
}
