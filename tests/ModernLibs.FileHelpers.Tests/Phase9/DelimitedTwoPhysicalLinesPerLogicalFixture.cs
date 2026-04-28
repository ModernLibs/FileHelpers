using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Phase9;

/// <summary>
/// Two physical lines per logical record (<see cref="FieldInNewLineAttribute"/> on field 2) — used for
/// incomplete-tail / <see cref="ErrorMode"/> coverage.
/// </summary>
[DelimitedRecord(",")]
internal sealed class DelimitedTwoPhysicalLinesPerLogicalFixture
{
    [FieldOrder(1)]
    public string Head { get; set; } = "";

    [FieldOrder(2)]
    [FieldInNewLine]
    public string Tail { get; set; } = "";
}
