#pragma warning disable MA0048  // MA0048: Multiple types per file — intentional; this file is a compile-time fixture collection
#pragma warning disable CS0649  // CS0649: Fields are set by the FileHelpers engine via reflection/source-gen, not in code

using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Attributes;

// ---- Delimited record struct (multiple field attributes) ----
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
    [FieldOptional] // fields after an optional field must be optional (3.x rule, MLFH018)
    [FieldValueDiscarded]
    public string? LegacyField;
}

// ---- Fixed-length sealed class ----
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

// ---- Readonly record struct with fixed-length (record struct support) ----
[FixedLengthRecord]
internal readonly record struct FixedStructRecord(
    [property: FieldFixedLength(5)] string Prefix,
    [property: FieldFixedLength(20)] string Name,
    [property: FieldFixedLength(3)] string Suffix
);

// ---- Delimited record class (FieldInNewLine + FieldConverter(Type)) ----
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
    [FieldConverter(typeof(PipedPayloadConverter))]  // the custom converter type overload
    public string? Payload;
}

/// <summary>Custom converter for <see cref="PipedRecord"/>.</summary>
internal sealed class PipedPayloadConverter : ModernLibs.FileHelpers.Converters.ConverterBase
{
    public override object? StringToField(string text) => text;
}
