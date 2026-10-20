// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using System.Text;
using ModernLibs.FileHelpers.Parsing;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// P1–P3, P7, P8: <see cref="PipelineLineReader"/> decodes bytes to characters before splitting lines, carries a
/// CR across buffer boundaries, honours byte order marks, flushes the decoder and splits like SpanLineReader.
/// </summary>
[Category("Pipeline")]
[Category("ReadCore")]
public sealed class PipelineLineReaderDecodeTests
{
    private static async Task<List<string>> ReadAll(byte[] bytes, Encoding encoding)
    {
        await using var reader = new PipelineLineReader(new MemoryStream(bytes), encoding);
        var lines = new List<string>();
        await foreach (var line in reader.ReadLinesAsync())
            lines.Add(line.ToString());
        return lines;
    }

    private static string Join(List<string> lines) => string.Join("|", lines);

    private static List<string> SpanLines(string content)
    {
        var lines = new List<string>();
        var reader = new SpanLineReader(content.AsSpan());
        while (reader.TryReadLine(out var line))
            lines.Add(line.ToString());
        return lines;
    }

    [Test]
    [DisplayName("P1: 3000-line CRLF input larger than the pipe buffer yields exactly 3000 lines and no empty lines")]
    public async Task CrLf_AcrossBufferBoundaries_NoExtraLines()
    {
        // Arrange: 3000 CRLF lines (~40 KB) so many CRs land at the end of a 4 KB pipe segment
        var sb = new StringBuilder();
        for (int i = 0; i < 3000; i++)
            sb.Append(i).Append(",name").Append(i).Append("\r\n");
        var content = sb.ToString();

        // Act: read through the pipeline reader
        var lines = await ReadAll(Encoding.UTF8.GetBytes(content), Encoding.UTF8);

        // Assert: identical to the synchronous splitter
        await Assert.That(lines.Count).IsEqualTo(3000);
        await Assert.That(lines.Any(l => l.Length == 0)).IsFalse();
        await Assert.That(Join(lines)).IsEqualTo(Join(SpanLines(content)));
    }

    [Test]
    [DisplayName("P1: CR as the very last byte of a pipe segment followed by LF is one terminator")]
    public async Task Cr_AtExactSegmentEnd_JoinedWithFollowingLf()
    {
        // Arrange: first line fills 4095 bytes so its CR is byte 4096 and LF starts the next segment
        var first = new string('a', 4095);
        var content = first + "\r\nsecond\r\nthird";

        // Act: read lines
        var lines = await ReadAll(Encoding.ASCII.GetBytes(content), Encoding.ASCII);

        // Assert: three lines, none empty
        await Assert.That(lines.Count).IsEqualTo(3);
        await Assert.That(lines[0]).IsEqualTo(first);
        await Assert.That(lines[1]).IsEqualTo("second");
        await Assert.That(lines[2]).IsEqualTo("third");
    }

    [Test]
    [DisplayName("P2: UTF-8 byte order mark is stripped from the first line")]
    public async Task Utf8Bom_IsStripped()
    {
        // Arrange: BOM + two lines
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("a,b\nc,d")).ToArray();

        // Act: read lines
        var lines = await ReadAll(bytes, Encoding.UTF8);

        // Assert: first field does not start with U+FEFF
        await Assert.That(lines[0]).IsEqualTo("a,b");
        await Assert.That(lines[1]).IsEqualTo("c,d");
    }

    [Test]
    [DisplayName("P3: UTF-16 LE content with BOM is decoded and split on characters")]
    public async Task Utf16Le_WithBom_DecodesLines()
    {
        // Arrange: UTF-16 LE with BOM, configured encoding UTF-8 (BOM wins like StreamReader)
        var enc = Encoding.Unicode;
        var bytes = enc.GetPreamble().Concat(enc.GetBytes("alpha\r\nbéta\nγάμμα")).ToArray();

        // Act: read lines
        var lines = await ReadAll(bytes, Encoding.UTF8);

        // Assert: three correctly decoded lines
        await Assert.That(Join(lines)).IsEqualTo("alpha|béta|γάμμα");
    }

    [Test]
    [DisplayName("P3: UTF-16 BE and UTF-32 LE without BOM decode with the configured encoding")]
    public async Task Utf16BeAndUtf32_ConfiguredEncoding_DecodeLines()
    {
        // Arrange: same text in two wide encodings without preamble
        const string text = "x1,y\néè,z\r\nlast";
        var be = Encoding.BigEndianUnicode;
        var u32 = new UTF32Encoding(bigEndian: false, byteOrderMark: false);

        // Act: read lines in both encodings
        var beLines = await ReadAll(be.GetBytes(text), be);
        var u32Lines = await ReadAll(u32.GetBytes(text), u32);

        // Assert: both match the char splitter
        await Assert.That(Join(beLines)).IsEqualTo(Join(SpanLines(text)));
        await Assert.That(Join(u32Lines)).IsEqualTo(Join(SpanLines(text)));
    }

    [Test]
    [DisplayName("P3: multi-byte UTF-8 character split across a pipe segment boundary decodes correctly")]
    public async Task Utf8MultiByte_SplitAcrossSegments_Decodes()
    {
        // Arrange: 4095 ASCII bytes then a 2-byte 'é' straddling the 4096-byte segment boundary
        var content = new string('a', 4095) + "é,end\nnext";

        // Act: read lines
        var lines = await ReadAll(Encoding.UTF8.GetBytes(content), Encoding.UTF8);

        // Assert: no replacement characters
        await Assert.That(lines.Count).IsEqualTo(2);
        await Assert.That(lines[0]).EndsWith("aé,end");
        await Assert.That(lines[1]).IsEqualTo("next");
    }

    [Test]
    [DisplayName("P7: truncated trailing UTF-8 sequence becomes U+FFFD with the default replacement fallback")]
    public async Task TruncatedTrailingByte_ReplacementFallback_YieldsReplacementChar()
    {
        // Arrange: "ab" followed by the first byte of a 2-byte sequence
        var bytes = new byte[] { (byte)'a', (byte)'b', 0xC3 };

        // Act: read lines with Encoding.UTF8 (replacement fallback)
        var lines = await ReadAll(bytes, Encoding.UTF8);

        // Assert: the dangling byte is not silently dropped
        await Assert.That(lines.Count).IsEqualTo(1);
        await Assert.That(lines[0]).IsEqualTo("ab�");
    }

    [Test]
    [DisplayName("P7: truncated trailing UTF-8 sequence throws with a throwing decoder")]
    public async Task TruncatedTrailingByte_ExceptionFallback_Throws()
    {
        // Arrange: throwing UTF-8 encoding
        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var bytes = new byte[] { (byte)'a', (byte)'\n', (byte)'b', 0xC3 };

        // Act: read all lines
        var ex = await ModernLibs.FileHelpers.Tests.Errors.Capture.ExceptionAsync(() => ReadAll(bytes, strict));

        // Assert: the decoder fallback exception surfaces
        await Assert.That(ex).IsTypeOf<DecoderFallbackException>();
    }

    [Test]
    [DisplayName("P8: bare CR followed later by LF splits like SpanLineReader")]
    public async Task BareCr_ThenLaterLf_SplitsLikeSpanLineReader()
    {
        // Arrange: mixed terminators
        const string content = "a\rb\nc\r\rd";

        // Act: read lines
        var lines = await ReadAll(Encoding.UTF8.GetBytes(content), Encoding.UTF8);

        // Assert: identical to SpanLineReader (a, b, c, "", d)
        await Assert.That(Join(lines)).IsEqualTo(Join(SpanLines(content)));
        await Assert.That(lines.Count).IsEqualTo(5);
    }
}
