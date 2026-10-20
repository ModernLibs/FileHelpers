// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// <c>[FieldTrim(TrimMode, params char[] chars)]</c> with FileHelpers 3.x semantics (DelimitedField /
/// FieldBase.TrimString): the trim characters are skipped before the opening quote (Left/Both) and after the
/// closing quote (Right/Both) of a quoted delimited field; the field VALUE is trimmed of whitespace per
/// TrimMode (3.x <c>string.Trim()</c>, the characters are not used there); nothing is trimmed on write.
/// Without characters the 3.x whitespace set applies.
/// </summary>
[Category("Generator")]
public sealed class TrimCharsTests
{
    [Test]
    [DisplayName("Custom trim chars around a quoted field are skipped before and after the quotes")]
    public async Task CustomTrimChars_AroundQuotes()
    {
        // Arrange: '-' padding around a quoted value containing the delimiter
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { [FieldQuoted] [FieldTrim(TrimMode.Both, '-')] public string A = ""; public int N; }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString("--\"a,b\"--,4")[0];
                    return r.A + "|" + r.N;
                }
            }
            """;

        // Act: compile and execute through the engine
        string output = GeneratorHarness.Execute(source);

        // Assert: engine field count and generated splitter agree; quoted content recovered
        await Assert.That(output).IsEqualTo("a,b|4");
    }

    [Test]
    [DisplayName("TrimMode.Left skips trim chars only before the quote; trailing chars still break the quote rule")]
    public async Task LeftTrimChars_OnlyBeforeQuote()
    {
        // Arrange: Left trim with '*' — chars after the closing quote are not skipped (3.x BadUsage)
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { [FieldQuoted] [FieldTrim(TrimMode.Left, '*')] public string A = ""; public int N; }
            public static class Probe
            {
                public static string Run()
                {
                    var engine = new FileHelperEngine<R>();
                    string ok = engine.ReadString("**\"x\",1")[0].A;
                    try { engine.ReadString("**\"x\"**,1"); return ok + "|no error"; }
                    catch (FileHelpersException ex) { return ok + "|" + ex.GetType().Name; }
                }
            }
            """;

        // Act: compile and execute through the engine
        string output = GeneratorHarness.Execute(source);

        // Assert: leading '*' skipped; trailing '*' before the separator rejected
        await Assert.That(output).IsEqualTo("x|BadUsageException");
    }

    [Test]
    [DisplayName("Unquoted value of an OptionalForRead field starts after the leading trim chars (3.x)")]
    public async Task OptionalForRead_UnquotedValue_SkipsLeadingTrimChars()
    {
        // Arrange: optional quoting, '*' trim chars, value not quoted
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { [FieldQuoted('"', QuoteMode.OptionalForRead)] [FieldTrim(TrimMode.Left, '*')] public string A = ""; public int N; }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString("**abc,1")[0];
                    return r.A + "|" + r.N;
                }
            }
            """;

        // Act: compile and execute through the engine
        string output = GeneratorHarness.Execute(source);

        // Assert: leading '*' removed
        await Assert.That(output).IsEqualTo("abc|1");
    }

    [Test]
    [DisplayName("Field values are trimmed of whitespace per TrimMode; custom trim chars do not apply to the value (3.x)")]
    public async Task ValueTrim_UsesWhitespace_NotTrimChars()
    {
        // Arrange: unquoted delimited and fixed-length fields with '-' trim chars
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class D { [FieldTrim(TrimMode.Both, '-')] public string A = ""; public int N; }
            [FixedLengthRecord]
            public sealed class F { [FieldFixedLength(8)] [FieldTrim(TrimMode.Both, '-')] public string A = ""; }
            public static class Probe
            {
                public static string Run()
                {
                    var d = new FileHelperEngine<D>().ReadString(" -x- ,1")[0];
                    var f = new FileHelperEngine<F>().ReadString(" --ab-- ")[0];
                    return "[" + d.A + "][" + f.A + "]";
                }
            }
            """;

        // Act: compile and execute through the engine
        string output = GeneratorHarness.Execute(source);

        // Assert: whitespace removed, '-' kept
        await Assert.That(output).IsEqualTo("[-x-][--ab--]");
    }

    [Test]
    [DisplayName("Without chars the 3.x whitespace set applies before the quote (e.g. no-break space)")]
    public async Task DefaultTrimChars_AreThe3xWhitespaceSet()
    {
        // Arrange: no-break space before the opening quote, default trim chars
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { [FieldQuoted] [FieldTrim(TrimMode.Both)] public string A = ""; public int N; }
            public static class Probe
            {
                public static string Run()
                {
                    var r = new FileHelperEngine<R>().ReadString(" \"a,b\"　,4")[0];
                    return r.A + "|" + r.N + "|" + FileHelperRegistry<R>.Descriptor.Fields[0].TrimChars!.Contains(' ');
                }
            }
            """;

        // Act: compile and execute through the engine
        string output = GeneratorHarness.Execute(source);

        // Assert: quoted value recognised after U+00A0, U+3000 skipped before the separator
        await Assert.That(output).IsEqualTo("a,b|4|True");
    }

    [Test]
    [DisplayName("[FieldTrim] does not trim on write (3.x)")]
    public async Task Write_DoesNotTrim()
    {
        // Arrange: trimmed fields with surrounding blanks in the record
        const string source = """
            using ModernLibs.FileHelpers;
            [DelimitedRecord(",")]
            public sealed class R { [FieldTrim(TrimMode.Both)] public string A = ""; [FieldTrim(TrimMode.Both, '-')] public string B = ""; }
            public static class Probe
            {
                public static string Run() =>
                    "[" + new FileHelperEngine<R>().WriteString(new[] { new R { A = " a ", B = "-b-" } }).TrimEnd('\r', '\n') + "]";
            }
            """;

        // Act: compile and execute
        string output = GeneratorHarness.Execute(source);

        // Assert: written unchanged
        await Assert.That(output).IsEqualTo("[ a ,-b-]");
    }
}
