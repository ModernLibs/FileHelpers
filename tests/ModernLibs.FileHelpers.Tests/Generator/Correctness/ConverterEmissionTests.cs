// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// <c>[FieldConverter]</c> arguments are honoured on read AND write.
/// </summary>
[Category("Generator")]
public sealed class ConverterEmissionTests
{
    [Test]
    [DisplayName("ConverterKind.Date format is used when writing delimited records")]
    public async Task DateFormat_Delimited_Write()
    {
        // Arrange: date field with an explicit format
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R
            {
                public string Id = "";
                [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] public System.DateTime When;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<R>();
                    var r = engine.ReadString("a,2024-01-15")[0];
                    return r.When.Month + "|" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: written with the declared format
        await Assert.That(output).IsEqualTo("1|a,2024-01-15");
    }

    [Test]
    [DisplayName("ConverterKind.Date format is used when writing fixed-length records")]
    public async Task DateFormat_Fixed_Write()
    {
        // Arrange: fixed-length date field with an explicit format
        const string source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public sealed class R
            {
                [FieldFixedLength(2)] public string Id = "";
                [FieldFixedLength(10)] [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] public System.DateTime When;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<R>();
                    var r = new R { Id = "ab", When = new System.DateTime(2024, 1, 15) };
                    string line = engine.WriteString(new[] { r }).TrimEnd('\r', '\n');
                    return line + "|" + engine.ReadString(line)[0].When.Day;
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: date written in full, not blanks
        await Assert.That(output).IsEqualTo("ab2024-01-15|15");
    }

    [Test]
    [DisplayName("Custom ConverterBase.FieldToString is called on write (delimited and fixed-length)")]
    public async Task CustomConverter_FieldToString_UsedOnWrite()
    {
        // Arrange: a ConverterBase that brackets values
        const string source = """
            using ModernLibs.FileHelpers;
            using ModernLibs.FileHelpers.Converters;
            public sealed class Bracket : ConverterBase
            {
                public override object? StringToField(string text) => int.Parse(text.Trim('<', '>', ' '), System.Globalization.CultureInfo.InvariantCulture);
                public override string? FieldToString(object? field) => "<" + field + ">";
            }
            [DelimitedRecord(",")]
            public sealed class DR { [FieldConverter(typeof(Bracket))] public int V; public string S = ""; }
            [FixedLengthRecord]
            public sealed class FR { [FieldFixedLength(5)] [FieldConverter(typeof(Bracket))] public int V; [FieldFixedLength(1)] public string S = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    string d = new FileHelperEngine<DR>().WriteString(new[] { new DR { V = 12, S = "x" } }).TrimEnd();
                    string f = new FileHelperEngine<FR>().WriteString(new[] { new FR { V = 7, S = "y" } }).TrimEnd('\r', '\n');
                    var back = new FileHelperEngine<DR>().ReadString(d)[0];
                    return d + "|" + f + "|" + back.V;
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: converter output on both write paths, readable again
        await Assert.That(output).IsEqualTo("<12>,x|<7>  y|12");
    }

    [Test]
    [DisplayName("ConverterKind.Decimal/Double/Single with \",\" decimal separator parse and write 1,5")]
    public async Task DecimalSeparator_Comma_ReadAndWrite()
    {
        // Arrange: semicolon-delimited record with comma decimal separators
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(";")]
            public sealed class Euro
            {
                [FieldConverter(ConverterKind.Decimal, ",")] public decimal M;
                [FieldConverter(ConverterKind.Double, ",")] public double D;
                [FieldConverter(ConverterKind.Single, ",")] public float S;
                [FieldConverter(ConverterKind.Decimal, ".")] public decimal Dot;
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<Euro>();
                    var r = engine.ReadString("1,5;2,25;3,5;4.75")[0];
                    return (r.M == 1.5m) + "|" + (r.D == 2.25) + "|" + (r.S == 3.5f) + "|" + (r.Dot == 4.75m) + "|" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: values parsed with the declared separator and written back with it
        await Assert.That(output).IsEqualTo("True|True|True|True|1,5;2,25;3,5;4.75");
    }

    [Test]
    [DisplayName("ConverterKind.Decimal with a culture name uses that culture")]
    public async Task DecimalSeparator_CultureName()
    {
        // Arrange: decimal with de-DE culture name as converter argument
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(";")]
            public sealed class C { [FieldConverter(ConverterKind.Decimal, "de-DE")] public decimal M; }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<C>();
                    var r = engine.ReadString("1,5")[0];
                    return (r.M == 1.5m) + "|" + engine.WriteString(new[] { r }).TrimEnd();
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: de-DE comma separator honoured both ways
        await Assert.That(output).IsEqualTo("True|1,5");
    }

    [Test]
    [DisplayName("A ConverterKind that does not match the field type reports MLFH019")]
    public async Task ConverterKindMismatch_ReportsDiagnostic()
    {
        // Arrange: Int32 converter on a string field
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class M { [FieldConverter(ConverterKind.Int32)] public string Name = ""; }
            """;

        // Act: generate
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: MLFH019 reported, compilation clean
        await Assert.That(run.Diagnostics("MLFH019").Count).IsEqualTo(1);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }
}
