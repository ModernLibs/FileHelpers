// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernLibs.FileHelpers.Generator;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator;

/// <summary>Verifies that the same input always produces byte-identical output.</summary>
[Category("Generator")]
public sealed class DeterminismTests
{
    private static CSharpCompilation CreateCompilation(string source)
    {
        return CSharpCompilation.Create(
            assemblyName: "DeterminismTestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(
                    typeof(ModernLibs.FileHelpers.DelimitedRecordAttribute).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>Two generator runs on identical input produce byte-identical output.</summary>
    [Test]
    [DisplayName("Generator produces byte-identical output for identical input across two runs")]
    public async Task Generator_SameInput_ProducesByteIdenticalOutput()
    {
        // Arrange: Create compilation with DelimitedRecord type
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class OrderRecord
            {
                [FieldOrder(1)] public string OrderId = "";
                [FieldOrder(2)] public int Amount;
                [FieldOrder(3)] public decimal Total;
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();

        GeneratorDriver driver1 = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());
        GeneratorDriver driver2 = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        var result1 = driver1.RunGenerators(compilation.Clone()).GetRunResult();
        var result2 = driver2.RunGenerators(compilation.Clone()).GetRunResult();

        // MA0002: use StringComparer.Ordinal for culture-independent, deterministic ordering.
        var files1 = result1.GeneratedTrees
            .Select(static t => t.GetText().ToString())
            .OrderBy(static x => x, System.StringComparer.Ordinal)
            .ToArray();
        var files2 = result2.GeneratedTrees
            .Select(static t => t.GetText().ToString())
            .OrderBy(static x => x, System.StringComparer.Ordinal)
            .ToArray();

        // Act: Compare file counts

        // Assert: Same number of files and byte-identical content
        await Assert.That(files1.Length).IsEqualTo(files2.Length);

        for (int i = 0; i < files1.Length; i++)
        {
            await Assert.That(files1[i]).IsEqualTo(files2[i]);
        }
    }
}
