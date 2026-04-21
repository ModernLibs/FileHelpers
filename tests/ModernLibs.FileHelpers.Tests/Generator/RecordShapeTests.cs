// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernLibs.FileHelpers.Generator;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator;

/// <summary>
/// Tests that the generator correctly handles all four record shapes:
/// - SGEN-04: sealed class with public fields
/// - SGEN-05: record class with init properties (no EqualityContract leak)
/// - SGEN-06: readonly record struct with positional parameters
/// </summary>
[Category("Generator")]
public sealed class RecordShapeTests
{
    private static CSharpCompilation CreateCompilation(string source)
    {
        // System.Runtime must be included so attribute ConstructorArguments with int/string
        // types resolve correctly during Roslyn semantic analysis.
        string runtimeDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return CSharpCompilation.Create(
            assemblyName: "RecordShapeTestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(
                    typeof(ModernLibs.FileHelpers.DelimitedRecordAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(
                    System.IO.Path.Combine(runtimeDir, "System.Runtime.dll")),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>SGEN-04: Sealed class with public fields generates correct descriptor.</summary>
    [Test]
    [DisplayName("Generator produces descriptor for sealed class with public fields (SGEN-04)")]
    public async Task SGEN04_SealedClass_PublicFields_GeneratesDescriptor()
    {
        // Arrange: Create compilation with sealed class having public fields
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class TradeRecord
            {
                [FieldOrder(1)] public string Symbol = "";
                [FieldOrder(2)] public decimal Price;
                [FieldOrder(3)] public int Quantity;
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        // Act: Get generator results
        var result = driver.GetRunResult();

        // SGEN-04: pipeline produced exactly one descriptor for the sealed class
        // Assert: One generated file with expected type and field names
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);

        string generated = result.GeneratedTrees[0].GetText().ToString();
        await Assert.That(generated).Contains("__TradeRecordDescriptor");
        // Should have 3 fields: Symbol, Price, Quantity
        await Assert.That(generated).Contains("Symbol");
        await Assert.That(generated).Contains("Price");
        await Assert.That(generated).Contains("Quantity");
    }

    /// <summary>SGEN-05: Record class with init properties does NOT produce EqualityContract field.</summary>
    [Test]
    [DisplayName("Generator excludes EqualityContract from record class descriptor (SGEN-05)")]
    public async Task SGEN05_RecordClass_InitProperties_ExcludesEqualityContract()
    {
        // Arrange: Create compilation with positional record class
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed record PersonRecord(
                [property: FieldOrder(1)] string Name,
                [property: FieldOrder(2)] int Age);
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        // Act: Get generator results
        var result = driver.GetRunResult();

        // SGEN-05: pipeline ran
        // Assert: One generated file without EqualityContract but with user fields
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);

        string generated = result.GeneratedTrees[0].GetText().ToString();
        // EqualityContract must NOT appear in the generated descriptor
        await Assert.That(generated).DoesNotContain("EqualityContract");
        // The actual user-defined fields must be present
        await Assert.That(generated).Contains("Name");
        await Assert.That(generated).Contains("Age");
    }

    /// <summary>SGEN-06: Readonly record struct with positional params generates descriptor.</summary>
    [Test]
    [DisplayName("Generator produces descriptor for readonly record struct with positional params (SGEN-06)")]
    public async Task SGEN06_ReadonlyRecordStruct_PositionalParams_GeneratesDescriptor()
    {
        // Arrange: Create compilation with readonly record struct
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public readonly record struct PricePoint(
                [property: FieldOrder(1)] string Symbol,
                [property: FieldOrder(2)] decimal Price);
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        // Act: Get generator results
        var result = driver.GetRunResult();

        // SGEN-06: pipeline produced one descriptor for the readonly record struct
        // Assert: One generated file with expected type and fields, no EqualityContract
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);

        string generated = result.GeneratedTrees[0].GetText().ToString();
        await Assert.That(generated).Contains("__PricePointDescriptor");
        await Assert.That(generated).Contains("Symbol");
        await Assert.That(generated).Contains("Price");
        // EqualityContract must NOT appear
        await Assert.That(generated).DoesNotContain("EqualityContract");
    }
}
