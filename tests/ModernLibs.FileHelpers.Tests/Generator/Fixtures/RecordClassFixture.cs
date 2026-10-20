// CS0649: Properties are assigned by the FileHelpers engine via source-generated code.
#pragma warning disable CS0649

using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Generator.Fixtures;

/// <summary>
/// Integration fixture: record class with init-only properties.
/// Annotated with [DelimitedRecord] so the build-time generator emits a [ModuleInitializer]
/// descriptor. Used by ModuleInitializerTests for the record class shape.
/// </summary>
[DelimitedRecord(",")]
internal sealed record class RecordClassFixture
{
    [FieldOrder(1)] public string Name { get; init; } = string.Empty;
    [FieldOrder(2)] public int Age { get; init; }
}
