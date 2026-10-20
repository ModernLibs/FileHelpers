// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using Microsoft.CodeAnalysis;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// One bad or colliding record type must never drop the whole generator output.
/// </summary>
[Category("Generator")]
public sealed class HintNameAndRobustnessTests
{
    private static async Task AssertNoGeneratorException(GeneratorHarness.Run run)
    {
        foreach (var result in run.Result.Results)
            await Assert.That(result.Exception).IsNull();
        // CS8785: "Generator failed to generate source" — reported when AddSource / the pipeline throws.
        await Assert.That(run.Diagnostics("CS8785").Count).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Two record types with the same simple name in different namespaces both get descriptors")]
    public async Task SameSimpleName_DifferentNamespaces_BothGenerated()
    {
        // Arrange: two Customer types plus a probe that reads both
        const string source = """
            using ModernLibs.FileHelpers;
            namespace A { [DelimitedRecord(",")] public sealed class Customer { public string Name = ""; } }
            namespace B { [DelimitedRecord(";")] public sealed class Customer { public string Name = ""; public int Age; } }
            public static class Probe
            {
                public static string Run()
                {
                    var a = new FileHelperEngine<A.Customer>().ReadString("x");
                    var b = new FileHelperEngine<B.Customer>().ReadString("y;3");
                    return a[0].Name + "|" + b[0].Name + "|" + b[0].Age;
                }
            }
            """;

        // Act: run the generator and execute the probe
        var run = GeneratorHarness.RunGenerator(source);
        string output = GeneratorHarness.Execute(source);

        // Assert: no generator failure, two descriptors, both registered
        await AssertNoGeneratorException(run).ConfigureAwait(false);
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(2);
        await Assert.That(output).IsEqualTo("x|y|3");
    }

    [Test]
    [DisplayName("A nested record and a top-level record with the same name both get descriptors")]
    public async Task NestedAndTopLevel_SameName_BothGenerated()
    {
        // Arrange: Outer.Customer and Customer in the same namespace
        const string source = """
            using ModernLibs.FileHelpers;
            namespace N
            {
                [DelimitedRecord(",")] public sealed class Customer { public string Name = ""; }
                public static class Outer
                {
                    [DelimitedRecord(",")] public sealed class Customer { public int Id; }
                }
            }
            public static class Probe
            {
                public static string Run()
                {
                    var a = new FileHelperEngine<N.Customer>().ReadString("x");
                    var b = new FileHelperEngine<N.Outer.Customer>().ReadString("7");
                    return a[0].Name + "|" + b[0].Id;
                }
            }
            """;

        // Act: run the generator and execute the probe
        var run = GeneratorHarness.RunGenerator(source);
        string output = GeneratorHarness.Execute(source);

        // Assert: both descriptors generated and usable
        await AssertNoGeneratorException(run).ConfigureAwait(false);
        await Assert.That(output).IsEqualTo("x|7");
    }

    [Test]
    [DisplayName("VG6: record types whose names differ only in case both get descriptors")]
    public async Task CaseOnlyDifference_BothGenerated()
    {
        // Arrange: Rec and REC (hint names are compared case-insensitively by Roslyn)
        const string source = """
            using ModernLibs.FileHelpers;
            namespace N
            {
                [DelimitedRecord(",")] public sealed class Rec { public string Name = ""; }
                [DelimitedRecord(",")] public sealed class REC { public int Id; }
            }
            public static class Probe
            {
                public static string Run() =>
                    new FileHelperEngine<N.Rec>().ReadString("x")[0].Name + "|" + new FileHelperEngine<N.REC>().ReadString("7")[0].Id;
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);
        string output = GeneratorHarness.Execute(source);

        // Assert
        await AssertNoGeneratorException(run).ConfigureAwait(false);
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(2);
        await Assert.That(output).IsEqualTo("x|7");
    }

    [Test]
    [DisplayName("VG6: record types whose names sanitize to the same file name both get descriptors")]
    public async Task SanitizedNameCollision_BothGenerated()
    {
        // Arrange: a combining accent is not a file-name-safe character and maps to '_'
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public sealed class Rec_ { public string Name = ""; }
            [DelimitedRecord(",")] public sealed class Reć { public string Name = ""; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert
        await AssertNoGeneratorException(run).ConfigureAwait(false);
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(2);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("A type with both record attributes reports MLFH011 instead of crashing the generator")]
    public async Task BothRecordAttributes_ReportsDiagnostic()
    {
        // Arrange: one broken type and one valid type in the same compilation
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            [FixedLengthRecord]
            public sealed class Both { [FieldFixedLength(3)] public string Name = ""; }
            [DelimitedRecord(",")] public sealed class Fine { public string Name = ""; }
            """;

        // Act: run the generator
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: exactly one MLFH011, the valid type is still generated, output compiles
        await AssertNoGeneratorException(run).ConfigureAwait(false);
        await Assert.That(run.Diagnostics("MLFH011").Count).IsEqualTo(1);
        await Assert.That(run.GeneratedText).Contains("__FineDescriptor");
        await Assert.That(run.GeneratedText).DoesNotContain("__BothDescriptor");
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue();
    }

    [Test]
    [DisplayName("Rec and generic Rec<T> do not collide; the generic type reports MLFH016")]
    public async Task GenericAndNonGeneric_SameName_GenericReportsDiagnostic()
    {
        // Arrange: Rec and Rec<T> in the same namespace
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public sealed class Rec { public string Name = ""; }
            [DelimitedRecord(",")] public sealed class Rec<T> { public string Name = ""; }
            """;

        // Act: run the generator
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: Rec generated, Rec<T> rejected with a clear diagnostic, output compiles
        await AssertNoGeneratorException(run).ConfigureAwait(false);
        await Assert.That(run.Diagnostics("MLFH016").Count).IsEqualTo(1);
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(1);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("[FieldDelimiter(\"\")] with [FieldQuoted] reports MLFH012 and keeps other descriptors")]
    public async Task EmptyFieldDelimiter_WithQuoted_ReportsDiagnostic()
    {
        // Arrange: a record with an empty per-field delimiter next to a valid record
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class Broken
            {
                [FieldQuoted] public string A = "";
                [FieldDelimiter("")] public string B = "";
            }
            [DelimitedRecord(",")] public sealed class Fine { public string Name = ""; }
            """;

        // Act: run the generator
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: no generator crash, MLFH012 reported, Fine still generated
        await AssertNoGeneratorException(run).ConfigureAwait(false);
        await Assert.That(run.Diagnostics("MLFH012").Count).IsEqualTo(1);
        await Assert.That(run.GeneratedText).Contains("__FineDescriptor");
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("[DelimitedRecord(\"\")] reports MLFH012 instead of being silently dropped")]
    public async Task EmptyRecordDelimiter_ReportsDiagnostic()
    {
        // Arrange: a record with an empty record delimiter
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord("")] public sealed class Broken { public string Name = ""; }
            """;

        // Act: run the generator
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: MLFH012 reported at the attribute, nothing generated
        await AssertNoGeneratorException(run).ConfigureAwait(false);
        var diagnostics = run.Diagnostics("MLFH012");
        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].Location.IsInSource).IsTrue();
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(run.Result)).IsEqualTo(0);
    }
}
