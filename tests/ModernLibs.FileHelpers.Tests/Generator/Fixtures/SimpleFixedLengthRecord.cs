// CS0649: Fields are assigned by the FileHelpers engine via source-generated code, not in user code.
#pragma warning disable CS0649

using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Generator.Fixtures;

/// <summary>
/// Integration fixture: [FixedLengthRecord] type used in Phase 4 generator tests.
/// When Phase 4 implements the FixedLength pipeline arm, the build-time generator will emit
/// a [ModuleInitializer] descriptor for this type (SGEN-08 integration).
/// </summary>
[FixedLengthRecord]
internal sealed class SimpleFixedLengthRecord
{
    [FieldOrder(1)] [FieldFixedLength(10)] public string Name = string.Empty;
    [FieldOrder(2)] [FieldFixedLength(5)] public int Code;
}
