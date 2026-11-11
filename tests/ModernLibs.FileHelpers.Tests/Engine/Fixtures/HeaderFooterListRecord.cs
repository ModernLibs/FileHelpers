namespace ModernLibs.FileHelpers.Tests.Engine.Fixtures;

/// <summary>Both [IgnoreFirst(1)] (header) and [IgnoreLast(1)] (footer).</summary>
[DelimitedRecord(",")]
[IgnoreFirst(1)]
[IgnoreLast(1)]
internal sealed class HeaderFooterListRecord
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public string Name = string.Empty;
}
