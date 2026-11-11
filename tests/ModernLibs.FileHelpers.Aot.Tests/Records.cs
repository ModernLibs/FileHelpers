using ModernLibs.FileHelpers;

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
