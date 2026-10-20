// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// FileHelpers 3.x parity of <c>[FieldQuoted]</c>: per-field multiline default (AllowForBoth), QuoteMode on
/// read and write, quotes only in quoted fields, and the 3.x errors for malformed quoted values.
/// </summary>
[Category("Generator")]
public sealed class QuotedFieldParityTests
{
    private const string Prelude = """
        using ModernLibs.FileHelpers;
        public static class H
        {
            public static string Try(System.Func<string> f)
            {
                try { return f(); }
                catch (System.Exception ex) { return "ERR(" + ex.GetType().Name + "): " + ex.Message; }
            }
        }

        """;

    [Test]
    [DisplayName("3.x: [FieldQuoted] allows multiline values by default (AllowForBoth)")]
    public async Task FieldQuoted_DefaultsToMultiline()
    {
        // Arrange: no MultilineMode anywhere
        const string source = Prelude + """
            [DelimitedRecord(",")]
            public sealed class R { public int Id; [FieldQuoted] public string Notes = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<R>();
                    var rows = engine.ReadString("1,\"a\nb\"\n2,\"c\"");
                    string written = engine.WriteString(rows);
                    return rows.Length + "|" + rows[0].Notes.Replace("\n", "\\n") + "|" + rows[1].Notes + "|" + written.Replace("\n", "\\n").Replace("\r", "\\r");
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert: two records, newline kept, written back quoted
        await Assert.That(output).IsEqualTo("2|a\\nb|c|1,\"a\\nb\"\\r\\n2,\"c\"\\r\\n");
    }

    [Test]
    [DisplayName("3.x: MultilineMode.NotAllow on [FieldQuoted] reports an unclosed quoted field")]
    public async Task FieldQuoted_NotAllow_UnclosedQuoteIsAnError()
    {
        // Arrange: per-field NotAllow, and a record-level override on another type
        const string source = Prelude + """
            [DelimitedRecord(",")]
            public sealed class R { public int Id; [FieldQuoted(QuoteMode.OptionalForRead, MultilineMode.NotAllow)] public string Notes = ""; }
            [DelimitedRecord(",", MultilineMode = MultilineMode.NotAllow)]
            public sealed class Q { public int Id; [FieldQuoted] public string Notes = ""; }
            public static class Probe
            {
                public static string Run() =>
                    H.Try(() => new FileHelperEngine<R>().ReadString("1,\"a\nb\"").Length.ToString())
                    + " || " + H.Try(() => new FileHelperEngine<Q>().ReadString("1,\"a\nb\"").Length.ToString());
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert: both reject the line break inside the quotes
        string[] parts = output.Split(" || ");
        await Assert.That(parts[0]).Contains("unclosed quoted string");
        await Assert.That(parts[1]).Contains("unclosed quoted string");
    }

    [Test]
    [DisplayName("3.x: writing a line break into a field whose multiline mode forbids writing throws")]
    public async Task FieldQuoted_AllowForRead_WriteNewLineThrows()
    {
        // Arrange
        const string source = Prelude + """
            [DelimitedRecord(",")]
            public sealed class R { [FieldQuoted(QuoteMode.OptionalForRead, MultilineMode.AllowForRead)] public string Notes = ""; }
            public static class Probe
            {
                public static string Run() =>
                    H.Try(() => new FileHelperEngine<R>().WriteString(new[] { new R { Notes = "a\nb" } }));
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).StartsWith("ERR(BadUsageException)");
        await Assert.That(output).Contains("new line");
    }

    [Test]
    [DisplayName("3.x: AlwaysQuoted / OptionalForWrite require the quote on read; Optional*Read accept plain values")]
    public async Task QuoteMode_Read()
    {
        // Arrange: one record type per mode
        const string source = Prelude + """
            [DelimitedRecord(",")] public sealed class A { [FieldQuoted(QuoteMode.AlwaysQuoted)] public string V = ""; public int N; }
            [DelimitedRecord(",")] public sealed class W { [FieldQuoted(QuoteMode.OptionalForWrite)] public string V = ""; public int N; }
            [DelimitedRecord(",")] public sealed class O { [FieldQuoted(QuoteMode.OptionalForRead)] public string V = ""; public int N; }
            [DelimitedRecord(",")] public sealed class B { [FieldQuoted(QuoteMode.OptionalForBoth)] public string V = ""; public int N; }
            public static class Probe
            {
                public static string Run() =>
                    H.Try(() => new FileHelperEngine<A>().ReadString("\"x\",1")[0].V)
                    + " || " + H.Try(() => new FileHelperEngine<A>().ReadString("x,1")[0].V)
                    + " || " + H.Try(() => new FileHelperEngine<W>().ReadString("x,1")[0].V)
                    + " || " + H.Try(() => new FileHelperEngine<A>().ReadString(" \"x\",1")[0].V)
                    + " || " + H.Try(() => new FileHelperEngine<O>().ReadString("x,1")[0].V)
                    + " || " + H.Try(() => new FileHelperEngine<B>().ReadString("x,1")[0].V);
            }
            """;

        // Act
        string[] parts = GeneratorHarness.Execute(source).Split(" || ");

        // Assert
        await Assert.That(parts[0]).IsEqualTo("x");
        await Assert.That(parts[1]).Contains("does not begin with the quote char");
        await Assert.That(parts[2]).Contains("does not begin with the quote char");
        await Assert.That(parts[3]).Contains("spaces before the quote char");
        await Assert.That(parts[4]).IsEqualTo("x");
        await Assert.That(parts[5]).IsEqualTo("x");
    }

    [Test]
    [DisplayName("3.x: text between the closing quote and the delimiter is an error unless [FieldTrim] removes blanks")]
    public async Task TextAfterClosingQuote()
    {
        // Arrange
        const string source = Prelude + """
            [DelimitedRecord(",")] public sealed class R { [FieldQuoted] public string V = ""; public int N; }
            [DelimitedRecord(",")] public sealed class T { [FieldQuoted] [FieldTrim(TrimMode.Right)] public string V = ""; public int N; }
            public static class Probe
            {
                public static string Run() =>
                    H.Try(() => new FileHelperEngine<R>().ReadString("\"x\"y,1")[0].V)
                    + " || " + H.Try(() => new FileHelperEngine<R>().ReadString("\"x\"  ,1")[0].V)
                    + " || " + H.Try(() => new FileHelperEngine<T>().ReadString("\"x\"  ,1")[0].V);
            }
            """;

        // Act
        string[] parts = GeneratorHarness.Execute(source).Split(" || ");

        // Assert
        await Assert.That(parts[0]).Contains("not just before the separator");
        await Assert.That(parts[1]).Contains("not just before the separator");
        await Assert.That(parts[2]).IsEqualTo("x");
    }

    [Test]
    [DisplayName("3.x: AlwaysQuoted / OptionalForRead always quote on write; Optional*Write quote only for delimiter or line break")]
    public async Task QuoteMode_Write()
    {
        // Arrange
        const string source = Prelude + """
            [DelimitedRecord(",")] public sealed class A { [FieldQuoted(QuoteMode.AlwaysQuoted)] public string V = ""; }
            [DelimitedRecord(",")] public sealed class O { [FieldQuoted(QuoteMode.OptionalForRead)] public string V = ""; }
            [DelimitedRecord(",")] public sealed class W { [FieldQuoted(QuoteMode.OptionalForWrite)] public string V = ""; }
            [DelimitedRecord(",")] public sealed class B { [FieldQuoted(QuoteMode.OptionalForBoth)] public string V = ""; }
            public static class Probe
            {
                private static string L(string s) => s.Replace("\r\n", "/").Replace("\n", "\\n");
                public static string Run() =>
                    L(new FileHelperEngine<A>().WriteString(new[] { new A { V = "x" } }))
                    + " " + L(new FileHelperEngine<O>().WriteString(new[] { new O { V = "x" } }))
                    + " " + L(new FileHelperEngine<W>().WriteString(new[] { new W { V = "x" }, new W { V = "a,b" } }))
                    + " " + L(new FileHelperEngine<B>().WriteString(new[] { new B { V = "say \"hi\"" }, new B { V = "a\nb" } }));
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert: quotes inside a value without delimiter do not force quoting (3.x)
        await Assert.That(output).IsEqualTo("\"x\"/ \"x\"/ x/\"a,b\"/ say \"hi\"/\"a\\nb\"/");
    }

    [Test]
    [DisplayName("3.x: quote characters are literal in fields without [FieldQuoted]")]
    public async Task UnquotedFields_QuoteIsLiteral()
    {
        // Arrange: no quoted field at all, and a record whose only quoted field is the last one
        const string source = Prelude + """
            [DelimitedRecord(",")] public sealed class N { public string A = ""; public string B = ""; public string C = ""; }
            [DelimitedRecord(",")] public sealed class L { public string A = ""; public string B = ""; [FieldQuoted] public string C = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var n = new FileHelperEngine<N>().ReadString("\"a,b\",c")[0];
                    var l = new FileHelperEngine<L>().ReadString("\"x,y\",\"z\"")[0];
                    return "[" + n.A + "][" + n.B + "][" + n.C + "] [" + l.A + "][" + l.B + "][" + l.C + "]";
                }
            }
            """;

        // Act
        string output = GeneratorHarness.Execute(source);

        // Assert
        await Assert.That(output).IsEqualTo("[\"a][b\"][c] [\"x][y\"][z]");
    }
}
