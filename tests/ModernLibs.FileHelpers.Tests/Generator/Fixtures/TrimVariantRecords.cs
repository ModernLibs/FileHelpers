// CS0649: Fields are assigned by the FileHelpers engine via source-generated code, not in user code.
#pragma warning disable CS0649
// MA0048: Multiple small fixture types per file — intentional grouping of TrimMode variants.
#pragma warning disable MA0048

using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Generator.Fixtures;

/// <summary>
/// Phase 7 Wave 2 fixture: [FieldTrim(TrimMode.Left)] on a single string field.
/// Exercises D-05 trim emission for leading-only whitespace.
/// </summary>
[DelimitedRecord(",")]
internal sealed class LeftTrimmedRecord
{
    [FieldOrder(1)]
    [FieldTrim(TrimMode.Left)]
    public string Name = string.Empty;
}

/// <summary>
/// Phase 7 Wave 2 fixture: [FieldTrim(TrimMode.Right)] on a single string field.
/// Exercises D-05 trim emission for trailing-only whitespace.
/// </summary>
[DelimitedRecord(",")]
internal sealed class RightTrimmedRecord
{
    [FieldOrder(1)]
    [FieldTrim(TrimMode.Right)]
    public string Name = string.Empty;
}
