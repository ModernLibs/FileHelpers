#pragma warning disable MA0048  // MA0048: Multiple types per file — intentional; this file is a compile-time fixture collection
#pragma warning disable CS0649  // CS0649: Fields are set by the FileHelpers engine via reflection/source-gen, not in code

using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Attributes;

// ---- Delimited record struct (ATTR-01 + multiple field attributes) ----
// This type exercises: DelimitedRecord, IgnoreFirst, IgnoreEmptyLines, IgnoreCommentedLines,
// FieldOrder, FieldTrim, FieldQuoted, FieldConverter, FieldNullValue, FieldOptional, FieldHidden,
// FieldNotEmpty, FieldValueDiscarded.
[DelimitedRecord(",")]
[IgnoreFirst(1)]
[IgnoreEmptyLines]
[IgnoreCommentedLines('#')]
internal record struct OrderRecord
{
    [FieldOrder(1)]
    [FieldTrim(TrimMode.Both)]
    [FieldNotEmpty]
    public string OrderId;

    [FieldOrder(2)]
    [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")]
    public DateTime OrderDate;

    [FieldOrder(3)]
    [FieldConverter(ConverterKind.Decimal)]
    [FieldNullValue(0.0)]
    public decimal Amount;

    [FieldOrder(4)]
    [FieldQuoted('"', QuoteMode.OptionalForRead)]
    [FieldTrim(TrimMode.Both, ' ', '\t')]
    public string CustomerName;

    [FieldOrder(5)]
    [FieldOptional]
    public string? Notes;

    [FieldOrder(6)]
    [FieldHidden]
    public string? InternalTag;

    [FieldOrder(7)]
    [FieldValueDiscarded]
    public string? LegacyField;
}

// ---- Fixed-length sealed class (ATTR-02 + ATTR-04 + ATTR-05) ----
[FixedLengthRecord(FixedMode.ExactLength)]
[IgnoreLast(1)]
internal sealed class FixedRecord
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Left)]
    public string? Name;

    [FieldFixedLength(8, OverflowMode = OverflowMode.Error)]
    [FieldAlign(AlignMode.Right, '0')]
    public string? Code;

    [FieldFixedLength(6)]
    [FieldConverter(ConverterKind.Date, "yyyyMM")]
    public DateTime Period;
}

// ---- Readonly record struct with fixed-length (ATTR-02 + record struct support) ----
[FixedLengthRecord]
internal readonly record struct FixedStructRecord(
    [property: FieldFixedLength(5)] string Prefix,
    [property: FieldFixedLength(20)] string Name,
    [property: FieldFixedLength(3)] string Suffix
);

// ---- Delimited record class (ATTR-01 + FieldInNewLine + FieldConverter(Type)) ----
[DelimitedRecord("|")]
[IgnoreEmptyLines(true)]
internal sealed class PipedRecord
{
    [FieldOrder(1)]
    public string? Id;

    [FieldOrder(2)]
    [FieldInNewLine]
    public string? Description;

    [FieldOrder(3)]
    [FieldConverter(typeof(object))]  // uses custom converter type overload — MLFH004 expected, suppressed in csproj for this test project
    public object? Payload;
}
