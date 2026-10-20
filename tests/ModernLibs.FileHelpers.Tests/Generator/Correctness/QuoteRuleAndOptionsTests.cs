// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// The generated quote-aware splitter follows the engine's rule (a quote opens a quoted
/// field only at field start, optional leading blanks, configured quote char), and record options
/// carry the quote char, the ignore-spaces flag and per-field trim modes to the engine.
/// </summary>
[Category("Generator")]
public sealed class QuoteRuleAndOptionsTests
{
    [Test]
    [DisplayName("Quote inside an unquoted field (5\" ruler) is literal; the quoted field still splits correctly")]
    public async Task QuoteInsideField_IsLiteral()
    {
        // Arrange: unquoted text containing a quote before a [FieldQuoted] field with an embedded delimiter
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { public string Desc = ""; [FieldQuoted] public string Name = ""; public int N; }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString("5\" ruler,\"a,b\",3")[0];
                    return r.Desc + "|" + r.Name + "|" + r.N;
                }
            }
            """;

        // Act: compile and execute through the engine
        string output = GeneratorHarness.Execute(source);

        // Assert: the mid-field quote did not open a quoted section
        await Assert.That(output).IsEqualTo("5\" ruler|a,b|3");
    }

    [Test]
    [DisplayName("The configured quote char ([FieldQuoted('\\'')]) is used; double quotes are literal")]
    public async Task ConfiguredQuoteChar_IsUsed()
    {
        // Arrange: single-quote quoting, double quote inside unquoted data
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { public string A = ""; [FieldQuoted('\'')] public string B = ""; public string C = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<R>();
                    var r = engine.ReadString("say \"hi\",'x,y',it''s")[0];
                    return r.A + "|" + r.B + "|" + r.C + "|" + (FileHelperRegistry<R>.Descriptor.Options.QuoteChar == '\'');
                }
            }
            """;

        // Act: compile and execute through the engine
        string output = GeneratorHarness.Execute(source);

        // Assert: quoted with ', double quotes untouched, option emitted
        await Assert.That(output).IsEqualTo("say \"hi\"|x,y|it''s|True");
    }

    [Test]
    [DisplayName("A quote after leading blanks opens the quoted field; [FieldTrim] removes the blanks before unquoting")]
    public async Task LeadingBlanksBeforeQuote_TrimmedField()
    {
        // Arrange: trimmed quoted field preceded by spaces
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { [FieldQuoted] [FieldTrim(TrimMode.Both)] public string A = ""; public int N; }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString("  \"a,b\"  ,4")[0];
                    return r.A + "|" + r.N;
                }
            }
            """;

        // Act: compile and execute through the engine
        string output = GeneratorHarness.Execute(source);

        // Assert: quoted content recovered, delimiter inside quotes ignored
        await Assert.That(output).IsEqualTo("a,b|4");
    }

    [Test]
    [DisplayName("3.x: without a left trim, a quote after leading blanks is literal (field count and split agree)")]
    public async Task LeadingBlanksBeforeQuote_UntrimmedField_QuoteIsLiteral()
    {
        // Arrange: untrimmed quoted first field preceded by a blank; the comma inside the quotes is a delimiter
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { [FieldQuoted] public string A = ""; [FieldQuoted] public string B = ""; public string C = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString(" \"a,b\",c")[0];
                    return "[" + r.A + "]|[" + r.B + "]|[" + r.C + "]";
                }
            }
            """;

        // Act: compile and execute through the engine
        string output = GeneratorHarness.Execute(source);

        // Assert: like FileHelpers 3.x (OptionalForRead), the field is read as plain text up to the delimiter
        await Assert.That(output).IsEqualTo("[ \"a]|[b\"]|[c]");
    }

    [Test]
    [DisplayName("3.x: TrimMode.Right does not allow blanks before the opening quote; TrimMode.Left does")]
    public async Task LeadingBlanksBeforeQuote_DependsOnPerFieldTrimMode()
    {
        // Arrange: first field right-trimmed only, second field left-trimmed
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R
            {
                [FieldQuoted] [FieldTrim(TrimMode.Right)] public string A = "";
                [FieldQuoted] [FieldTrim(TrimMode.Left)] public string B = "";
                public string C = "";
            }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<R>();
                    var right = engine.ReadString(" \"a,b\",c")[0];
                    var left = engine.ReadString("x,  \"p,q\",z")[0];
                    return "[" + right.A + "]|[" + right.B + "]|[" + right.C + "] [" + left.A + "]|[" + left.B + "]|[" + left.C + "]";
                }
            }
            """;

        // Act: compile and execute through the engine
        string output = GeneratorHarness.Execute(source);

        // Assert: right-trimmed field keeps the quote literal; left-trimmed field is unquoted
        await Assert.That(output).IsEqualTo("[ \"a]|[b\"]|[c] [x]|[p,q]|[z]");
    }

    [Test]
    [DisplayName("Options carry IgnoreEmptySpaces from [IgnoreEmptyLines(true)]; whitespace-only lines are skipped")]
    public async Task IgnoreEmptySpaces_IsEmitted()
    {
        // Arrange: record ignoring whitespace-only lines
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            [IgnoreEmptyLines(true)]
            public sealed class R { public string A = ""; public int N; }
            [DelimitedRecord(",")]
            [IgnoreEmptyLines]
            public sealed class Q { public string A = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var rows = new FileHelperEngine<R>().ReadString("a,1\n   \nb,2");
                    return rows.Length + "|" + FileHelperRegistry<R>.Descriptor.Options.IgnoreEmptySpaces + "|" + FileHelperRegistry<Q>.Descriptor.Options.IgnoreEmptySpaces
                        + "|" + (FileHelperRegistry<Q>.Descriptor.Options.QuoteChar == '\0');
                }
            }
            """;

        // Act: compile and execute through the engine
        string output = GeneratorHarness.Execute(source);

        // Assert: blank line skipped; flag only set by IgnoreEmptyLines(true); no quote char without quoted fields
        await Assert.That(output).IsEqualTo("2|True|False|True");
    }

    [Test]
    [DisplayName("Field descriptors expose the per-field [FieldTrim] mode")]
    public async Task FieldDescriptor_ExposesTrimMode()
    {
        // Arrange: one trimmed and one untrimmed field
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { [FieldTrim(TrimMode.Left)] public string A = ""; public string B = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var f = FileHelperRegistry<R>.Descriptor.Fields;
                    return f[0].TrimMode + "|" + f[1].TrimMode;
                }
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: modes surfaced
        await Assert.That(output).IsEqualTo("Left|None");
    }
}
