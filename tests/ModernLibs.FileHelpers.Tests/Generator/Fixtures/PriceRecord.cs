using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Generator.Fixtures;

/// <summary>
/// Minimal readonly record struct with no string fields.
/// Uses primary constructor syntax so the source-generated Parse body can use
/// positional constructor syntax: <c>new PriceRecord(__v0, __v1)</c>.
/// Used to verify zero per-record heap allocations for struct types.
/// </summary>
[DelimitedRecord(",")]
internal readonly record struct PriceRecord(
    [property: FieldOrder(1)] int Quantity,
    [property: FieldOrder(2)] decimal Price);
