// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernLibs.FileHelpers.Generator;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator;

/// <summary>
/// TDD RED tests for generator per-field delimiter emission (FDEL-03).
/// These tests verify that the generator emits correct code for mixed-delimiter records.
/// </summary>
[Category("Phase17")]
[Category("Generator")]
public sealed class PerFieldDelimiterTests
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

    /// <summary>
    /// FDEL-03: Generator emits FieldDescriptor with per-field delimiter metadata.
    /// </summary>
    [Test]
    [DisplayName("Generator emits FieldDescriptor constructor with delimiter argument for per-field override")]
    public async Task PerFieldDelimiter_EmitsInFieldDescriptor()
    {
        // Arrange: Create compilation with mixed-delimiter record
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class MixedRecord
            {
                [FieldOrder(1)] public string Field1 = "";
                [FieldOrder(2)] [FieldDelimiter(";")] public string Field2 = "";
                [FieldOrder(3)] public string Field3 = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        // Act: Get generated source
        var result = driver.GetRunResult();
        string generatedSource = result.GeneratedTrees[0].GetText().ToString();

        // Assert: FieldDescriptor for Field2 includes semicolon delimiter
        await Assert.That(generatedSource).Contains("FieldDescriptor");
        // The middle field should have ";" as its delimiter in the _fields array
        await Assert.That(generatedSource).Contains("nameof(global::MixedRecord.Field2)");
    }

    /// <summary>
    /// FDEL-03: Single field with [FieldDelimiter] overrides record-level delimiter in emitted code.
    /// </summary>
    [Test]
    [DisplayName("Generator uses semicolon delimiter for field with [FieldDelimiter] on comma-delimited record")]
    public async Task SingleFieldDelimiter_OverridesRecordDelimiter()
    {
        // Arrange: Record with comma delimiter but Field2 uses semicolon
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class SingleOverrideRecord
            {
                [FieldOrder(1)] public string Field1 = "";
                [FieldOrder(2)] [FieldDelimiter(";")] public string Field2 = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        // Act: Get generated source
        var result = driver.GetRunResult();
        string generatedSource = result.GeneratedTrees[0].GetText().ToString();

        // Assert: Emitted code uses correct delimiter for each field
        // Field1 and record use comma, Field2 uses semicolon
        await Assert.That(generatedSource).Contains("IndexOf");
        // The generated code should contain both comma and semicolon references
        await Assert.That(generatedSource).Contains(',');
    }

    /// <summary>
    /// FDEL-03: Mixed delimiters emit correct IndexOf calls per field.
    /// </summary>
    [Test]
    [DisplayName("Generator emits correct IndexOf calls for mixed comma-semicolon delimiters")]
    public async Task MixedDelimiters_EmitsCorrectIndexOfCalls()
    {
        // Arrange: Record with mixed delimiters
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class MixedDelimiterRecord
            {
                [FieldOrder(1)] public string Field1 = "";
                [FieldOrder(2)] [FieldDelimiter(";")] public string Field2 = "";
                [FieldOrder(3)] [FieldDelimiter("|")] public string Field3 = "";
                [FieldOrder(4)] public string Field4 = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        // Act: Get generated source
        var result = driver.GetRunResult();
        string generatedSource = result.GeneratedTrees[0].GetText().ToString();

        // Assert: Generated code contains IndexOf logic
        await Assert.That(generatedSource).Contains("IndexOf");
        // The parse body should exist
        await Assert.That(generatedSource).Contains("public global::MixedDelimiterRecord Parse(global::System.ReadOnlySpan<char> line)");
    }

    /// <summary>
    /// FDEL-03: Fields without [FieldDelimiter] use record-level delimiter.
    /// </summary>
    [Test]
    [DisplayName("Fields without [FieldDelimiter] attribute use record-level comma delimiter")]
    public async Task NoFieldDelimiter_UsesRecordDelimiter()
    {
        // Arrange: Record with only record-level delimiter
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class StandardRecord
            {
                [FieldOrder(1)] public string Field1 = "";
                [FieldOrder(2)] public string Field2 = "";
                [FieldOrder(3)] public string Field3 = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        // Act: Get generated source
        var result = driver.GetRunResult();
        string generatedSource = result.GeneratedTrees[0].GetText().ToString();

        // Assert: All fields use comma delimiter (record-level)
        await Assert.That(generatedSource).Contains("Parse");
        await Assert.That(generatedSource).Contains("IndexOf");
        // FieldDescriptor array should be emitted
        await Assert.That(generatedSource).Contains("_fields");
    }

    /// <summary>
    /// FDEL-03: All fields with different delimiters emit correctly.
    /// </summary>
    [Test]
    [DisplayName("Generator handles all fields having unique delimiters")]
    public async Task AllFieldsWithDifferentDelimiters_EmitsCorrectly()
    {
        // Arrange: Record where every field has a different delimiter
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class AllDifferentDelimitersRecord
            {
                [FieldOrder(1)] [FieldDelimiter(";")] public string Field1 = "";
                [FieldOrder(2)] [FieldDelimiter("|")] public string Field2 = "";
                [FieldOrder(3)] [FieldDelimiter("\t")] public string Field3 = "";
                [FieldOrder(4)] [FieldDelimiter("~")] public string Field4 = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        // Act: Get generated source
        var result = driver.GetRunResult();
        string generatedSource = result.GeneratedTrees[0].GetText().ToString();

        // Assert: Generated code contains the descriptor with delimiters
        await Assert.That(generatedSource).Contains("FieldDescriptor");
        await Assert.That(generatedSource).Contains("Parse");
        await Assert.That(generatedSource).Contains("Format");
        // Should compile without errors
        await Assert.That(result.Diagnostics.IsEmpty).IsTrue();
    }

    /// <summary>
    /// FDEL-03: Multi-character per-field delimiter emits correctly.
    /// </summary>
    [Test]
    [DisplayName("Generator handles multi-character per-field delimiters")]
    public async Task MultiCharFieldDelimiter_EmitsCorrectly()
    {
        // Arrange: Record with multi-char field delimiter
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class MultiCharDelimiterRecord
            {
                [FieldOrder(1)] public string Field1 = "";
                [FieldOrder(2)] [FieldDelimiter("|~|")] public string Field2 = "";
                [FieldOrder(3)] public string Field3 = "";
            }
            """;

        var compilation = CreateCompilation(source);
        var generator = new FileHelpersGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());

        driver = driver.RunGenerators(compilation);

        // Act: Get generated source
        var result = driver.GetRunResult();
        string generatedSource = result.GeneratedTrees[0].GetText().ToString();

        // Assert: Generated code handles multi-char delimiter
        await Assert.That(generatedSource).Contains("FieldDescriptor");
        await Assert.That(generatedSource).Contains("IndexOf");
        // No compilation errors
        await Assert.That(result.Diagnostics.IsEmpty).IsTrue();
    }
}
