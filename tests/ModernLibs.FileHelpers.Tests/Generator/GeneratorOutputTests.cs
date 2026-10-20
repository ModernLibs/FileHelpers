// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernLibs.FileHelpers.Generator;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator;

[Category("Generator")]
public sealed class GeneratorOutputTests
{
    private static CSharpCompilation CreateCompilation(string source)
    {
        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(
                    typeof(ModernLibs.FileHelpers.DelimitedRecordAttribute).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>ForAttributeWithMetadataName discovers the annotated type and pipeline runs.</summary>
    [Test]
    [DisplayName("Generator discovers DelimitedRecord type and pipeline produces one output file")]
    public async Task Generator_DiscoverDelimitedRecord_PipelineRuns()
    {
        // Arrange: Create compilation with DelimitedRecord-annotated type
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class Trade
            {
                [FieldOrder(1)] public string Symbol = "";
                [FieldOrder(2)] public decimal Price;
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        // Act: Get generator results
        var result = driver.GetRunResult();

        // Assert: No diagnostics and exactly one generated file for Trade type
        await Assert.That(result.Diagnostics.IsEmpty).IsTrue();
        // pipeline ran and produced exactly one generated file for the Trade type
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
    }

    /// <summary>Emitted descriptor compiles and contains Parse/Format stubs.</summary>
    [Test]
    [DisplayName("Generator emits descriptor implementing IRecyclableDescriptor with Parse and Format stubs")]
    public async Task Generator_EmittedDescriptor_CompilesWithParseAndFormatStubs()
    {
        // Arrange: Create compilation with DelimitedRecord-annotated type
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class Trade
            {
                [FieldOrder(1)] public string Symbol = "";
                [FieldOrder(2)] public decimal Price;
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        // Act: Get generator results
        var result = driver.GetRunResult();

        // Assert: One generated file with expected content
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);

        string generatedSource = result.GeneratedTrees[0].GetText().ToString();

        // Assertions on the emitted source text.
        // Trade is a class with mutable (non-init-only) fields, so the generator emits
        // IRecyclableDescriptor<T> (which extends IRecordDescriptor<T>) for RecycleRecords.
        await Assert.That(generatedSource).Contains("file sealed class __TradeDescriptor");
        await Assert.That(generatedSource).Contains("IRecyclableDescriptor<global::Trade>");
        await Assert.That(generatedSource).Contains("writer.Advance");
        await Assert.That(generatedSource).Contains("ModuleInitializer");
        await Assert.That(generatedSource).Contains("FileHelperRegistry");
        await Assert.That(generatedSource).Contains("SetDescriptor");
        await Assert.That(generatedSource).Contains("#pragma warning disable CA2255");
    }
}
