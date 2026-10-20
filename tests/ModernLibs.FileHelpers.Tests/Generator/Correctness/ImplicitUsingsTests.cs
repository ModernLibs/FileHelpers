// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// Generated code must compile in consumer projects with <c>ImplicitUsings</c> disabled
/// (most FileHelpers 3.x-era projects). The harness compiles without any global usings.
/// </summary>
[Category("Generator")]
public sealed class ImplicitUsingsTests
{
    [Test]
    [DisplayName("Delimited descriptor using quoting, trim, per-field and multi-char delimiters compiles without implicit usings")]
    public async Task DelimitedKitchenSink_CompilesWithoutImplicitUsings()
    {
        // Arrange: one record touching every delimited emission path
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord("||")]
            public sealed class Kitchen
            {
                [FieldQuoted] public string Quoted = "";
                [FieldTrim(TrimMode.Both)] public string Trimmed = "";
                [FieldDelimiter(";")] public int Number;
                public decimal Money;
                [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] public System.DateTime When;
                [FieldInNewLine] public string Next = "";
                [FieldFixedLength(6)] [FieldAlign(AlignMode.Right, '0')] public string Padded = "";
            }
            [DelimitedRecord(",")]
            public sealed class Simple
            {
                public string A = "";
                public int B;
            }
            """;

        // Act: generate and compile
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: no compile errors and no warnings inside generated files
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText + "\n" + run.GeneratedText);
        await Assert.That(run.GeneratedWarnings.IsEmpty).IsTrue().Because(string.Join("\n", run.GeneratedWarnings));
    }

    [Test]
    [DisplayName("Fixed-length descriptor with alignment, converters and overflow compiles without implicit usings")]
    public async Task FixedKitchenSink_CompilesWithoutImplicitUsings()
    {
        // Arrange: one fixed-length record touching every fixed emission path
        const string source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord(FixedMode.AllowVariableLength)]
            public sealed class FixedKitchen
            {
                [FieldFixedLength(5)] public string Code = "";
                [FieldFixedLength(8)] [FieldAlign(AlignMode.Right, '0')] public int Amount;
                [FieldFixedLength(10)] [FieldTrim(TrimMode.Both)] public decimal Price;
                [FieldFixedLength(10)] [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] public System.DateTime When;
                [FieldFixedLength(4, OverflowMode = OverflowMode.Error)] public string Strict = "";
                [FieldFixedLength(6)] [FieldAlign(AlignMode.Center)] public double Ratio;
            }
            """;

        // Act: generate and compile
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: no compile errors and no warnings inside generated files
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText + "\n" + run.GeneratedText);
        await Assert.That(run.GeneratedWarnings.IsEmpty).IsTrue().Because(string.Join("\n", run.GeneratedWarnings));
    }
}
