// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernLibs.FileHelpers.Generator;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// VG1: validation diagnostics come from <c>FileHelpersAnalyzer</c> (real locations, <c>#pragma</c> support);
/// the generator never reports a diagnostic bound to a syntax tree, so an incremental re-run after an edit
/// (the IDE case) cannot fail with "SyntaxTree is not part of the compilation".
/// </summary>
[Category("Generator")]
public sealed class IncrementalDiagnosticsTests
{
    private const string RecordSource = """
        using ModernLibs.FileHelpers;
        namespace N;
        [DelimitedRecord(",")]
        public sealed class R
        {
            [FieldOrder(1)] public int A;
            [FieldOrder(5)] public int B;
        }
        public sealed class NotRec { public int X; }
        public static class Use
        {
            public static object Make() => new FileHelperEngine<NotRec>();
        }
        """;

    [Test]
    [DisplayName("VG1: re-running the driver after editing a file with diagnostics does not throw")]
    public async Task EditAfterDiagnostic_IncrementalRun_DoesNotThrow()
    {
        // Arrange: a record with MLFH008 and an MLFH001 usage, plus an unrelated file
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var recordTree = CSharpSyntaxTree.ParseText(RecordSource, parse, path: "R.cs");
        var otherTree = CSharpSyntaxTree.ParseText("namespace N; public class Other { }", parse, path: "Other.cs");
        var compilation = CSharpCompilation.Create("Inc", [recordTree, otherTree], GeneratorHarness.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new FileHelpersGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGenerators(compilation);

        // Act: edit the record file below the diagnostics (same spans, new tree) and re-run twice
        var edited = compilation.ReplaceSyntaxTree(recordTree,
            CSharpSyntaxTree.ParseText(RecordSource + "\n// trailing comment\n", parse, path: "R.cs"));
        Exception? failure = null;
        GeneratorDriverRunResult? result = null;
        try
        {
            driver = driver.RunGenerators(edited);
            driver = driver.RunGeneratorsAndUpdateCompilation(edited, out _, out _);
            result = driver.GetRunResult();
        }
        catch (ArgumentException ex)
        {
            failure = ex;
        }

        // Assert: no exception, and no generator diagnostic is bound to a syntax tree
        await Assert.That(failure).IsNull();
        await Assert.That(result!.Results[0].Exception).IsNull();
        await Assert.That(result.Diagnostics.Any(static d => d.Location.SourceTree is not null)).IsFalse();
    }

    [Test]
    [DisplayName("VG1: the analyzer reports validation diagnostics at the source location")]
    public async Task Analyzer_ReportsInSourceLocations()
    {
        // Act: run generator + analyzer
        var run = GeneratorHarness.RunGenerator(RecordSource);

        // Assert: MLFH008 at B (line 7, zero-based 6), MLFH001 at the type argument (line 12, zero-based 11)
        var gap = run.Diagnostics("MLFH008").Single();
        var missing = run.Diagnostics("MLFH001").Single();
        await Assert.That(gap.Location.IsInSource).IsTrue();
        await Assert.That(gap.Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(6);
        await Assert.That(missing.Location.IsInSource).IsTrue();
        await Assert.That(missing.Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(11);
    }

    [Test]
    [DisplayName("VG1: #pragma warning disable suppresses a validation warning")]
    public async Task Pragma_SuppressesWarning()
    {
        // Arrange: the MLFH008 record wrapped in a pragma
        const string source = """
            using ModernLibs.FileHelpers;
            #pragma warning disable MLFH008
            [DelimitedRecord(",")]
            public sealed class R
            {
                [FieldOrder(1)] public int A;
                [FieldOrder(5)] public int B;
            }
            #pragma warning restore MLFH008
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: suppressed, descriptor still generated
        await Assert.That(run.Diagnostics("MLFH008").Count).IsEqualTo(0);
        await Assert.That(run.GeneratedText).Contains("__RDescriptor");
    }
}
