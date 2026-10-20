// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// P4, P5, P14, P16 and IgnoreEmptyLines.IgnoreSpaces: quote detection only at field start with the configured
/// quote char, <see cref="MultilineMode.AllowForBoth"/> enables multiline reads, 3.x regex options.
/// </summary>
[Category("ReadCore")]
public sealed class ReadCoreQuoteTests
{
    [Test]
    [DisplayName("P4: a quote in the middle of an unquoted field is literal for field-count validation")]
    public async Task MidFieldQuote_FieldCount_IsNotAQuote()
    {
        // Arrange: three-field record, inch mark inside the second field
        var engine = new FileHelperEngine<RcInch>();

        // Act: read a line with 5" in the description
        var records = engine.ReadString("1,5\" ruler,ok\n2,plain,ok");

        // Assert: both records parsed with the literal quote kept
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Desc).IsEqualTo("5\" ruler");
        await Assert.That(records[1].Tag).IsEqualTo("ok");
    }

    [Test]
    [DisplayName("P4: a mid-field quote does not open a multiline record and swallow following lines")]
    public async Task MidFieldQuote_Multiline_DoesNotSwallowLines()
    {
        // Arrange: multiline-enabled record, inch mark inside a field
        var engine = new FileHelperEngine<RcInchMultiline>();

        // Act: read three lines
        var records = engine.ReadString("1,5\" ruler,a\n2,plain,b\n3,x,c");

        // Assert: three separate records
        await Assert.That(records.Length).IsEqualTo(3);
        await Assert.That(records[0].Desc).IsEqualTo("5\" ruler");
        await Assert.That(records[2].Id).IsEqualTo(3);
    }

    [Test]
    [DisplayName("P4: an opening quote after leading spaces starts a multiline quoted field when the field is left-trimmed")]
    public async Task QuoteAfterLeadingSpace_TrimmedField_OpensMultiline()
    {
        // Arrange: multiline record where the trimmed quoted field follows ", "
        var engine = new FileHelperEngine<RcMultilineTrimmed>();

        // Act: quoted field spans two lines (leading space before the quote)
        var records = engine.ReadString("1, \"first\nsecond\",\"t\"\n2,\"single\",\"u\"");

        // Assert: two records, the first one assembled from both physical lines
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Notes).IsEqualTo("first\nsecond");
        await Assert.That(records[0].Tail).IsEqualTo("t");
        await Assert.That(records[1].Notes).IsEqualTo("single");
    }

    [Test]
    [DisplayName("3.x: a quote after leading spaces in an untrimmed field is literal and opens no multiline record")]
    public async Task QuoteAfterLeadingSpace_UntrimmedField_IsLiteral()
    {
        // Arrange: multiline record whose quoted field is not trimmed
        var engine = new FileHelperEngine<RcMultilineBoth>();

        // Act: blank before the quote; the next line is a separate record
        var records = engine.ReadString("1, \"first\n2,\"single\"");

        // Assert: two records, the quote of the first one kept as text
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Notes).IsEqualTo(" \"first");
        await Assert.That(records[1].Notes).IsEqualTo("single");
    }

    [Test]
    [DisplayName("3.x: after a multiline field closes, the next field's own trim mode decides whether blanks may precede a quote")]
    public async Task ContinuationLine_UsesFollowingFieldTrimMode()
    {
        // Arrange: trimmed multiline Notes followed by an untrimmed quoted Tail
        var engine = new FileHelperEngine<RcMultilineTrimmed>();

        // Act: Tail starts with a blank and a quote on the continuation line
        var records = engine.ReadString("1, \"first\nsecond\", \"x\n2,\"a\",\"b\"");

        // Assert: the untrimmed Tail does not reopen a quoted section, so line 3 is its own record
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Notes).IsEqualTo("first\nsecond");
        await Assert.That(records[0].Tail).IsEqualTo(" \"x");
        await Assert.That(records[1].Tail).IsEqualTo("b");
    }

    [Test]
    [DisplayName("P5: the configured quote char (apostrophe) drives multiline assembly")]
    public async Task ApostropheQuote_Multiline_AssemblesRecord()
    {
        // Arrange: hand descriptor with QuoteChar = '\'' and multiline read
        var engine = HandDescriptors.NewEngine<HandRecord<ApostropheQuoteTag>>();

        // Act: a quoted field spans two lines; a double quote inside is literal
        var records = engine.ReadString("'one\ntwo',x\n'say \"hi\"',y");

        // Assert: two records, newline kept inside the first field
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].A).IsEqualTo("one\ntwo");
        await Assert.That(records[0].B).IsEqualTo("x");
        await Assert.That(records[1].A).IsEqualTo("say \"hi\"");
    }

    [Test]
    [DisplayName("P16: MultilineMode.AllowForBoth enables multiline reading")]
    public async Task AllowForBoth_EnablesMultilineRead()
    {
        // Arrange: AllowForBoth record
        var engine = new FileHelperEngine<RcMultilineBoth>();

        // Act: quoted field spans three physical lines
        var records = engine.ReadString("7,\"a\r\nb\r\nc\"\r\n8,\"d\"\r\n");

        // Assert: two records, line breaks preserved as in the input
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Notes).IsEqualTo("a\r\nb\r\nc");
        await Assert.That(records[1].Id).IsEqualTo(8);
    }

    [Test]
    [DisplayName("P14: IncludeIfRegex matches case-insensitively like FileHelpers 3.x")]
    public async Task RegexCondition_IsCaseInsensitive()
    {
        // Arrange: condition ^abc
        var engine = new FileHelperEngine<RcRegexInclude>();

        // Act: mixed-case codes
        var records = engine.ReadString("ABC1,1\nxyz,2\nabc2,3");

        // Assert: both ABC1 and abc2 included
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Code).IsEqualTo("ABC1");
        await Assert.That(records[1].Code).IsEqualTo("abc2");
    }

    [Test]
    [DisplayName("P17: IgnoreEmptyLines with IgnoreSpaces skips whitespace-only lines on every path")]
    public async Task IgnoreSpaces_SkipsWhitespaceOnlyLines()
    {
        // Arrange: hand descriptor with IgnoreEmptyLines + IgnoreEmptySpaces
        var engine = HandDescriptors.NewEngine<HandRecord<IgnoreSpacesTag>>();
        const string input = "a,b\n   \n\t\nc,d\n";

        // Act: batch and streaming reads
        var batch = engine.ReadString(input);
        var streamed = engine.ReadSync(input).ToArray();

        // Assert: whitespace-only lines skipped by both
        await Assert.That(batch.Length).IsEqualTo(2);
        await Assert.That(streamed.Length).IsEqualTo(2);
        await Assert.That(batch[1].A).IsEqualTo("c");
    }
}
