// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Globalization;
using Microsoft.CodeAnalysis;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>Layout rules that FileHelpers 3.x enforces with a BadUsageException at run time.</summary>
[Category("Generator")]
public sealed class LayoutRuleTests
{
    [Test]
    [DisplayName("VG8: mixing explicit and implicit [FieldOrder] reports MLFH024 for each member without it")]
    public async Task MixedFieldOrder_ReportsMlfh024()
    {
        // Arrange: only A has [FieldOrder]; B (line 6, zero-based 5) and C do not
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class Mixed
            {
                [FieldOrder(3)] public int A;
                public int B;
                public int C;
                [FieldHidden] public int Ignored;
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: B and C reported, no descriptor
        var diagnostics = run.Diagnostics("MLFH024");
        await Assert.That(diagnostics.Count).IsEqualTo(2);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostics.Any(static d => d.Location.GetLineSpan().StartLinePosition.Line == 5)).IsTrue();
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(run.Result)).IsEqualTo(0);
    }

    [Test]
    [DisplayName("VG11: [FieldQuoted] on a fixed-length field reports MLFH025")]
    public async Task FieldQuoted_OnFixedLength_ReportsMlfh025()
    {
        // Arrange: the quoted field is on line 5 (zero-based 4)
        const string source = """
            using ModernLibs.FileHelpers;
            [FixedLengthRecord]
            public sealed class F
            {
                [FieldFixedLength(5)] [FieldQuoted] public string Name = "";
                [FieldFixedLength(3)] public string Code = "";
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert
        var diagnostic = run.Diagnostics("MLFH025").Single();
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(4);
        await Assert.That(GeneratorHarness.WorkingDescriptorCount(run.Result)).IsEqualTo(0);
    }

    [Test]
    [DisplayName("VG12: C# 10 reports MLFH026 per record and generates nothing")]
    public async Task LanguageVersion10_ReportsMlfh026()
    {
        // Arrange: two records compiled as C# 10
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public sealed class A { public string Name = ""; }
            [FixedLengthRecord] public sealed class B { [FieldFixedLength(3)] public string Code = ""; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source, languageVersion: Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp10);

        // Assert: one error per record, no generated code, so no CS8936 in generated files
        var diagnostics = run.Diagnostics("MLFH026");
        await Assert.That(diagnostics.Count).IsEqualTo(2);
        await Assert.That(diagnostics[0].Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostics[0].Location.IsInSource).IsTrue();
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(0);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("VG12: C# 11 compiles the generated code of a wide range of record shapes")]
    public async Task LanguageVersion11_Generates()
    {
        // Arrange: quoted/trimmed/converted/nullable/custom-converter fields, fixed-length, constructor-built record
        const string source = """
            using System;
            using ModernLibs.FileHelpers;
            using ModernLibs.FileHelpers.Converters;
            public sealed class Up : ConverterBase { public override object? StringToField(string text) => text.ToUpperInvariant(); }
            public enum Kind { X, Y }
            [DelimitedRecord(",")]
            [IgnoreFirst(1)]
            public sealed class A
            {
                [FieldQuoted] [FieldTrim(TrimMode.Both)] public string Name = "";
                public int? Count;
                [FieldConverter(ConverterKind.Date, "yyyyMMdd")] public DateTime When;
                [FieldConverter(typeof(Up))] public string Code = "";
                [FieldNullValue(Kind.Y)] public Kind K;
                [FieldOptional] public decimal Amount;
            }
            [FixedLengthRecord(FixedMode.AllowVariableLength)]
            public sealed class B
            {
                [FieldFixedLength(3)] [FieldAlign(AlignMode.Right, '0')] public int Id;
                [FieldFixedLength(5)] [FieldTrim(TrimMode.Right)] public string Text = "";
                [FieldFixedLength(4)] [FieldInNewLine] public string Next = "";
            }
            [DelimitedRecord("|")]
            public sealed record C(int Id, string Name);
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source, languageVersion: Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp11);

        // Assert
        await Assert.That(run.Diagnostics("MLFH026").Count).IsEqualTo(0);
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(3);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("VG14: a ref struct record reports MLFH027 and generates nothing")]
    public async Task RefStructRecord_ReportsMlfh027()
    {
        // Arrange
        const string source = """
            using System;
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public ref struct S { public int Id; public ReadOnlySpan<char> Name; }
            [DelimitedRecord(",")] public sealed class Fine { public string Name = ""; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: only MLFH027 for S (no MLFH013 for the span member), Fine still generated
        var diagnostic = run.Diagnostics("MLFH027").Single();
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(diagnostic.Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(2);
        await Assert.That(run.Diagnostics("MLFH013").Count).IsEqualTo(0);
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(1);
        await Assert.That(run.GeneratedText).Contains("__FineDescriptor");
    }

    [Test]
    [DisplayName("VG14: a ReadOnlySpan<char> field in a class reports MLFH013")]
    public async Task CharSpanField_ReportsMlfh013()
    {
        // Arrange: ReadOnlySpan<char> cannot be a field of a class, but can be a property
        const string source = """
            using System;
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { public ReadOnlySpan<char> Name => default; public string Other = ""; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert: get-only and unannotated, so it is skipped silently; nothing about ref structs
        await Assert.That(run.Diagnostics("MLFH027").Count).IsEqualTo(0);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("VG10: [IgnoreInheritedClass] on an intermediate base stops the inherited fields there (3.x)")]
    public async Task IgnoreInheritedClass_OnIntermediateBase_StopsThere()
    {
        // Arrange: C : B : A, [IgnoreInheritedClass] on B → B1, C1 (A1 is not mapped)
        const string source = """
            using ModernLibs.FileHelpers;
            public class A { public string A1 = ""; }
            [IgnoreInheritedClass] public class B : A { public string B1 = ""; }
            [DelimitedRecord(",")] public sealed class C : B { public string C1 = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<C>();
                    var c = engine.ReadString("b,c")[0];
                    return c.B1 + "|" + c.C1 + "|" + (c.A1.Length == 0) + "|" + engine.WriteString(new[] { c }).TrimEnd();
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("b|c|True|b,c");
    }

    [Test]
    [DisplayName("VG8: [FieldOrder] on every member, or on none, is accepted")]
    public async Task AllOrNoneFieldOrder_IsAccepted()
    {
        // Arrange
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class All { [FieldOrder(2)] public int A; [FieldOrder(1)] public int B; [FieldHidden] public int H; }
            [DelimitedRecord(",")]
            public sealed class None { public int A; public int B; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert
        await Assert.That(run.Diagnostics("MLFH024").Count).IsEqualTo(0);
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(2);
    }

    [Test]
    [DisplayName("A first [FieldInNewLine] field with [IgnoreEmptyLines] reports MLFH029: written files cannot be read back")]
    public async Task FirstFieldInNewLine_WithIgnoreEmptyLines_ReportsMlfh029()
    {
        // Arrange: delimited (first field on line 6, zero-based 5) and fixed-length variants
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            [IgnoreEmptyLines]
            public sealed class D
            {
                [FieldInNewLine] public string A = "";
                public string B = "";
            }
            [FixedLengthRecord]
            [IgnoreEmptyLines(true)]
            public sealed class F
            {
                [FieldFixedLength(2), FieldInNewLine] public string A = "";
                [FieldFixedLength(2)] public string B = "";
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<D>();
                    string written = engine.WriteString(new[] { new D { A = "a", B = "b" } });
                    try { return engine.ReadString(written)[0].A; }
                    catch (FileHelpersException ex) { return ex.GetType().Name; }
                }
            }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);
        string roundTrip = GeneratorHarness.Execute(source);

        // Assert: one warning per record at the first field; the descriptors are still generated, and the
        // written record does not read back (the leading blank line is skipped, as in 3.x)
        var diagnostics = run.Diagnostics("MLFH029");
        await Assert.That(diagnostics.Count).IsEqualTo(2);
        await Assert.That(diagnostics.All(static d => d.Severity == DiagnosticSeverity.Warning)).IsTrue();
        await Assert.That(diagnostics.Any(static d => d.Location.GetLineSpan().StartLinePosition.Line == 5)).IsTrue();
        await Assert.That(run.Result.GeneratedTrees.Length).IsEqualTo(2);
        await Assert.That(roundTrip).IsNotEqualTo("a");
    }

    [Test]
    [DisplayName("MLFH029 is not reported without [IgnoreEmptyLines] or when [FieldInNewLine] is on a later field")]
    public async Task FieldInNewLine_WithoutConflict_NoMlfh029()
    {
        // Arrange
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class First { [FieldInNewLine] public string A = ""; public string B = ""; }
            [DelimitedRecord(",")]
            [IgnoreEmptyLines]
            public sealed class Later { public string A = ""; [FieldInNewLine] public string B = ""; }
            """;

        // Act
        var run = GeneratorHarness.RunGenerator(source);

        // Assert
        await Assert.That(run.Diagnostics("MLFH029").Count).IsEqualTo(0);
    }

    private const string Usings = "using ModernLibs.FileHelpers;\n";

    [Test]
    [DisplayName("MLFH030-033: a layout that cannot round-trip is reported once and still emitted")]
    [Arguments("[DelimitedRecord(\"\\\"\")] public partial class R { [FieldQuoted] public string A = \"\"; public string B = \"\"; }", "MLFH030")]
    [Arguments("[DelimitedRecord(\",\\n\")] public partial class R { public string A = \"\"; public string B = \"\"; }", "MLFH031")]
    [Arguments("[DelimitedRecord(\",\")] public partial class R { [FieldTrim(TrimMode.Both, ',', ' ')] public string A = \"\"; public string B = \"\"; }", "MLFH032")]
    [Arguments("[DelimitedRecord(\",\")] public partial class R { [FieldQuoted] [FieldTrim(TrimMode.Both, '\"')] public string A = \"\"; public string B = \"\"; }", "MLFH033")]
    public async Task Layout_That_Cannot_Round_Trip_Is_Reported(string source, string id)
    {
        var run = GeneratorHarness.RunGenerator(Usings + source);
        await Assert.That(run.Diagnostics(id).Count).IsEqualTo(1);
        await Assert.That(run.Result.GeneratedTrees.Length).IsGreaterThan(0);          // still emitted
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);
    }

    [Test]
    [DisplayName("MLFH030-033: an ordinary CSV record reports none of the new diagnostics")]
    public async Task Ordinary_Csv_Record_Reports_None_Of_The_New_Diagnostics()
    {
        var run = GeneratorHarness.RunGenerator(Usings + "[DelimitedRecord(\",\")] public partial class R { [FieldQuoted] [FieldTrim(TrimMode.Both)] public string A = \"\"; public int B; }");
        await Assert.That(run.GeneratorDiagnostics.Count(static d => d.Id is "MLFH030" or "MLFH031" or "MLFH032" or "MLFH033")).IsEqualTo(0);
    }

    [Test]
    [DisplayName("MLFH030-033: severities (030, 031 and 033 Warning, 032 Info)")]
    public async Task New_Diagnostics_Have_The_Severity_Backed_By_The_Evidence()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord("\"")] public partial class A { [FieldQuoted] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",\r\n")] public partial class B { public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class C { [FieldTrim(TrimMode.Both, ',')] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class D { [FieldQuoted] [FieldTrim(TrimMode.Both, '"')] public string X = ""; public string Y = ""; }
            """;

        var run = GeneratorHarness.RunGenerator(source);

        await Assert.That(run.Diagnostics("MLFH030").Single().Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(run.Diagnostics("MLFH031").Single().Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(run.Diagnostics("MLFH032").Single().Severity).IsEqualTo(DiagnosticSeverity.Info);
        await Assert.That(run.Diagnostics("MLFH033").Single().Severity).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    [DisplayName("MLFH030: a delimiter that starts with the quote character is reported per quoted field, at the field")]
    public async Task Delimiter_StartingWithQuote_Reports030()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord("\",\"")] public partial class A { [FieldQuoted] public string X = ""; [FieldQuoted] public string Y = ""; public string Z = ""; }
            [DelimitedRecord(",")] public partial class B { public string X = ""; [FieldQuoted('\'')] [FieldDelimiter("'|")] public string Y = ""; public string Z = ""; }
            [DelimitedRecord("ba")] public partial class C { [FieldQuoted('b')] public string X = ""; public string Y = ""; }
            """;

        var run = GeneratorHarness.RunGenerator(source);

        // A: X and Y; B: Y (its own delimiter starts with the quote); C: X
        var diagnostics = run.Diagnostics("MLFH030");
        await Assert.That(diagnostics.Count).IsEqualTo(4);
        await Assert.That(diagnostics.Any(static d => d.Location.GetLineSpan().StartLinePosition.Line == 2)).IsTrue();
    }

    [Test]
    [DisplayName("MLFH030: layouts that round-trip in 3.5.2 and 4.9.1 are not reported")]
    public async Task Round_Tripping_Quote_Layouts_Report_No_030()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord("|\"")] public partial class QuoteAtEnd { [FieldQuoted] public string X = ""; [FieldQuoted] public string Y = ""; }
            [DelimitedRecord("ab")] public partial class QuoteAtEnd2 { [FieldQuoted('b')] public string X = ""; [FieldQuoted('b')] public string Y = ""; }
            [DelimitedRecord("a\"b")] public partial class QuoteInMiddle { [FieldQuoted] public string X = ""; [FieldQuoted] public string Y = ""; }
            [DelimitedRecord(",")] public partial class QuoteAtEndOfOwn { [FieldQuoted('\'')] [FieldDelimiter(";'")] public string X = ""; public string Y = ""; }
            [DelimitedRecord("\"")] public partial class LastQuoted { public string X = ""; [FieldQuoted] public string Y = ""; }
            [DelimitedRecord("\"")] public partial class OwnDelimiter { [FieldQuoted] [FieldDelimiter(";")] public string X = ""; public string Y = ""; public string Z = ""; }
            [DelimitedRecord(",")] public partial class LastOwnDelimiter { public string X = ""; [FieldQuoted] [FieldDelimiter("\"")] public string Y = ""; }
            [DelimitedRecord("\"")] public partial class HiddenLast { public string X = ""; [FieldQuoted] public string Y = ""; [FieldHidden] public string Z = ""; }
            """;

        var run = GeneratorHarness.RunGenerator(source);

        await Assert.That(run.Diagnostics("MLFH030").Count).IsEqualTo(0);
    }

    [Test]
    [DisplayName("MLFH031: a line break in a [FieldDelimiter] is reported at the field; tab, space and last-field delimiters are not")]
    public async Task LineBreak_InFieldDelimiter_Reports031()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public partial class A { public string X = ""; [FieldDelimiter(";\r")] public string Y = ""; public string Z = ""; }
            [DelimitedRecord("\t")] public partial class B { public string X = ""; public string Y = ""; }
            [DelimitedRecord(" | ")] public partial class C { public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class D { public string X = ""; [FieldDelimiter("\n")] public string Y = ""; }
            """;

        var run = GeneratorHarness.RunGenerator(source);

        var diagnostics = run.Diagnostics("MLFH031");
        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(1);
        await Assert.That(diagnostics[0].GetMessage(CultureInfo.InvariantCulture)).Contains("'\\r'");
    }

    [Test]
    [DisplayName("MLFH032: a delimiter character in the trim list is reported; the quote character, white space and default trim characters are not")]
    public async Task TrimChars_Delimiter_Reports032_But_Not_Defaults()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public partial class A { [FieldTrim(TrimMode.Both, ',', ' ')] public string X = ""; public string Y = ""; }
            [DelimitedRecord("\t")] public partial class B { [FieldTrim(TrimMode.Both)] public string X = ""; public string Y = ""; }
            [DelimitedRecord(" | ")] public partial class C { [FieldTrim(TrimMode.Both, ' ', 'x')] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class D { [FieldQuoted] [FieldTrim(TrimMode.Both, '"')] public string X = ""; public string Y = ""; }
            """;

        var run = GeneratorHarness.RunGenerator(source);

        // A only: B has the default characters, C trims a space inside " | ", D lists the quote (MLFH033)
        var diagnostics = run.Diagnostics("MLFH032");
        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(diagnostics[0].Location.GetLineSpan().StartLinePosition.Line).IsEqualTo(1);
    }

    [Test]
    [DisplayName("MLFH033: the quote character in the trim list is reported for Left or Both with OptionalForRead or AlwaysQuoted")]
    public async Task TrimChars_QuoteChar_Reports033()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public partial class Both { [FieldQuoted] [FieldTrim(TrimMode.Both, '"')] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class Left { [FieldQuoted] [FieldTrim(TrimMode.Left, ' ', '"')] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class Always { [FieldQuoted('"', QuoteMode.AlwaysQuoted)] [FieldTrim(TrimMode.Both, '"')] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class LastField { public string X = ""; [FieldQuoted('\'')] [FieldTrim(TrimMode.Both, '\'')] public string Y = ""; }
            """;

        var run = GeneratorHarness.RunGenerator(source);

        await Assert.That(run.Diagnostics("MLFH033").Count).IsEqualTo(4);
        await Assert.That(run.Diagnostics("MLFH032").Count).IsEqualTo(0);
    }

    [Test]
    [DisplayName("MLFH033: TrimMode.Right, OptionalForBoth, OptionalForWrite, unquoted fields and other trim characters are not reported")]
    public async Task TrimChars_QuoteChar_Negatives_Report_No_033()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public partial class Right { [FieldQuoted] [FieldTrim(TrimMode.Right, '"')] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class Both { [FieldQuoted('"', QuoteMode.OptionalForBoth)] [FieldTrim(TrimMode.Both, '"')] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class Write { [FieldQuoted('"', QuoteMode.OptionalForWrite)] [FieldTrim(TrimMode.Both, '"')] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class Unquoted { [FieldTrim(TrimMode.Both, '"')] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class Other { [FieldQuoted] [FieldTrim(TrimMode.Both, 'x', ' ')] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class Default { [FieldQuoted] [FieldTrim(TrimMode.Both)] public string X = ""; public string Y = ""; }
            [DelimitedRecord(",")] public partial class OtherQuote { [FieldQuoted('\'')] [FieldTrim(TrimMode.Both, '"')] public string X = ""; public string Y = ""; }
            """;

        var run = GeneratorHarness.RunGenerator(source);

        await Assert.That(run.Diagnostics("MLFH033").Count).IsEqualTo(0);
    }

    // Step 1 evidence (T31). The same layouts ran against FileHelpers 3.5.2 and ModernLibs.FileHelpers 4.9.1
    // in a scratch project; docs/diagnostics.md records the comparison. Each probe writes first (a failure to
    // write is not what is being shown) and reports only an exception from the read.

    [Test]
    [DisplayName("MLFH030 evidence: a delimiter that starts with the quote writes text it cannot read back")]
    public async Task Evidence_DelimiterStartsWithQuote_DoesNotRoundTrip()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord("\"")] public sealed class R { [FieldQuoted] public string A = ""; public string B = ""; }
            [DelimitedRecord("|\"")] public sealed class E { [FieldQuoted] public string A = ""; [FieldQuoted] public string B = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>();
                    string rText = r.WriteString(new[] { new R { A = "x", B = "y" }, new R { A = "p", B = "q" } });
                    var e = new FileHelperEngine<E>();
                    string eText = e.WriteString(new[] { new E { A = "x", B = "y" }, new E { A = "p", B = "q" } });
                    string rResult;
                    try { rResult = r.ReadString(rText).Length.ToString(); }
                    catch (System.Exception ex) { rResult = "READ-THREW " + ex.GetType().Name; }
                    return rResult + "|" + e.ReadString(eText).Length;
                }
            }
            """;

        // R: the quote starts the delimiter; E (quote at the end of the delimiter, not reported) round-trips
        await Assert.That(GeneratorHarness.Execute(source)).IsEqualTo("READ-THREW BadUsageException|2");
    }

    [Test]
    [DisplayName("MLFH031 evidence: a record delimiter with a line break writes text it cannot read back")]
    public async Task Evidence_LineBreakDelimiter_DoesNotRoundTrip()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",\n")] public sealed class R { public string A = ""; public string B = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<R>();
                    string text = engine.WriteString(new[] { new R { A = "x", B = "y" }, new R { A = "p", B = "q" } });
                    try { return engine.ReadString(text).Length.ToString(); }
                    catch (System.Exception ex) { return "READ-THREW " + ex.GetType().Name + ": " + ex.Message; }
                }
            }
            """;

        string output = GeneratorHarness.Execute(source);
        await Assert.That(output).StartsWith("READ-THREW FileHelpersException: ");
        await Assert.That(output).Contains("Expected 2 fields but found 1");
    }

    [Test]
    [DisplayName("MLFH032/033 evidence: the quote character in the trim list strips the opening quote; a delimiter in the list changes nothing")]
    public async Task Evidence_TrimChars_QuoteMisreads_DelimiterIsInert()
    {
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")] public sealed class Q { [FieldQuoted('"', QuoteMode.OptionalForRead)] [FieldTrim(TrimMode.Both, '"')] public string A = ""; public string B = ""; }
            [DelimitedRecord(",")] public sealed class D { [FieldTrim(TrimMode.Both, ',', ' ')] public string A = ""; public string B = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var q = new FileHelperEngine<Q>().ReadString("\"x\",y\n");
                    var d = new FileHelperEngine<D>().ReadString(" x ,y\n");
                    return "[" + q[0].A + "]|[" + d[0].A + "]";
                }
            }
            """;

        // Q: 4.9.1 and 3.5.2 both read "x" as x" (opening quote stripped); D: nothing changes, white space is trimmed
        await Assert.That(GeneratorHarness.Execute(source)).IsEqualTo("[x\"]|[x]");
    }
}
