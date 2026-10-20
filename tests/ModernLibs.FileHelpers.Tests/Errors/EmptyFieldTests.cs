// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Generator;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Errors;

/// <summary>
/// Empty-field semantics (FileHelpers 3.x): an empty or whitespace-only value for a non-nullable
/// value-type field without <see cref="FieldNullValueAttribute"/> raises
/// <see cref="NullValueNotFoundException"/>; <c>T?</c> fields get <see langword="null"/>; string fields
/// keep the (empty) text.
/// </summary>
[Category("Errors")]
public sealed class EmptyFieldTests
{
    [Test]
    [DisplayName("Delimited: empty int field throws NullValueNotFoundException with line and field name")]
    public async Task Delimited_EmptyInt_ThrowsNullValueNotFound()
    {
        // Arrange: the Qty (int) field of line 2 is empty
        var engine = new FileHelperEngine<ConvClassRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString("1,a,10\n2,b,"));

        // Assert: 3.x exception type, positioned on line 2, naming the field
        await Assert.That(ex).IsTypeOf<NullValueNotFoundException>();
        var nullValue = (NullValueNotFoundException)ex!;
        await Assert.That(nullValue.LineNumber).IsEqualTo(2);
        await Assert.That(nullValue.Message).Contains("'Qty'");
        await Assert.That(nullValue.Message).Contains("ConvClassRecord");
        await Assert.That(nullValue.Message).StartsWith("Line: 2");
    }

    [Test]
    [DisplayName("Delimited SaveAndContinue: empty int field records an error and skips the record")]
    public async Task Delimited_EmptyInt_SaveAndContinue()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvClassRecord> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var records = engine.ReadString("1,a,10\n2,b,\n3,c,30");

        // Assert
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsTypeOf<NullValueNotFoundException>();
    }

    [Test]
    [DisplayName("Fixed-length: whitespace-only int field throws NullValueNotFoundException")]
    public async Task Fixed_BlankInt_ThrowsNullValueNotFound()
    {
        // Arrange: Id occupies 5 blank characters
        var engine = new FileHelperEngine<ConvFixedRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString("     abc"));

        // Assert
        await Assert.That(ex).IsTypeOf<NullValueNotFoundException>();
        await Assert.That(((NullValueNotFoundException)ex!).LineNumber).IsEqualTo(1);
        await Assert.That(ex.Message).Contains("'Id'");
    }

    [Test]
    [DisplayName("Delimited: empty string field reads as empty string")]
    public async Task Delimited_EmptyString_IsEmpty()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvClassRecord>();

        // Act
        var records = engine.ReadString("1,,10");

        // Assert: string keeps its (empty) text, not null
        await Assert.That(records[0].Code).IsEqualTo(string.Empty);
        await Assert.That(records[0].Qty).IsEqualTo(10);
    }

    [Test]
    [DisplayName("Implicit DateTime and bool fields also require a value (NullValueNotFoundException)")]
    public async Task EmptyDateAndBool_Throw()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvMixedRecord>();

        // Act
        var emptyDate = Capture.Exception(() => engine.ReadString("1.5;;true"));
        var emptyBool = Capture.Exception(() => engine.ReadString("1.5;15012024; "));

        // Assert
        await Assert.That(emptyDate).IsTypeOf<NullValueNotFoundException>();
        await Assert.That(emptyDate!.Message).Contains("'When'");
        await Assert.That(emptyBool).IsTypeOf<NullValueNotFoundException>();
        await Assert.That(emptyBool!.Message).Contains("'Flag'");
    }

    [Test]
    [DisplayName("Generator: empty input for a T? field assigns null instead of throwing")]
    public async Task Generator_NullableValueType_EmptyAssignsNull()
    {
        // Arrange: a record with a nullable int field (only the parse emission is inspected here)
        var source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class NullableRec
            {
                public int? Count;
                public int Total;
            }
            """;
        var compilation = CSharpCompilation.Create(
            "NullableAsm",
            [CSharpSyntaxTree.ParseText(source)],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DelimitedRecordAttribute).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new FileHelpersGenerator().AsSourceGenerator());

        // Act
        driver = driver.RunGenerators(compilation);
        string generated = driver.GetRunResult().GeneratedTrees[0].GetText().ToString();

        // Assert: T? gets null, the non-nullable int raises NullValueNotFoundException
        await Assert.That(generated).Contains("int? __v0;");
        await Assert.That(generated).Contains("__v0 = null;");
        await Assert.That(generated).Contains("throw __NullValueNotFound(lines, __f1, \"Total\");");
    }
}
