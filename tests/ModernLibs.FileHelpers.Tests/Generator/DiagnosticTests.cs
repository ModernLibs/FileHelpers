// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernLibs.FileHelpers.Generator;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator;

/// <summary>
/// Tests that the generator emits correct compile-time diagnostics:
/// - MLFH001: type used with engine lacks record attribute (RED — implemented in Plan 04-03)
/// - MLFH002: [FixedLengthRecord] field missing [FieldFixedLength] — emits error, suppresses descriptor
/// - MLFH003: duplicate [FieldOrder] values in the same record (RED — implemented in Plan 04-03)
/// </summary>
[Category("Generator")]
public sealed class DiagnosticTests
{
    private static CSharpCompilation CreateCompilation(string source)
    {
        // System.Runtime must be included alongside typeof(object) so that attribute
        // ConstructorArguments with int/string types resolve correctly during Roslyn analysis.
        // Without System.Runtime, Roslyn cannot resolve built-in types and returns
        // ConstructorArguments.Length == 0 for attributes with int parameters.
        string runtimeDir = System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return CSharpCompilation.Create(
            assemblyName: "DiagnosticTestAssembly",
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

    // ── MLFH001 (RED until Plan 04-03) ───────────────────────────────────────────

    /// <summary>SGEN-07: MLFH001 — type used with engine lacks record attribute.</summary>
    [Test]
    [DisplayName("Generator emits MLFH001 diagnostic when type lacks record attribute")]
    public async Task MFH001_TypeLacksRecordAttribute_EmitsDiagnostic()
    {
        // Arrange: Create compilation with undecorated type used with FileHelperRegistry
        var source = """
            using ModernLibs.FileHelpers;
            public sealed class Undecorated
            {
                public string Name = "";
            }
            public static class Program
            {
                public static void Run()
                {
                    _ = FileHelperRegistry<Undecorated>.Descriptor;
                }
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();

        // Act: Find MLFH001 diagnostic
        var mfh001 = result.Diagnostics.FirstOrDefault(d => string.Equals(d.Id, "MLFH001", StringComparison.Ordinal));

        // Assert: MLFH001 diagnostic emitted with Error severity and mentions Undecorated
        await Assert.That(mfh001).IsNotNull();
        await Assert.That(mfh001!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(mfh001.GetMessage(CultureInfo.InvariantCulture)).Contains("Undecorated");
    }

    // ── MLFH002 (GREEN — implemented in Plan 04-02) ──────────────────────────────

    /// <summary>MLFH002: FixedLengthRecord with a field missing [FieldFixedLength] emits MLFH002 and suppresses descriptor.</summary>
    [Test]
    [DisplayName("Generator emits MLFH002 and suppresses descriptor when FixedLengthRecord field lacks FieldFixedLength")]
    public async Task MFH002_FixedLengthRecord_FieldMissingFixedLength_EmitsDiagnosticAndSuppressesDescriptor()
    {
        // Arrange: Create compilation with FixedLengthRecord where Symbol field lacks [FieldFixedLength]
        var source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public sealed class BrokenFixed
            {
                // Symbol has no [FieldFixedLength] — should trigger MFH002
                [FieldOrder(1)] public string Symbol = "";
                [FieldOrder(2)] [FieldFixedLength(10)] public string Code = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();

        // MLFH002: generator emits the diagnostic for the unannotated field
        var mfh002 = result.Diagnostics
            .Where(d => string.Equals(d.Id, "MLFH002", StringComparison.Ordinal))
            .ToList();

        // Act: Count MLFH002 diagnostics

        // Assert: Exactly one MLFH002 diagnostic for Symbol field
        await Assert.That(mfh002.Count).IsEqualTo(1);

        // Descriptor is suppressed — no generated file
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);

        // Diagnostic message identifies the specific field
        await Assert.That(mfh002[0].GetMessage(CultureInfo.InvariantCulture)).Contains("Symbol");
    }

    /// <summary>MLFH002: FixedLengthRecord with all fields annotated does NOT emit MLFH002 and generates descriptor.</summary>
    [Test]
    [DisplayName("Generator produces descriptor with no MLFH002 when all FixedLengthRecord fields have FieldFixedLength")]
    public async Task MFH002_FixedLengthRecord_AllFieldsAnnotated_NoMFH002Diagnostic()
    {
        // Arrange: Create compilation with properly annotated FixedLengthRecord
        var source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public sealed class ValidFixed
            {
                [FieldOrder(1)] [FieldFixedLength(10)] public string Symbol = "";
                [FieldOrder(2)] [FieldFixedLength(8)]  public string Code = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();

        // No MLFH002 diagnostics — all fields are correctly annotated
        var mfh002 = result.Diagnostics
            .Where(d => string.Equals(d.Id, "MLFH002", StringComparison.Ordinal))
            .ToList();

        // Act: Count MLFH002 diagnostics

        // Assert: No MLFH002 diagnostics and descriptor was generated
        await Assert.That(mfh002.Count).IsEqualTo(0);

        // Descriptor is generated
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);

        string generated = result.GeneratedTrees[0].GetText().ToString();
        await Assert.That(generated).Contains("__ValidFixedDescriptor");
    }

    // ── MLFH003 (RED until Plan 04-03) ───────────────────────────────────────────

    /// <summary>SGEN-09: MLFH003 — duplicate [FieldOrder] values in the same record.</summary>
    [Test]
    [DisplayName("Generator emits MLFH003 and suppresses descriptor for duplicate FieldOrder values")]
    public async Task MFH003_DuplicateFieldOrder_EmitsDiagnosticAndSuppressesDescriptor()
    {
        // Arrange: Create compilation with duplicate [FieldOrder(1)] values
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class AmbiguousRecord
            {
                [FieldOrder(1)] public string First = "";
                [FieldOrder(1)] public string Second = "";  // duplicate!
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();

        // Act: Find MLFH003 diagnostic
        var mfh003 = result.Diagnostics.FirstOrDefault(d => string.Equals(d.Id, "MLFH003", StringComparison.Ordinal));

        // Assert: MLFH003 diagnostic emitted with Error severity, mentions both fields and order value
        await Assert.That(mfh003).IsNotNull();
        await Assert.That(mfh003!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        // Message must name both fields and the shared order value
        await Assert.That(mfh003.GetMessage(CultureInfo.InvariantCulture)).Contains("First");
        await Assert.That(mfh003.GetMessage(CultureInfo.InvariantCulture)).Contains("Second");
        await Assert.That(mfh003.GetMessage(CultureInfo.InvariantCulture)).Contains("1");
        // Descriptor must NOT be emitted when MFH003 fires (ambiguous layout)
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
    }
}
