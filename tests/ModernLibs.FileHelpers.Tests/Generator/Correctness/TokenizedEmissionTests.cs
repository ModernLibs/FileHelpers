// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using System.Text.RegularExpressions;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator.Correctness;

/// <summary>
/// The emitted text of the tokenized read contract (design 4.4, freeze F-4, F-10) and of the legacy bodies that call
/// the runtime read helpers (design 4.6): which records get <see cref="ITokenizedDescriptor{T}"/>, what the
/// tokenized bodies hold and in which order, and what the legacy bodies no longer hold.
/// </summary>
[Category("Generator")]
public sealed class TokenizedEmissionTests
{
    /// <summary>25 mixed plain fields: built-in conversions, nullables, a trim, a not-empty check, a discarded and two optional fields.</summary>
    internal const string PlainShape = """
        [DelimitedRecord(",")] public class PlainShape
        {
            public int Id; public string Name = ""; public decimal Amount; public double Ratio; public long Big;
            public bool Flag; public char Letter; public DateTime When; public DateOnly Day; public TimeSpan Span;
            public Guid Key; public byte Small; public short Medium; public float Single; public int? MaybeInt;
            public decimal? MaybeAmount; [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] public DateTime Iso;
            [FieldTrim(TrimMode.Both)] public string Trimmed = ""; [FieldNotEmpty] public string Required = "";
            [FieldNullValue(-1)] public int Defaulted; public string? MaybeText; [FieldValueDiscarded] public string Ignored = "";
            public uint Unsigned; [FieldOptional] public string? Note; [FieldOptional] public int? Rank;
        }
        """;

    /// <summary>One field of each <c>QuoteMode</c>, one quoted field with an end trim, one with a not-empty check.</summary>
    internal const string QuotedShape = """
        [DelimitedRecord(",")] public class QuotedShape
        {
            [FieldQuoted(QuoteMode.AlwaysQuoted)] public string Always = "";
            [FieldQuoted(QuoteMode.OptionalForRead), FieldTrim(TrimMode.Right)] public string ForRead = "";
            [FieldQuoted(QuoteMode.OptionalForWrite), FieldNotEmpty] public string ForWrite = "";
            [FieldQuoted(QuoteMode.OptionalForBoth)] public int ForBoth;
            public string Plain = "";
            public int Number;
        }
        """;

    /// <summary>A recyclable record with a custom <c>ConverterBase</c> and an <c>ISpanConverter&lt;T&gt;</c>.</summary>
    internal const string RecyclableShape = """
        [DelimitedRecord(";")] public class RecyclableShape
        {
            [FieldConverter(typeof(UpperConverter))] public string Upper { get; set; } = "";
            [FieldConverter(typeof(CentsConverter))] public Cents Price { get; set; }
            [FieldConverter(typeof(CentsConverter))] public Cents? MaybePrice { get; set; }
            public int Count { get; set; }
            [FieldOptional] public string? Comment { get; set; }
        }
        public sealed class UpperConverter : ConverterBase { public override object? StringToField(string from) => from.ToUpperInvariant(); }
        public struct Cents { public long Value; }
        public sealed class CentsConverter : ISpanConverter<Cents>
        {
            public bool TryParse(ReadOnlySpan<char> s, out Cents v) { bool ok = long.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long l); v = new Cents { Value = l }; return ok; }
            public bool TryFormat(Cents v, Span<char> d, out int w) => v.Value.TryFormat(d, out w, default, System.Globalization.CultureInfo.InvariantCulture);
        }
        """;

    private const string Usings = """
        using System;
        using ModernLibs.FileHelpers;
        using ModernLibs.FileHelpers.Converters;

        """;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>Generates <paramref name="shape"/> and returns the text; fails on compile errors or warnings.</summary>
    private static async Task<string> Generate(string shape)
    {
        var run = GeneratorHarness.RunGenerator(Usings + shape);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText + "\n" + run.GeneratedText);
        await Assert.That(run.GeneratedWarnings.IsEmpty).IsTrue().Because(string.Join("\n", run.GeneratedWarnings));
        return run.GeneratedText;
    }

    /// <summary>The method that starts at <paramref name="signature"/>, up to its closing brace (four spaces of indent), or null.</summary>
    private static string? Method(string text, string signature)
    {
        int start = text.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0)
            return null;
        int end = text.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        return text[start..(end + "\n    }\n".Length)];
    }

    /// <summary>The banned texts of a tokenized body found in <paramref name="body"/> (F05: <c>try</c> as a whole word).</summary>
    private static List<string> LayoutKnowledge(string body, bool allowConverterTry)
    {
        var found = new List<string>();
        foreach (string banned in (string[])["IndexOf", "ArrayPool", "','", "';'", "'\"'", "__qRent", "finally"])
        {
            if (body.Contains(banned, StringComparison.Ordinal))
                found.Add(banned);
        }

        if (!allowConverterTry && Regex.IsMatch(body, @"\btry\b", RegexOptions.None, Timeout))
            found.Add("try");
        return found;
    }

    public static IEnumerable<string> LayoutShapes() => [nameof(PlainShape), nameof(QuotedShape)];

    private static string Shape(string name) => name switch
    {
        nameof(PlainShape) => PlainShape,
        nameof(QuotedShape) => QuotedShape,
        _ => RecyclableShape,
    };

    [Test]
    [MethodDataSource(nameof(LayoutShapes))]
    public async Task Tokenized_Bodies_Hold_No_Layout_Knowledge(string shape)
    {
        string text = await Generate(Shape(shape));
        string? tryParse = Method(text, "public bool TryParse(");
        string? tryParseInto = Method(text, "public bool TryParseInto(");

        await Assert.That(tryParse).IsNotNull();
        await Assert.That(tryParseInto).IsNotNull();
        await Assert.That(LayoutKnowledge(tryParse!, allowConverterTry: false)).IsEmpty();
        await Assert.That(LayoutKnowledge(tryParseInto!, allowConverterTry: false)).IsEmpty();
    }

    /// <summary>
    /// A custom converter keeps the legacy try/catch that wraps its exception (design 4.4 rule 3); every other banned
    /// text stays out, and every <c>try</c> is that converter wrapper.
    /// </summary>
    [Test]
    public async Task Tokenized_Bodies_Of_Custom_Converters_Hold_Only_The_Converter_Try()
    {
        string text = await Generate(RecyclableShape);
        foreach (string signature in (string[])["public bool TryParse(", "public bool TryParseInto("])
        {
            string? body = Method(text, signature);
            await Assert.That(body).IsNotNull().Because(signature);
            await Assert.That(LayoutKnowledge(body!, allowConverterTry: true)).IsEmpty();

            int tries = Regex.Count(body!, @"\btry\b", RegexOptions.None, Timeout);
            int converterCatches = Regex.Count(body!, @"catch \(global::System\.Exception __ex\d+\) when \(__ex\d+ is not global::ModernLibs\.FileHelpers\.BadUsageException\)", RegexOptions.None, Timeout);
            await Assert.That(tries).IsEqualTo(3);
            await Assert.That(converterCatches).IsEqualTo(tries);
        }
    }

    public static IEnumerable<string> AllShapes() => [nameof(PlainShape), nameof(QuotedShape), nameof(RecyclableShape)];

    [Test]
    [MethodDataSource(nameof(AllShapes))]
    public async Task Legacy_Parse_Has_No_Pool_And_No_Copied_Helpers(string shape)
    {
        string text = await Generate(Shape(shape));
        int format = text.IndexOf("public void Format(", StringComparison.Ordinal);
        await Assert.That(format).IsGreaterThan(0);
        string readSide = text[..format];

        var found = new List<string>();
        foreach (string banned in (string[])["ArrayPool", "__qRent", "__Column(", "__ConvertError(", "__NullValueNotFound(", "__ConverterFailure(", "new string(__f"])
        {
            if (readSide.Contains(banned, StringComparison.Ordinal))
                found.Add(banned);
        }

        if (Regex.IsMatch(readSide, @"\bfinally\b", RegexOptions.None, Timeout))
            found.Add("finally");
        await Assert.That(found).IsEmpty();

        // The helpers are not emitted anywhere in the file.
        await Assert.That(text).DoesNotContain("static int __Column(");
        await Assert.That(text).DoesNotContain("__ConvertError(");
    }

    [Test]
    [MethodDataSource(nameof(AllShapes))]
    public async Task Quote_Checks_Come_Before_Any_Value_Is_Read(string shape)
    {
        string text = await Generate(Shape(shape));
        foreach (string signature in (string[])["public bool TryParse(", "public bool TryParseInto("])
        {
            string body = Method(text, signature)!;
            int firstValue = body.IndexOf("tokens.GetValue(", StringComparison.Ordinal);
            int firstString = body.IndexOf("tokens.GetString(", StringComparison.Ordinal);
            int firstRead = firstValue < 0 ? firstString : firstString < 0 ? firstValue : Math.Min(firstValue, firstString);
            int lastCheck = body.LastIndexOf("tokens.IsQuoted(", StringComparison.Ordinal);
            await Assert.That(firstRead).IsGreaterThan(0).Because(signature);
            await Assert.That(lastCheck).IsLessThan(firstRead).Because(signature);
        }
    }

    [Test]
    public async Task QuotedShape_Checks_The_Quote_Required_Fields_Only()
    {
        string text = await Generate(QuotedShape);
        string body = Method(text, "public bool TryParse(")!;
        await Assert.That(body).Contains("if (tokens.Count > 0 && !tokens.IsQuoted(0)) { result = default!; return false; }");
        await Assert.That(body).Contains("if (tokens.Count > 2 && !tokens.IsQuoted(2)) { result = default!; return false; }");
        await Assert.That(body).DoesNotContain("tokens.IsQuoted(1)");
        await Assert.That(body).DoesNotContain("tokens.IsQuoted(3)");

        string into = Method(text, "public bool TryParseInto(")!;
        await Assert.That(into).Contains("if (tokens.Count > 0 && !tokens.IsQuoted(0)) return false;");
        await Assert.That(into).Contains("if (tokens.Count > 2 && !tokens.IsQuoted(2)) return false;");
    }

    /// <summary>Review Focus 3, freeze F-4: an absent optional quote-required field is not checked.</summary>
    [Test]
    public async Task Quote_Required_Optional_Field_Checks_Count()
    {
        const string shape = """
            [DelimitedRecord(",")] public class OptionalQuoted
            {
                public int A;
                public string B = "";
                [FieldOptional, FieldQuoted(QuoteMode.AlwaysQuoted)] public string? C;
            }
            """;
        string text = await Generate(shape);
        await Assert.That(Method(text, "public bool TryParse(")!).Contains("if (tokens.Count > 2 && !tokens.IsQuoted(2))");
        await Assert.That(Method(text, "public bool TryParseInto(")!).Contains("if (tokens.Count > 2 && !tokens.IsQuoted(2))");
    }

    [Test]
    public async Task FixedLength_And_FieldInNewLine_Records_Do_Not_Get_The_Interface()
    {
        const string shape = """
            [FixedLengthRecord] public class Fixed { [FieldFixedLength(3)] public string A = ""; [FieldFixedLength(2)] public int B; }
            [DelimitedRecord(",")] public class NewLine { public string A = ""; [FieldInNewLine] public int B; }
            [DelimitedRecord(",")] public class NewLineQuoted { [FieldQuoted] public string A = ""; [FieldInNewLine, FieldQuoted(QuoteMode.AlwaysQuoted)] public string B = ""; }
            """;
        string text = await Generate(shape);
        await Assert.That(text).DoesNotContain("ITokenizedDescriptor");
        await Assert.That(text).DoesNotContain("TryParse(global::System.ReadOnlySpan<char> lines, in global::ModernLibs.FileHelpers.RecordTokens");
        await Assert.That(text).DoesNotContain("tokens.");

        // The plain delimited record of the same file does.
        string delimited = await Generate("""[DelimitedRecord(",")] public class Delimited { public string A = ""; public int B; }""");
        await Assert.That(delimited).Contains("global::ModernLibs.FileHelpers.ITokenizedDescriptor<global::Delimited>");
    }

    [Test]
    public async Task TryParseInto_Is_Emitted_Exactly_When_ParseInto_Is()
    {
        const string shape = """
            [DelimitedRecord(",")] public class Settable { public int A { get; set; } public string B { get; set; } = ""; }
            [DelimitedRecord(",")] public struct Struct { public int A; public string B; }
            [DelimitedRecord(",")] public class Ctor { public Ctor(int a, string b) { A = a; B = b; } public int A { get; } public string B { get; } }
            [DelimitedRecord(",")] public record Positional(int A, string B);
            """;
        var run = GeneratorHarness.RunGenerator(Usings + shape);
        await Assert.That(run.CompileErrors.IsEmpty).IsTrue().Because(run.ErrorText);

        var trees = run.Result.GeneratedTrees.Select(t => t.GetText().ToString()).ToList();
        string Tree(string type) => trees.Single(t => t.Contains($"class __{type}Descriptor", StringComparison.Ordinal));

        string settable = Tree("Settable");
        await Assert.That(settable).Contains("global::ModernLibs.FileHelpers.IRecyclableDescriptor<global::Settable>, global::ModernLibs.FileHelpers.ITokenizedDescriptor<global::Settable>");
        await Assert.That(settable).Contains("public bool TryParse(global::System.ReadOnlySpan<char> lines, in global::ModernLibs.FileHelpers.RecordTokens tokens, out global::Settable result)");
        await Assert.That(settable).Contains("public bool TryParseInto(global::System.ReadOnlySpan<char> lines, in global::ModernLibs.FileHelpers.RecordTokens tokens, global::Settable existing)");

        foreach (string type in (string[])["Struct", "Ctor", "Positional"])
        {
            string tree = Tree(type);
            await Assert.That(tree).Contains($"global::ModernLibs.FileHelpers.IRecordDescriptor<global::{type}>, global::ModernLibs.FileHelpers.ITokenizedDescriptor<global::{type}>").Because(type);
            await Assert.That(tree).Contains("public bool TryParse(").Because(type);
            await Assert.That(tree).DoesNotContain("TryParseInto").Because(type);
            await Assert.That(tree).DoesNotContain("ParseInto(").Because(type);
        }
    }

    [Test]
    public async Task String_Field_Uses_GetString_Unless_Trimmed_Or_NotEmpty()
    {
        const string shape = """
            [DelimitedRecord(",")] public class Strings
            {
                public string Plain = "";
                [FieldTrim(TrimMode.Both)] public string Trimmed = "";
                [FieldNotEmpty] public string NotEmpty = "";
            }
            """;
        string text = await Generate(shape);
        string body = Method(text, "public bool TryParse(")!;
        await Assert.That(body).Contains("tokens.GetString(0)");
        await Assert.That(body).DoesNotContain("GetString(1)");
        await Assert.That(body).DoesNotContain("GetString(2)");
        await Assert.That(body).DoesNotContain("__f0");
        await Assert.That(body).Contains("var __f1 = tokens.GetValue(1);");
        await Assert.That(body).Contains("var __f2 = tokens.GetValue(2);");
        await Assert.That(body).Contains("global::ModernLibs.FileHelpers.RecordParsing.CreateString(__f1)");

        // The legacy body creates every string with CreateString.
        string parse = Method(text, "public global::Strings Parse(")!;
        await Assert.That(parse).Contains("global::ModernLibs.FileHelpers.RecordParsing.CreateString(__f0)");
        await Assert.That(parse).Contains("global::ModernLibs.FileHelpers.RecordParsing.CreateString(__f1)");
    }

    [Test]
    public async Task Quoted_String_Field_In_The_Legacy_Body_Uses_UnquoteToString()
    {
        string text = await Generate(QuotedShape);
        string parse = Method(text, "public global::QuotedShape Parse(")!;

        // Field 0: no trim, no not-empty check: the quoted branch makes the string, the conversion pass adds nothing.
        await Assert.That(parse).Contains("__v0 = global::ModernLibs.FileHelpers.RecordParsing.UnquoteToString(__qInner0, '\"');");

        // Fields 1 (trim) and 2 (not-empty) and 3 (int) unquote into the span, then the later passes read it.
        await Assert.That(parse).Contains("__f1 = global::ModernLibs.FileHelpers.RecordParsing.Unquote(__qInner1, '\"');");
        await Assert.That(parse).Contains("__f2 = global::ModernLibs.FileHelpers.RecordParsing.Unquote(__qInner2, '\"');");
        await Assert.That(parse).Contains("__f3 = global::ModernLibs.FileHelpers.RecordParsing.Unquote(__qInner3, '\"');");
        await Assert.That(parse).Contains("global::ModernLibs.FileHelpers.RecordParsing.CreateString(__f1)");
        await Assert.That(parse).DoesNotContain("UnquoteToString(__qInner1");
        await Assert.That(Regex.IsMatch(parse, @"\btry\b", RegexOptions.None, Timeout)).IsFalse();

        // The tokenized body reads field 0 with GetString (unquoted by the tokens).
        await Assert.That(Method(text, "public bool TryParse(")!).Contains("tokens.GetString(0)");
    }

    [Test]
    public async Task Error_Helpers_Are_Runtime_Calls()
    {
        string text = await Generate(RecyclableShape + """
            [DelimitedRecord(",")] public class Errors { public int Number; [FieldNotEmpty] public string Text = ""; }
            """);
        await Assert.That(text).Contains("throw global::ModernLibs.FileHelpers.RecordParsing.ConvertError(lines, __f3, typeof(int), \"Count\", null, null);");
        await Assert.That(text).Contains("throw global::ModernLibs.FileHelpers.RecordParsing.NullValueNotFound(lines, __f3, \"Count\", \"RecyclableShape\");");
        await Assert.That(text).Contains("throw global::ModernLibs.FileHelpers.RecordParsing.ConverterFailure(lines, __f0, typeof(string), \"Upper\", \"UpperConverter\", __ex0);");
        await Assert.That(text).Contains("throw global::ModernLibs.FileHelpers.RecordParsing.ConvertError(lines, __f1, typeof(string), \"Text\", \"The value is empty and must be populated.\", null);");
    }
}
