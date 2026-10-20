// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixtures kept next to the tests that use them.
#pragma warning disable MA0048
// CA1051: Public fields are the FileHelpers record mapping surface.
#pragma warning disable CA1051
// CS0649: Public fields assigned by source-generated descriptor.
#pragma warning disable CS0649

using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Generator;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Errors;

/// <summary>Delimited record exercising [FieldNullValue] on several field kinds.</summary>
[DelimitedRecord(",")]
public sealed class NullValueRecord
{
    [FieldNullValue(-1)] public int Count;
    [FieldNullValue(true)] public bool Active;
    [FieldNullValue(2.5)] public decimal Price;
    [FieldNullValue("n/a")] public string Note = "";
    [FieldConverter(ConverterKind.Int32)][FieldNullValue(7)] public int Converted;
    [FieldNullValue(10)] public long Wide;
}

/// <summary>Fixed-length record exercising [FieldNullValue].</summary>
[FixedLengthRecord]
public sealed class FixedNullValueRecord
{
    [FieldFixedLength(4)][FieldNullValue(99)] public int Code;
    [FieldFixedLength(3)] public string Name = "";
}

/// <summary>Custom converter fixture: [FieldNullValue] short-circuits empty input before the converter.</summary>
[DelimitedRecord(",")]
public sealed class CustomConverterNullValueRecord
{
    public string Name = "";
    [FieldConverter(typeof(ThrowingConverter))][FieldNullValue(42)] public int Plain;
}

/// <summary>
/// <see cref="FieldNullValueAttribute"/> supplies the value of an empty / whitespace-only field
/// (FileHelpers 3.x). Assertions are on engine reads, not attribute construction.
/// </summary>
[Category("Errors")]
public sealed class FieldNullValueTests
{
    [Test]
    [DisplayName("Delimited: empty fields take their [FieldNullValue] (int, bool, decimal, converter, long)")]
    public async Task Delimited_EmptyFields_UseNullValue()
    {
        // Arrange
        var engine = new FileHelperEngine<NullValueRecord>();

        // Act: every field empty
        var record = engine.ReadString(",,,,,")[0];

        // Assert
        await Assert.That(record.Count).IsEqualTo(-1);
        await Assert.That(record.Active).IsTrue();
        await Assert.That(record.Price).IsEqualTo(2.5m);
        await Assert.That(record.Converted).IsEqualTo(7);
        await Assert.That(record.Wide).IsEqualTo(10L);
    }

    [Test]
    [DisplayName("Delimited: whitespace-only fields also take their [FieldNullValue]")]
    public async Task Delimited_WhitespaceFields_UseNullValue()
    {
        // Arrange
        var engine = new FileHelperEngine<NullValueRecord>();

        // Act
        var record = engine.ReadString("  , ,\t, , ,  ")[0];

        // Assert
        await Assert.That(record.Count).IsEqualTo(-1);
        await Assert.That(record.Active).IsTrue();
        await Assert.That(record.Price).IsEqualTo(2.5m);
        await Assert.That(record.Converted).IsEqualTo(7);
    }

    [Test]
    [DisplayName("String field keeps its text; [FieldNullValue] is not applied to strings on read (3.x)")]
    public async Task StringField_NullValueNotApplied()
    {
        // Arrange
        var engine = new FileHelperEngine<NullValueRecord>();

        // Act
        var record = engine.ReadString("1,true,1,,2,3")[0];

        // Assert
        await Assert.That(record.Note).IsEqualTo(string.Empty);
    }

    [Test]
    [DisplayName("Non-empty values are converted normally even when [FieldNullValue] is present")]
    public async Task NonEmptyValues_AreConverted()
    {
        // Arrange
        var engine = new FileHelperEngine<NullValueRecord>();

        // Act
        var record = engine.ReadString("5,false,1.25,x,3,4")[0];

        // Assert
        await Assert.That(record.Count).IsEqualTo(5);
        await Assert.That(record.Active).IsFalse();
        await Assert.That(record.Price).IsEqualTo(1.25m);
        await Assert.That(record.Note).IsEqualTo("x");
        await Assert.That(record.Converted).IsEqualTo(3);
        await Assert.That(record.Wide).IsEqualTo(4L);
    }

    [Test]
    [DisplayName("Invalid non-empty value still raises ConvertException with [FieldNullValue] present")]
    public async Task InvalidValue_StillThrows()
    {
        // Arrange
        var engine = new FileHelperEngine<NullValueRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString("abc,,,,,"));

        // Assert
        await Assert.That(ex).IsTypeOf<ConvertException>();
        await Assert.That(((ConvertException)ex!).FieldName).IsEqualTo("Count");
    }

    [Test]
    [DisplayName("Fixed-length: blank field takes its [FieldNullValue]")]
    public async Task Fixed_BlankField_UsesNullValue()
    {
        // Arrange
        var engine = new FileHelperEngine<FixedNullValueRecord>();

        // Act
        var records = engine.ReadString("    abc\n0012def");

        // Assert
        await Assert.That(records[0].Code).IsEqualTo(99);
        await Assert.That(records[0].Name).IsEqualTo("abc");
        await Assert.That(records[1].Code).IsEqualTo(12);
    }

    [Test]
    [DisplayName("Streaming and async-engine reads honour [FieldNullValue] too")]
    public async Task StreamingPaths_UseNullValue()
    {
        // Arrange
        var engine = new FileHelperEngine<FixedNullValueRecord>();
        using var asyncEngine = new FileHelperAsyncEngine<FixedNullValueRecord>();
        asyncEngine.BeginReadString("    abc");

        // Act
        var streamed = engine.ReadSync("    abc").Single();
        var next = asyncEngine.ReadNext();

        // Assert
        await Assert.That(streamed.Code).IsEqualTo(99);
        await Assert.That(next!.Code).IsEqualTo(99);
    }

    [Test]
    [DisplayName("Custom converter field: empty input takes [FieldNullValue] without calling the converter")]
    public async Task CustomConverter_EmptyUsesNullValue()
    {
        // Arrange
        var engine = new FileHelperEngine<CustomConverterNullValueRecord>();

        // Act
        var record = engine.ReadString("n,")[0];

        // Assert
        await Assert.That(record.Plain).IsEqualTo(42);
    }

    [Test]
    [DisplayName("Generator: [FieldNullValue] on a T? field is used instead of null")]
    public async Task Generator_NullableWithNullValue_UsesNullValue()
    {
        // Arrange
        var generated = RunGenerator("""
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class NullableNullValueRec
            {
                [FieldNullValue(5)] public int? Count;
            }
            """, out _);

        // Assert
        await Assert.That(generated).Contains("__v0 = (int?)(5);");
    }

    [Test]
    [DisplayName("Generator: [FieldNullValue] with an incompatible type reports MLFH010 and blocks the descriptor")]
    public async Task Generator_IncompatibleNullValue_ReportsMLFH010()
    {
        // Arrange + Act
        var generated = RunGenerator("""
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class BadNullValueRec
            {
                [FieldNullValue("zero")] public int Count;
            }
            """, out var diagnostics);

        // Assert: error diagnostic naming the field and both types; no working descriptor (no cast emitted)
        var mlfh010 = diagnostics.FirstOrDefault(d => string.Equals(d.Id, "MLFH010", StringComparison.Ordinal));
        await Assert.That(mlfh010).IsNotNull();
        await Assert.That(mlfh010!.Severity).IsEqualTo(DiagnosticSeverity.Error);
        string message = mlfh010.GetMessage(CultureInfo.InvariantCulture);
        await Assert.That(message).Contains("Count");
        await Assert.That(message).Contains("string");
        await Assert.That(message).Contains("int");
        await Assert.That(generated).DoesNotContain("\"zero\"");
    }

    private static string RunGenerator(string source, out System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics)
    {
        var compilation = CSharpCompilation.Create(
            "NullValueAsm",
            [CSharpSyntaxTree.ParseText(source)],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                // System.Runtime facade: FieldNullValueAttribute(object) binds against System.Runtime's object.
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(typeof(DelimitedRecordAttribute).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new FileHelpersGenerator().AsSourceGenerator());
        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();
        diagnostics = result.Diagnostics.AddRange(ModernLibs.FileHelpers.Tests.Generator.Correctness.GeneratorHarness.AnalyzerDiagnostics(compilation));
        return result.GeneratedTrees.Length > 0 ? result.GeneratedTrees[0].GetText().ToString() : string.Empty;
    }
}
