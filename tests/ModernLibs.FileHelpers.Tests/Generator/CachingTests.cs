// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernLibs.FileHelpers.Generator;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator;

[Category("Generator")]
public sealed class CachingTests
{
    private static CSharpCompilation CreateCompilation(string source) =>
        CSharpCompilation.Create(
            assemblyName: "CachingTestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(
                    typeof(ModernLibs.FileHelpers.DelimitedRecordAttribute).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>SGEN-10: Second run on unchanged source reports Cached/Unchanged on all tracked steps.</summary>
    [Test]
    [DisplayName("Generator second run reports all steps as Cached or Unchanged (SGEN-10)")]
    public async Task Generator_SecondRun_AllStepsCached()
    {
        // Arrange: Create compilation with DelimitedRecord type
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class OrderRecord
            {
                [FieldOrder(1)] public string OrderId = "";
                [FieldOrder(2)] public int Amount;
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();

        var opts = new GeneratorDriverOptions(
            disabledOutputs: IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            driverOptions: opts);

        // First run — populates the cache
        driver = driver.RunGenerators(compilation);

        // Second run on the same (cloned) compilation — must be entirely Cached
        driver = driver.RunGenerators(compilation.Clone());
        var runResult = driver.GetRunResult();

        var allOutputs = runResult.Results[0]
            .TrackedOutputSteps
            .SelectMany(static kvp => kvp.Value)
            .SelectMany(static step => step.Outputs)
            .ToList();

        // Act: Check outputs exist

        // Pipeline must have executed at least one tracked step
        // Assert: All outputs were either Cached or Unchanged
        await Assert.That(allOutputs).IsNotEmpty();

        foreach (var output in allOutputs)
        {
            var isCachedOrUnchanged = output.Reason is IncrementalStepRunReason.Cached
                or IncrementalStepRunReason.Unchanged;
            await Assert.That(isCachedOrUnchanged).IsTrue();
        }
    }
}
