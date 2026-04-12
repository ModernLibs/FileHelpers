// CS0649: Fields are assigned by the FileHelpers engine via source-generated code, not in user code.
#pragma warning disable CS0649

using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Generator.Fixtures;

/// <summary>
/// Minimal [DelimitedRecord] type used in generator integration tests.
/// The source generator processes this type at compile time and emits a [ModuleInitializer]
/// that registers the descriptor in FileHelperRegistry&lt;SimpleDelimitedRecord&gt;.
/// </summary>
[DelimitedRecord(",")]
internal sealed class SimpleDelimitedRecord
{
    [FieldOrder(1)] public string Symbol = string.Empty;
    [FieldOrder(2)] public int Quantity;
    [FieldOrder(3)] public decimal Price;
}
