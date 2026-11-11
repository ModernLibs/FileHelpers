// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests.
#pragma warning disable CA1849
// CA1859: Interface-typed variables are intentional — tests verify interface-based usage.
#pragma warning disable CA1859
// MA0048: Multiple fixture types live with the behavior tests that use them.
#pragma warning disable MA0048

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// <see cref="FieldTrimAttribute.TrimGivenChars"/>: opt-in trimming of the given characters on read (T44).
/// </summary>
/// <remarks>
/// The records are the ones of the 3.5.2 / 4.9.1 probe (probe-trim352.md): A unquoted Both 'x',' '; B the same with
/// [FieldQuoted(OptionalForRead)]; CL / CR Left / Right; D Both 'x' only; DQ D with [FieldQuoted(OptionalForRead)].
/// Each input is followed by <c>,y</c>. "With" expectations follow the rule (after the existing read steps,
/// including quote removal, the given characters are trimmed per TrimMode); "without" expectations are the
/// 4.9.1 (= 3.5.2) results of the probe, copied as literals.
/// </remarks>
[Category("Engine")]
[Category("FieldTrim")]
public sealed class TrimGivenCharsTests
{
    private static string Read<T>(string input) where T : class, ICodeRecord, new()
    {
        T[] records = new FileHelperEngine<T>().ReadString(input + ",y\r\n");
        return "[" + records[0].Code + "]|" + records[0].Other;
    }

    // ----- With TrimGivenChars = true -----------------------------------------------------------------------

    [Test]
    [DisplayName("A with option (unquoted, Both 'x',' '): given chars trimmed, quotes stay literal")]
    [Arguments("xxCODExx ", "[CODE]|y")]
    [Arguments(" xxCODExx", "[CODE]|y")]
    [Arguments("xCODEx", "[CODE]|y")]
    [Arguments("\"xxCODExx\"", "[\"xxCODExx\"]|y")]
    [Arguments(" \"xxCODExx\" ", "[\"xxCODExx\"]|y")]
    [Arguments("xx CODE xx", "[CODE]|y")]
    [Arguments("\"xx CODE xx\"", "[\"xx CODE xx\"]|y")]
    [Arguments("\"  xxCODExx  \"", "[\"  xxCODExx  \"]|y")]
    public async Task A_With_Option(string input, string expected) =>
        await Assert.That(Read<TgA>(input)).IsEqualTo(expected);

    [Test]
    [DisplayName("B with option (quoted OptionalForRead, Both 'x',' '): CODE for every input")]
    [Arguments("xxCODExx ", "[CODE]|y")]
    [Arguments(" xxCODExx", "[CODE]|y")]
    [Arguments("xCODEx", "[CODE]|y")]
    [Arguments("\"xxCODExx\"", "[CODE]|y")]
    [Arguments(" \"xxCODExx\" ", "[CODE]|y")]
    [Arguments("xx CODE xx", "[CODE]|y")]
    [Arguments("\"xx CODE xx\"", "[CODE]|y")]
    [Arguments("\"  xxCODExx  \"", "[CODE]|y")]
    public async Task B_With_Option(string input, string expected) =>
        await Assert.That(Read<TgB>(input)).IsEqualTo(expected);

    [Test]
    [DisplayName("CL with option (unquoted, Left 'x',' '): the start only")]
    [Arguments("xxCODExx ", "[CODExx ]|y")]
    [Arguments(" xxCODExx", "[CODExx]|y")]
    [Arguments("xCODEx", "[CODEx]|y")]
    [Arguments("\"xxCODExx\"", "[\"xxCODExx\"]|y")]
    [Arguments(" \"xxCODExx\" ", "[\"xxCODExx\" ]|y")]
    [Arguments("xx CODE xx", "[CODE xx]|y")]
    public async Task CL_With_Option(string input, string expected) =>
        await Assert.That(Read<TgCL>(input)).IsEqualTo(expected);

    [Test]
    [DisplayName("CR with option (unquoted, Right 'x',' '): the end only")]
    [Arguments("xxCODExx ", "[xxCODE]|y")]
    [Arguments(" xxCODExx", "[ xxCODE]|y")]
    [Arguments("xCODEx", "[xCODE]|y")]
    [Arguments("\"xxCODExx\"", "[\"xxCODExx\"]|y")]
    [Arguments(" \"xxCODExx\" ", "[ \"xxCODExx\"]|y")]
    [Arguments("xx CODE xx", "[xx CODE]|y")]
    public async Task CR_With_Option(string input, string expected) =>
        await Assert.That(Read<TgCR>(input)).IsEqualTo(expected);

    [Test]
    [DisplayName("D with option (unquoted, Both 'x' only): spaces stop the trim")]
    [Arguments("xxCODExx ", "[CODExx ]|y")]
    [Arguments(" xxCODExx", "[ xxCODE]|y")]
    [Arguments("xCODEx", "[CODE]|y")]
    [Arguments("\"xxCODExx\"", "[\"xxCODExx\"]|y")]
    [Arguments(" \"xxCODExx\" ", "[ \"xxCODExx\" ]|y")]
    [Arguments("xx CODE xx", "[ CODE ]|y")]
    public async Task D_With_Option(string input, string expected) =>
        await Assert.That(Read<TgD>(input)).IsEqualTo(expected);

    [Test]
    [DisplayName("DQ with option (quoted OptionalForRead, Both 'x' only): chars trimmed after unquoting, spaces stop the trim")]
    [Arguments("xxCODExx ", "[CODExx ]|y")]
    [Arguments(" xxCODExx", "[ xxCODE]|y")]
    [Arguments("xCODEx", "[CODE]|y")]
    [Arguments("\"xxCODExx\"", "[CODE]|y")]
    [Arguments(" \"xxCODExx\" ", "[ \"xxCODExx\" ]|y")]
    [Arguments("xx CODE xx", "[ CODE ]|y")]
    [Arguments("\"xx CODE xx\"", "[ CODE ]|y")]
    [Arguments("\"  xxCODExx  \"", "[  xxCODExx  ]|y")]
    public async Task DQ_With_Option(string input, string expected) =>
        await Assert.That(Read<TgDQ>(input)).IsEqualTo(expected);

    // ----- Without the option: the 4.9.1 (= 3.5.2) results of probe-trim352.md ------------------------------

    [Test]
    [DisplayName("Without option: A, B, CL, CR, D, DQ read exactly as 4.9.1 and 3.5.2")]
    [Arguments("xxCODExx ", "xxCODExx", "CODExx", "xxCODExx ", "xxCODExx", "xxCODExx", "CODExx")]
    [Arguments(" xxCODExx", "xxCODExx", "CODExx", "xxCODExx", " xxCODExx", "xxCODExx", "xxCODExx")]
    [Arguments("xCODEx", "xCODEx", "CODEx", "xCODEx", "xCODEx", "xCODEx", "CODEx")]
    [Arguments("\"xxCODExx\"", "\"xxCODExx\"", "xxCODExx", "\"xxCODExx\"", "\"xxCODExx\"", "\"xxCODExx\"", "xxCODExx")]
    [Arguments(" \"xxCODExx\" ", "\"xxCODExx\"", "xxCODExx", "\"xxCODExx\" ", " \"xxCODExx\"", "\"xxCODExx\"", "\"xxCODExx\"")]
    [Arguments("xx CODE xx", "xx CODE xx", "CODE xx", "xx CODE xx", "xx CODE xx", "xx CODE xx", "CODE xx")]
    [Arguments("\"xx CODE xx\"", "\"xx CODE xx\"", "xx CODE xx", "\"xx CODE xx\"", "\"xx CODE xx\"", "\"xx CODE xx\"", "xx CODE xx")]
    [Arguments("\"  xxCODExx  \"", "\"  xxCODExx  \"", "xxCODExx", "\"  xxCODExx  \"", "\"  xxCODExx  \"", "\"  xxCODExx  \"", "xxCODExx")]
    public async Task Without_Option_Reads_As_491(string input, string a, string b, string cl, string cr, string d, string dq)
    {
        await Assert.That(Read<PlA>(input)).IsEqualTo("[" + a + "]|y");
        await Assert.That(Read<PlB>(input)).IsEqualTo("[" + b + "]|y");
        await Assert.That(Read<PlCL>(input)).IsEqualTo("[" + cl + "]|y");
        await Assert.That(Read<PlCR>(input)).IsEqualTo("[" + cr + "]|y");
        await Assert.That(Read<PlD>(input)).IsEqualTo("[" + d + "]|y");
        await Assert.That(Read<PlDQ>(input)).IsEqualTo("[" + dq + "]|y");
    }

    // ----- Write path, descriptor, edge cases ----------------------------------------------------------------

    [Test]
    [DisplayName("Write is unchanged: the option never trims on write")]
    public async Task Write_Is_Unchanged()
    {
        string a = new FileHelperEngine<TgA>().WriteString([new TgA { Code = "xxCODExx", Other = "y" }]);
        string plain = new FileHelperEngine<PlA>().WriteString([new PlA { Code = "xxCODExx", Other = "y" }]);
        string b = new FileHelperEngine<TgB>().WriteString([new TgB { Code = " xxCODExx ", Other = "y" }]);
        string plainB = new FileHelperEngine<PlB>().WriteString([new PlB { Code = " xxCODExx ", Other = "y" }]);

        await Assert.That(a).IsEqualTo("xxCODExx,y\r\n");
        await Assert.That(plain).IsEqualTo("xxCODExx,y\r\n");
        await Assert.That(b).IsEqualTo(plainB);
        await Assert.That(b).Contains(" xxCODExx ");
    }

    [Test]
    [DisplayName("FieldDescriptor carries TrimGivenChars and the given chars as written")]
    public async Task Descriptor_Carries_The_Setting()
    {
        var withOption = FileHelperRegistry<TgB>.Descriptor.Fields[0];
        var without = FileHelperRegistry<PlB>.Descriptor.Fields[0];
        var quoteInList = FileHelperRegistry<TgQuoteInList>.Descriptor.Fields[0];

        await Assert.That(withOption.TrimGivenChars).IsTrue();
        await Assert.That(withOption.TrimChars).IsEqualTo("x ");
        await Assert.That(without.TrimGivenChars).IsFalse();
        await Assert.That(without.TrimChars).IsEqualTo("x ");
        await Assert.That(FileHelperRegistry<TgB>.Descriptor.Fields[1].TrimGivenChars).IsFalse();
        await Assert.That(quoteInList.TrimChars).IsEqualTo("\" ");
    }

    [Test]
    [DisplayName("TrimMode.None with the option is a documented no-op")]
    public async Task None_With_Option_Is_A_NoOp() =>
        await Assert.That(Read<TgNone>(" xxCODExx ")).IsEqualTo("[ xxCODExx ]|y");

    [Test]
    [DisplayName("No chars given: the 3.x white-space set is trimmed (includes U+200B, which string.Trim keeps)")]
    public async Task Default_Chars_Are_The_3x_WhiteSpace_Set()
    {
        await Assert.That(Read<TgDefault>("​\t CODE  ")).IsEqualTo("[CODE]|y");
        await Assert.That(Read<PlDefault>("​\t CODE  ")).IsEqualTo("[​\t CODE]|y");
    }

    [Test]
    [DisplayName("An empty char list with the option trims nothing")]
    public async Task Empty_List_Trims_Nothing() =>
        await Assert.That(Read<TgEmpty>(" CODE ")).IsEqualTo("[ CODE ]|y");

    [Test]
    [DisplayName("Quote char in the list: the quote is still recognised, then trimmed from the unquoted value")]
    [Arguments("\"x\"", "[x]|y")]
    [Arguments(" \"x\" ", "[x]|y")]
    [Arguments("\"a,b\"", "[a,b]|y")]
    [Arguments("\"\"\"q\"\"\"", "[q]|y")]
    [Arguments("\"a\"\"b\"", "[a\"b]|y")]
    [Arguments("plain\"", "[plain]|y")]
    public async Task Quote_In_List_Is_Trimmed_After_Unquoting(string input, string expected) =>
        await Assert.That(Read<TgQuoteInList>(input)).IsEqualTo(expected);

    [Test]
    [DisplayName("Quote char in the list: a multi-line quoted value is still recognised by the engine's line scanner")]
    public async Task Quote_In_List_Multiline_Value_Is_Read()
    {
        TgQuoteMultiline[] records = new FileHelperEngine<TgQuoteMultiline>().ReadString("\"a\r\nb\",y\r\n\"c\",z\r\n");

        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Code).IsEqualTo("a\r\nb");
        await Assert.That(records[0].Other).IsEqualTo("y");
        await Assert.That(records[1].Code).IsEqualTo("c");
    }

    [Test]
    [DisplayName("Quote char in the list: RecordParsing.IndexOfLineBreak still sees the opening quote")]
    public async Task Quote_In_List_IndexOfLineBreak_Sees_The_Quote()
    {
        var fields = new[]
        {
            new FieldDescriptor("Code", typeof(string), 0, TrimMode: TrimMode.Both, QuoteChar: '"', TrimChars: "\" ") { TrimGivenChars = true },
            new FieldDescriptor("Other", typeof(string), 1),
        };
        var legacy = new[] { fields[0] with { TrimGivenChars = false }, fields[1] };
        const string text = "\"a\nb\",y\nz";

        // With the option the quote opens a quoted value (break at 7); without it the leading quote is skipped (3.x).
        await Assert.That(RecordParsing.IndexOfLineBreak(text, fields, ",")).IsEqualTo(7);
        await Assert.That(RecordParsing.IndexOfLineBreak(text, legacy, ",")).IsEqualTo(2);
    }

    [Test]
    [DisplayName("Delimiter char in the list: it is trimmed from a quoted value after unquoting")]
    [Arguments("\"a,b,\"", "[a,b]|y")]
    [Arguments(" x ", "[x]|y")]
    public async Task Delimiter_In_List_Is_Trimmed_From_Quoted_Value(string input, string expected) =>
        await Assert.That(Read<TgDelimiterInList>(input)).IsEqualTo(expected);

    [Test]
    [DisplayName("Fixed-length field: the given chars are trimmed on read")]
    public async Task Fixed_Length_Field()
    {
        TgFixed[] records = new FileHelperEngine<TgFixed>().ReadString("0012300 ab\r\n00 7 0  cd\r\n");

        await Assert.That(records[0].Code).IsEqualTo("123");
        await Assert.That(records[1].Code).IsEqualTo("7");
        await Assert.That(records[0].Other).IsEqualTo("ab");
    }
}

/// <summary>Shape shared by the probe records.</summary>
internal interface ICodeRecord
{
    string Code { get; }

    string Other { get; }
}

[DelimitedRecord(",")]
internal sealed class TgA : ICodeRecord
{
    [FieldTrim(TrimMode.Both, 'x', ' ', TrimGivenChars = true)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TgB : ICodeRecord
{
    [FieldQuoted('"', QuoteMode.OptionalForRead)]
    [FieldTrim(TrimMode.Both, 'x', ' ', TrimGivenChars = true)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TgCL : ICodeRecord
{
    [FieldTrim(TrimMode.Left, 'x', ' ', TrimGivenChars = true)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TgCR : ICodeRecord
{
    [FieldTrim(TrimMode.Right, 'x', ' ', TrimGivenChars = true)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TgD : ICodeRecord
{
    [FieldTrim(TrimMode.Both, 'x', TrimGivenChars = true)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TgDQ : ICodeRecord
{
    [FieldQuoted('"', QuoteMode.OptionalForRead)]
    [FieldTrim(TrimMode.Both, 'x', TrimGivenChars = true)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TgNone : ICodeRecord
{
    [FieldTrim(TrimMode.None, 'x', ' ', TrimGivenChars = true)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TgDefault : ICodeRecord
{
    [FieldTrim(TrimMode.Both, TrimGivenChars = true)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TgEmpty : ICodeRecord
{
    [FieldTrim(TrimMode.Both, new char[] { }, TrimGivenChars = true)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TgQuoteInList : ICodeRecord
{
    [FieldQuoted('"', QuoteMode.OptionalForRead)]
    [FieldTrim(TrimMode.Both, '"', ' ', TrimGivenChars = true)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TgQuoteMultiline : ICodeRecord
{
    [FieldQuoted('"', QuoteMode.OptionalForRead, MultilineMode.AllowForRead)]
    [FieldTrim(TrimMode.Both, '"', ' ', TrimGivenChars = true)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TgDelimiterInList : ICodeRecord
{
    [FieldQuoted('"', QuoteMode.OptionalForRead)]
    [FieldTrim(TrimMode.Both, ',', ' ', TrimGivenChars = true)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[FixedLengthRecord]
internal sealed class TgFixed
{
    [FieldFixedLength(8)]
    [FieldTrim(TrimMode.Both, '0', ' ', TrimGivenChars = true)] public string Code { get; set; } = "";
    [FieldFixedLength(2)] public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class PlA : ICodeRecord
{
    [FieldTrim(TrimMode.Both, 'x', ' ')] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class PlB : ICodeRecord
{
    [FieldQuoted('"', QuoteMode.OptionalForRead)]
    [FieldTrim(TrimMode.Both, 'x', ' ')] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class PlCL : ICodeRecord
{
    [FieldTrim(TrimMode.Left, 'x', ' ')] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class PlCR : ICodeRecord
{
    [FieldTrim(TrimMode.Right, 'x', ' ')] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class PlD : ICodeRecord
{
    [FieldTrim(TrimMode.Both, 'x')] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class PlDQ : ICodeRecord
{
    [FieldQuoted('"', QuoteMode.OptionalForRead)]
    [FieldTrim(TrimMode.Both, 'x')] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class PlDefault : ICodeRecord
{
    [FieldTrim(TrimMode.Both)] public string Code { get; set; } = "";
    public string Other { get; set; } = "";
}
