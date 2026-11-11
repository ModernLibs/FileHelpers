using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Converters;

namespace ModernLibs.FileHelpers.Aot.Tests;

[DelimitedRecord(",")]
internal sealed class Person
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public string Name = string.Empty;
    [FieldOrder(3)] public decimal Balance;
}

[DelimitedRecord(",")]
internal sealed class Note
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] [FieldQuoted] public string Text = string.Empty;
}

[FixedLengthRecord]
internal readonly record struct Item(
    [property: FieldFixedLength(5)] string Code,
    [property: FieldFixedLength(10)] string Name,
    [property: FieldFixedLength(3)] string Suffix);

internal enum Level
{
    Low,
    High,
}

/// <summary>Custom 3.x converter: the field is written in upper case and read back in lower case.</summary>
internal sealed class CaseConverter : ConverterBase
{
    public override object? StringToField(string text) => text.ToLowerInvariant();

    public override string? FieldToString(object? field) => ((string?)field)?.ToUpperInvariant();
}

/// <summary>Enum, formatted date, nullable int, custom converter and double: the generated code of those paths.</summary>
[DelimitedRecord(",")]
internal sealed class Wide
{
    [FieldOrder(1)] public Level Level;
    [FieldOrder(2)] [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] public DateTime Day;
    [FieldOrder(3)] public int? Count;
    [FieldOrder(4)] [FieldConverter(typeof(CaseConverter))] public string Tag = string.Empty;
    [FieldOrder(5)] public double Ratio;
}
