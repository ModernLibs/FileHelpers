using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Generator.Fixtures;

/// <summary>
/// Integration fixture: readonly record struct with positional (init-only) properties.
/// Annotated with [DelimitedRecord] so the build-time generator emits a [ModuleInitializer]
/// descriptor. Used by ModuleInitializerTests (Phase 4 additions) to verify SGEN-06.
/// </summary>
[DelimitedRecord(",")]
internal readonly record struct ReadonlyRecordStructFixture(
    [property: FieldOrder(1)] string Name,
    [property: FieldOrder(2)] int Score);
