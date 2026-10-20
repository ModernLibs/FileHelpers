// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernLibs.FileHelpers.Generator;
using ModernLibs.FileHelpers.Tests.Generator.Correctness;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator;

/// <summary>
/// Tests that the generator emits correct compile-time diagnostics:
/// - MLFH001: type used with engine lacks record attribute
/// - MLFH002: [FixedLengthRecord] field missing [FieldFixedLength] — emits error, suppresses descriptor
/// - MLFH003: duplicate [FieldOrder] values in the same record
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

    // ── MLFH001 ────────────────────────────────────────────────────────────────────

    /// <summary>MLFH001 — type used with engine lacks record attribute.</summary>
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
        var diagnostics = result.Diagnostics.AddRange(ModernLibs.FileHelpers.Tests.Generator.Correctness.GeneratorHarness.AnalyzerDiagnostics(compilation));

        // Act: Find MLFH001 diagnostic
        var mfh001 = diagnostics.FirstOrDefault(d => string.Equals(d.Id, "MLFH001", StringComparison.Ordinal));

        // Assert: MLFH001 diagnostic emitted with Error severity and mentions Undecorated
        await Assert.That(mfh001).IsNotNull();
        await Assert.That(mfh001!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(mfh001.GetMessage(CultureInfo.InvariantCulture)).Contains("Undecorated");
    }

    // ── MLFH002 ────────────────────────────────────────────────────────────────────

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
        var diagnostics = result.Diagnostics.AddRange(ModernLibs.FileHelpers.Tests.Generator.Correctness.GeneratorHarness.AnalyzerDiagnostics(compilation));

        // MLFH002: generator emits the diagnostic for the unannotated field
        var mfh002 = diagnostics
            .Where(d => string.Equals(d.Id, "MLFH002", StringComparison.Ordinal))
            .ToList();

        // Act: Count MLFH002 diagnostics

        // Assert: Exactly one MLFH002 diagnostic for Symbol field
        await Assert.That(mfh002.Count).IsEqualTo(1);

        // Descriptor is suppressed — no generated file
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(result)).IsEqualTo(0);

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
        var diagnostics = result.Diagnostics.AddRange(ModernLibs.FileHelpers.Tests.Generator.Correctness.GeneratorHarness.AnalyzerDiagnostics(compilation));

        // No MLFH002 diagnostics — all fields are correctly annotated
        var mfh002 = diagnostics
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

    // ── MLFH003 ────────────────────────────────────────────────────────────────────

    /// <summary>MLFH003 — duplicate [FieldOrder] values in the same record.</summary>
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
        var diagnostics = result.Diagnostics.AddRange(ModernLibs.FileHelpers.Tests.Generator.Correctness.GeneratorHarness.AnalyzerDiagnostics(compilation));

        // Act: Find MLFH003 diagnostic
        var mfh003 = diagnostics.FirstOrDefault(d => string.Equals(d.Id, "MLFH003", StringComparison.Ordinal));

        // Assert: MLFH003 diagnostic emitted with Error severity, mentions both fields and order value
        await Assert.That(mfh003).IsNotNull();
        await Assert.That(mfh003!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        // Message must name both fields and the shared order value
        await Assert.That(mfh003.GetMessage(CultureInfo.InvariantCulture)).Contains("First");
        await Assert.That(mfh003.GetMessage(CultureInfo.InvariantCulture)).Contains("Second");
        await Assert.That(mfh003.GetMessage(CultureInfo.InvariantCulture)).Contains("1");
        // Descriptor must NOT be emitted when MFH003 fires (ambiguous layout)
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(result)).IsEqualTo(0);
    }

    // ── MLFH006 ────────────────────────────────────────────────────────────────────

    /// <summary>MLFH006: FieldConverter target type lacks parameterless constructor — emits Error.</summary>
    [Test]
    [DisplayName("Generator emits MLFH006 error when FieldConverter target type lacks parameterless constructor")]
    public async Task MLFH006_ConverterMissingParameterlessCtor_EmitsError()
    {
        // Arrange: CustomConverter extends ConverterBase but has only a parameterized constructor
        var source = """
            using ModernLibs.FileHelpers;
            using ModernLibs.FileHelpers.Converters;
            public sealed class CustomConverter : ConverterBase
            {
                public CustomConverter(string format) { }
                public override object ConvertFromString(string from) => from;
                public override string ConvertToString(object from) => from?.ToString() ?? "";
            }
            [DelimitedRecord(",")]
            public sealed class RecordWithBadConverter
            {
                [FieldOrder(1)] [FieldConverter(typeof(CustomConverter))] public string Value = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();
        var diagnostics = result.Diagnostics.AddRange(ModernLibs.FileHelpers.Tests.Generator.Correctness.GeneratorHarness.AnalyzerDiagnostics(compilation));

        var mfh006 = diagnostics.FirstOrDefault(d => string.Equals(d.Id, "MLFH006", StringComparison.Ordinal));

        await Assert.That(mfh006).IsNotNull();
        await Assert.That(mfh006!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(mfh006.GetMessage(CultureInfo.InvariantCulture)).Contains("Value");
        await Assert.That(mfh006.GetMessage(CultureInfo.InvariantCulture)).Contains("CustomConverter");
    }

    // ── MLFH007 ────────────────────────────────────────────────────────────────────

    /// <summary>MLFH007: FieldFixedLength exceeds default threshold of 1000 — emits Warning.</summary>
    [Test]
    [DisplayName("Generator emits MLFH007 warning when FieldFixedLength exceeds default threshold")]
    public async Task MLFH007_ExcessiveFieldWidth_EmitsWarning()
    {
        // Arrange: FieldFixedLength(2000) exceeds default threshold of 1000
        var source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public sealed class RecordWithWideField
            {
                [FieldOrder(1)] [FieldFixedLength(2000)] public string WideField = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();
        var diagnostics = result.Diagnostics.AddRange(ModernLibs.FileHelpers.Tests.Generator.Correctness.GeneratorHarness.AnalyzerDiagnostics(compilation));

        var mfh007 = diagnostics.FirstOrDefault(d => string.Equals(d.Id, "MLFH007", StringComparison.Ordinal));

        await Assert.That(mfh007).IsNotNull();
        await Assert.That(mfh007!.Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(mfh007.GetMessage(CultureInfo.InvariantCulture)).Contains("WideField");
        await Assert.That(mfh007.GetMessage(CultureInfo.InvariantCulture)).Contains("2000");
        await Assert.That(mfh007.GetMessage(CultureInfo.InvariantCulture)).Contains("1000");
    }

    // ── MLFH008 ────────────────────────────────────────────────────────────────────

    /// <summary>MLFH008: FieldOrder has gaps in sequence — emits Warning.</summary>
    [Test]
    [DisplayName("Generator emits MLFH008 warning when FieldOrder has gaps")]
    public async Task MLFH008_FieldOrderGaps_EmitsWarning()
    {
        // Arrange: FieldOrder values 1, 2, 5, 6 — gap between 2 and 5
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class RecordWithGaps
            {
                [FieldOrder(1)] public string First = "";
                [FieldOrder(2)] public string Second = "";
                [FieldOrder(5)] public string Fifth = "";
                [FieldOrder(6)] public string Sixth = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();
        var diagnostics = result.Diagnostics.AddRange(ModernLibs.FileHelpers.Tests.Generator.Correctness.GeneratorHarness.AnalyzerDiagnostics(compilation));

        var mfh008 = diagnostics.FirstOrDefault(d => string.Equals(d.Id, "MLFH008", StringComparison.Ordinal));

        await Assert.That(mfh008).IsNotNull();
        await Assert.That(mfh008!.Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(mfh008.GetMessage(CultureInfo.InvariantCulture)).Contains("RecordWithGaps");
        await Assert.That(mfh008.GetMessage(CultureInfo.InvariantCulture)).Contains("2");
        await Assert.That(mfh008.GetMessage(CultureInfo.InvariantCulture)).Contains("5");
    }

    /// <summary>MLFH008: Consecutive FieldOrder values — does NOT emit warning.</summary>
    [Test]
    [DisplayName("Generator does NOT emit MLFH008 when FieldOrder has no gaps")]
    public async Task MLFH008_NoGaps_NoWarning()
    {
        // Arrange: Consecutive FieldOrder values 1, 2, 3
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class RecordConsecutive
            {
                [FieldOrder(1)] public string First = "";
                [FieldOrder(2)] public string Second = "";
                [FieldOrder(3)] public string Third = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();
        var diagnostics = result.Diagnostics.AddRange(ModernLibs.FileHelpers.Tests.Generator.Correctness.GeneratorHarness.AnalyzerDiagnostics(compilation));

        var mfh008 = diagnostics.FirstOrDefault(d => string.Equals(d.Id, "MLFH008", StringComparison.Ordinal));

        await Assert.That(mfh008).IsNull();
    }

    // ── MLFH009 ────────────────────────────────────────────────────────────────────

    /// <summary>MLFH009: Record type has no public fields — emits Warning.</summary>
    [Test]
    [DisplayName("Generator emits MLFH009 warning when record type has no public fields")]
    public async Task MLFH009_NoPublicFields_EmitsWarning()
    {
        // Arrange: DelimitedRecord on a class with only private fields
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class EmptyRecord
            {
                private string _hidden = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();
        var diagnostics = result.Diagnostics.AddRange(ModernLibs.FileHelpers.Tests.Generator.Correctness.GeneratorHarness.AnalyzerDiagnostics(compilation));

        var mfh009 = diagnostics.FirstOrDefault(d => string.Equals(d.Id, "MLFH009", StringComparison.Ordinal));

        await Assert.That(mfh009).IsNotNull();
        await Assert.That(mfh009!.Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(mfh009.GetMessage(CultureInfo.InvariantCulture)).Contains("EmptyRecord");
        await Assert.That(mfh009.GetMessage(CultureInfo.InvariantCulture)).Contains("class");
    }

    /// <summary>MLFH009: Record type has public fields — does NOT emit warning.</summary>
    [Test]
    [DisplayName("Generator does NOT emit MLFH009 when record type has public fields")]
    public async Task MLFH009_HasPublicFields_NoWarning()
    {
        // Arrange: Standard DelimitedRecord with one public field
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class ValidRecord
            {
                [FieldOrder(1)] public string Name = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();
        var diagnostics = result.Diagnostics.AddRange(ModernLibs.FileHelpers.Tests.Generator.Correctness.GeneratorHarness.AnalyzerDiagnostics(compilation));

        var mfh009 = diagnostics.FirstOrDefault(d => string.Equals(d.Id, "MLFH009", StringComparison.Ordinal));

        await Assert.That(mfh009).IsNull();
    }
}
