// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// Generator side of <see cref="FieldTrimAttribute.TrimGivenChars"/> (T44): what is emitted, and how MLFH032 and
/// MLFH033 treat the option.
/// </summary>
[Category("Generator")]
public sealed class TrimGivenCharsGeneratorTests
{
    private const string Usings = "using ModernLibs.FileHelpers;\n";

    [Test]
    [DisplayName("TrimGivenChars: the given chars are trimmed with MemoryExtensions.Trim and the descriptor carries the flag")]
    public async Task Option_Emits_Given_Char_Trim_And_Descriptor_Flag()
    {
        var run = GeneratorHarness.RunGenerator(Usings + """
            [DelimitedRecord(",")] public partial class R
            {
                [FieldTrim(TrimMode.Both, 'x', ' ', TrimGivenChars = true)] public string A = "";
                [FieldTrim(TrimMode.Left, 'x', TrimGivenChars = true)] public string B = "";
                [FieldTrim(TrimMode.Right, 'x', TrimGivenChars = true)] public string C = "";
                [FieldTrim(TrimMode.Both, 'x')] public string D = "";
            }
            """);

        string text = run.GeneratedText;
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
        await Assert.That(text).Contains("__f0 = global::System.MemoryExtensions.Trim(__f0, \"x \"); // [FieldTrim] TrimGivenChars");
        await Assert.That(text).Contains("__f1 = global::System.MemoryExtensions.TrimStart(__f1, \"x\"); // [FieldTrim] TrimGivenChars");
        await Assert.That(text).Contains("__f2 = global::System.MemoryExtensions.TrimEnd(__f2, \"x\"); // [FieldTrim] TrimGivenChars");
        await Assert.That(text).Contains("__f3 = global::System.MemoryExtensions.Trim(__f3); // [FieldTrim]");
        await Assert.That(text).Contains("\"x \") { TrimGivenChars = true },");
        await Assert.That(text.Split("{ TrimGivenChars = true }").Length - 1).IsEqualTo(3);  // A, B, C; not D
    }

    [Test]
    [DisplayName("TrimGivenChars: the emitted code compiles as C# 11 without warnings")]
    public async Task Option_Code_Is_Valid_CSharp11()
    {
        var run = GeneratorHarness.RunGenerator(Usings + """
            [DelimitedRecord(",")] public partial class R
            {
                [FieldQuoted('"', QuoteMode.OptionalForRead)] [FieldTrim(TrimMode.Both, '"', ' ', TrimGivenChars = true)] public string A = "";
                [FieldTrim(TrimMode.Both, TrimGivenChars = true)] public string B = "";
                [FieldTrim(TrimMode.None, 'x', TrimGivenChars = true)] public string C = "";
                [FieldTrim(TrimMode.Both, new char[] { }, TrimGivenChars = true)] public string D = "";
            }
            [FixedLengthRecord] public partial class F
            {
                [FieldFixedLength(4)] [FieldTrim(TrimMode.Both, '0', TrimGivenChars = true)] public string A = "";
            }
            """, languageVersion: LanguageVersion.CSharp11);

        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
        await Assert.That(run.GeneratedWarnings.IsEmpty).IsTrue();
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(run.Result)).IsEqualTo(2);
        // R.A, R.B, R.D and F.A carry the flag; R.C (TrimMode.None) does not
        await Assert.That(run.GeneratedText.Split("{ TrimGivenChars = true }").Length - 1).IsEqualTo(4);
    }

    [Test]
    [DisplayName("TrimGivenChars: the quote char is left out of the chars skipped before the opening quote")]
    public async Task Option_Leaves_Quote_Out_Of_The_Skip_Set()
    {
        var with = GeneratorHarness.RunGenerator(Usings + """
            [DelimitedRecord(",")] public partial class R { [FieldQuoted] [FieldTrim(TrimMode.Both, '"', ' ', TrimGivenChars = true)] public string A = ""; public string B = ""; }
            """);
        var onlyQuote = GeneratorHarness.RunGenerator(Usings + """
            [DelimitedRecord(",")] public partial class R { [FieldQuoted] [FieldTrim(TrimMode.Both, '"', TrimGivenChars = true)] public string A = ""; public string B = ""; }
            """);
        var without = GeneratorHarness.RunGenerator(Usings + """
            [DelimitedRecord(",")] public partial class R { [FieldQuoted] [FieldTrim(TrimMode.Both, '"', ' ')] public string A = ""; public string B = ""; }
            """);

        await Assert.That(with.GeneratedText).Contains("__trim0 = global::System.Buffers.SearchValues.Create(\" \");");
        await Assert.That(onlyQuote.GeneratedText).DoesNotContain("__trim0");
        await Assert.That(without.GeneratedText).Contains("__trim0 = global::System.Buffers.SearchValues.Create(\"\\\" \");");
    }

    [Test]
    [DisplayName("MLFH033: not reported with TrimGivenChars (the quote is recognised, then trimmed); still reported without")]
    public async Task Mlfh033_Not_Reported_With_Option()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public partial class With { [FieldQuoted] [FieldTrim(TrimMode.Both, '"', TrimGivenChars = true)] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class Always { [FieldQuoted('"', QuoteMode.AlwaysQuoted)] [FieldTrim(TrimMode.Left, '"', ' ', TrimGivenChars = true)] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class Off { [FieldQuoted] [FieldTrim(TrimMode.Both, '"', TrimGivenChars = false)] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class Without { [FieldQuoted] [FieldTrim(TrimMode.Both, '"')] public string X = ""; public string Y = ""; }
            """;

        var run = GeneratorHarness.RunGenerator(source);

        // Off (line 4) and Without (line 5) only
        var diagnostics = run.Diagnostics("MLFH033");
        await Assert.That(diagnostics.Count).IsEqualTo(2);
        await Assert.That(diagnostics.All(static d => d.Location.GetLineSpan().StartLinePosition.Line >= 3)).IsTrue();
    }

    [Test]
    [DisplayName("MLFH032: reported as Info with and without TrimGivenChars")]
    public async Task Mlfh032_Stays_Info_With_Option()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public partial class With { [FieldQuoted] [FieldTrim(TrimMode.Both, ',', ' ', TrimGivenChars = true)] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class Without { [FieldTrim(TrimMode.Both, ',', ' ')] public string X = ""; public string Y = ""; }
            """;

        var run = GeneratorHarness.RunGenerator(source);

        var diagnostics = run.Diagnostics("MLFH032");
        await Assert.That(diagnostics.Count).IsEqualTo(2);
        await Assert.That(diagnostics.All(static d => d.Severity == DiagnosticSeverity.Info)).IsTrue();
    }

    [Test]
    [DisplayName("TrimGivenChars evidence: a quote char in the list is trimmed after unquoting; a delimiter char strips content of quoted values")]
    public async Task Evidence_Quote_And_Delimiter_In_List_With_Option()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public sealed class Q { [FieldQuoted('"', QuoteMode.OptionalForRead)] [FieldTrim(TrimMode.Both, '"', TrimGivenChars = true)] public string A = ""; public string B = ""; }
            [DelimitedRecord(",")] public sealed class D { [FieldQuoted('"', QuoteMode.OptionalForRead)] [FieldTrim(TrimMode.Both, ',', ' ', TrimGivenChars = true)] public string A = ""; public string B = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var q = new FileHelperEngine<Q>().ReadString("\"x\",y\n\"a,b\",z\n");
                    var d = new FileHelperEngine<D>().ReadString("\"a,b,\",y\n");
                    return "[" + q[0].A + "][" + q[1].A + "|" + q[1].B + "]|[" + d[0].A + "]";
                }
            }
            """;

        // Q: x (without the option 4.9.1 reads x"); "a,b" keeps its comma and the field boundary; D: the trailing comma goes
        await Assert.That(GeneratorHarness.Execute(source)).IsEqualTo("[x][a,b|z]|[a,b]");
    }
}
