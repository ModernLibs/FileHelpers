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

using System.Text;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// T49 (Ruling R41): <c>HeaderText</c>/<c>FooterText</c> follow FileHelpers 3.5.2 (probed on Linux, see the T49
/// report): every captured line is followed by <see cref="Environment.NewLine"/>, the last one included, whatever
/// <c>NewLineForWrite</c> is; both are <c>""</c> (not <see langword="null"/>) on a fresh engine and when there is no
/// header or footer. A header or footer read from a file and written back gains no empty line.
/// </summary>
[Category("Read")]
public sealed class HeaderFooterText3xTests
{
    private static readonly string NL = Environment.NewLine;

    /// <summary>The newline that is not <see cref="Environment.NewLine"/> on this OS.</summary>
    private static readonly string OtherNewLine = string.Equals(Environment.NewLine, "\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static string Header => $"h1{NL}h2{NL}";

    private static string Footer => $"f1{NL}f2{NL}";

    private static async Task AssertFramed(string? header, string? footer)
    {
        await Assert.That(header).IsEqualTo(Header);
        await Assert.That(footer).IsEqualTo(Footer);
    }

    [Test]
    [DisplayName("A fresh engine has empty HeaderText and FooterText, not null (3.5.2)")]
    public async Task FreshEngine_HeaderAndFooter_AreEmpty()
    {
        // Arrange
        var engine = new FileHelperEngine<NlFramed>();
        using var asyncEngine = new FileHelperAsyncEngine<NlFramed>();
        IFileHelperEngine<NlFramed> viaInterface = new FileHelperEngine<NlFramed>();
        using IFileHelperAsyncEngine<NlFramed> viaAsyncInterface = new FileHelperAsyncEngine<NlFramed>();

        // Assert
        await Assert.That(engine.HeaderText).IsEqualTo(string.Empty);
        await Assert.That(engine.FooterText).IsEqualTo(string.Empty);
        await Assert.That(asyncEngine.HeaderText).IsEqualTo(string.Empty);
        await Assert.That(asyncEngine.FooterText).IsEqualTo(string.Empty);
        await Assert.That(viaInterface.HeaderText).IsEqualTo(string.Empty);
        await Assert.That(viaAsyncInterface.FooterText).IsEqualTo(string.Empty);
    }

    [Test]
    [DisplayName("A read without IgnoreFirst/IgnoreLast resets HeaderText and FooterText to empty (3.5.2)")]
    public async Task Read_WithoutHeaderOrFooter_ResetsToEmpty()
    {
        // Arrange: values left from earlier use
        var engine = new FileHelperEngine<NlRecord> { HeaderText = "x", FooterText = "y" };

        // Act
        engine.ReadString("1,a\n");

        // Assert
        await Assert.That(engine.HeaderText).IsEqualTo(string.Empty);
        await Assert.That(engine.FooterText).IsEqualTo(string.Empty);
    }

    [Test]
    [Arguments("h1\nh2\n1,a\n2,b\nf1\nf2\n")]
    [Arguments("h1\r\nh2\r\n1,a\r\n2,b\r\nf1\r\nf2\r\n")]
    [Arguments("h1\nh2\n1,a\nf1\nf2")]
    [DisplayName("Every read path ends each captured line with Environment.NewLine (3.5.2)")]
    public async Task Read_EveryPath_JoinsAndTerminatesWithEnvironmentNewLine(string input)
    {
        // Arrange
        var engine = new FileHelperEngine<NlFramed>();
        using var asyncEngine = new FileHelperAsyncEngine<NlFramed>();
        string path = Path.GetTempFileName();
        File.WriteAllText(path, input);

        try
        {
            // Act + Assert: ReadString, ReadStream(TextReader), ReadFile, ReadAsync(TextReader), async engine
            engine.ReadString(input);
            await AssertFramed(engine.HeaderText, engine.FooterText);

            engine.ReadStream(new StringReader(input));
            await AssertFramed(engine.HeaderText, engine.FooterText);

            engine.ReadFile(path);
            await AssertFramed(engine.HeaderText, engine.FooterText);

            await foreach (var record in engine.ReadAsync(new StringReader(input)))
                _ = record;
            await AssertFramed(engine.HeaderText, engine.FooterText);

            asyncEngine.BeginReadString(input);
            while (asyncEngine.ReadNext() is not null)
            {
            }

            await AssertFramed(asyncEngine.HeaderText, asyncEngine.FooterText);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [DisplayName("NewLineForWrite does not change HeaderText/FooterText on read (3.5.2)")]
    public async Task Read_IgnoresNewLineForWrite()
    {
        // Arrange
        var engine = new FileHelperEngine<NlFramed> { NewLineForWrite = "|" };
        using var asyncEngine = new FileHelperAsyncEngine<NlFramed> { NewLineForWrite = "|" };

        // Act
        engine.ReadString("h1\nh2\n1,a\nf1\nf2\n");
        asyncEngine.BeginReadString("h1\nh2\n1,a\nf1\nf2\n");
        while (asyncEngine.ReadNext() is not null)
        {
        }

        // Assert
        await AssertFramed(engine.HeaderText, engine.FooterText);
        await AssertFramed(asyncEngine.HeaderText, asyncEngine.FooterText);
    }

    [Test]
    [DisplayName("ParallelReadString captures HeaderText/FooterText like ReadString")]
    public async Task ParallelRead_MatchesReadString()
    {
        // Arrange: input large enough to be split into chunks (at least two 64 K chunks)
        var sb = new StringBuilder("h1\r\nh2\r\n");
        for (int i = 0; i < 30_000; i++)
            sb.Append(i).Append(",name\n");
        sb.Append("f1\nf2");
        string input = sb.ToString();
        var byDefault = new FileHelperEngine<NlFramed>();
        var withBar = new FileHelperEngine<NlFramed> { NewLineForWrite = "|" };

        // Act
        var records = byDefault.ParallelReadString(input, 64 * 1024);
        withBar.ParallelReadString(input, 64 * 1024);

        // Assert
        await Assert.That(records.Length).IsEqualTo(30_000);
        await AssertFramed(byDefault.HeaderText, byDefault.FooterText);
        await AssertFramed(withBar.HeaderText, withBar.FooterText);
    }

    [Test]
    [DisplayName("Async engine: HeaderText is set at once, FooterText stays empty until the end (3.5.2)")]
    public async Task AsyncEngine_PartialRead_FooterIsEmpty()
    {
        // Arrange
        using var engine = new FileHelperAsyncEngine<NlFramed>();

        // Act: one record of two, then Close
        engine.BeginReadString("h1\nh2\n1,a\n2,b\nf1\nf2\n");
        _ = engine.ReadNext();
        string? headerAfterOne = engine.HeaderText;
        string? footerAfterOne = engine.FooterText;
        engine.Close();

        // Assert
        await Assert.That(headerAfterOne).IsEqualTo(Header);
        await Assert.That(footerAfterOne).IsEqualTo(string.Empty);
        await Assert.That(engine.FooterText).IsEqualTo(string.Empty);
    }

    [Test]
    [Arguments("h1\nh2\n1,a\n2,b\nf1\nf2\n")]
    [Arguments("h1\r\nh2\r\n1,a\r\n2,b\r\nf1\r\nf2\r\n")]
    [DisplayName("Round trip with the default NewLineForWrite: header and footer come back without an empty line (3.5.2)")]
    public async Task RoundTrip_DefaultNewLine_NoEmptyLine(string input)
    {
        // Arrange
        var engine = new FileHelperEngine<NlFramed>();
        using var asyncEngine = new FileHelperAsyncEngine<NlFramed>();
        string expected = $"h1{NL}h2{NL}1,a{NL}2,b{NL}f1{NL}f2{NL}";

        // Act: read, then write back with the same engine (HeaderText/FooterText as read)
        string written = engine.WriteString(engine.ReadString(input));
        var rows = new List<NlFramed>();
        asyncEngine.BeginReadString(input);
        while (asyncEngine.ReadNext() is { } row)
            rows.Add(row);
        asyncEngine.Close();
        asyncEngine.BeginWriteString();
        asyncEngine.WriteNext(rows);
        string asyncWritten = asyncEngine.EndWriteString();

        // Assert
        await Assert.That(written).IsEqualTo(expected);
        await Assert.That(asyncWritten).IsEqualTo(expected);
    }

    [Test]
    [DisplayName("Round trip with the other NewLineForWrite: no empty line after the header or footer (3.5.2 writes one)")]
    public async Task RoundTrip_OtherNewLine_NoEmptyLine()
    {
        // Arrange: the header/footer read end with Environment.NewLine, the records use the other newline
        var engine = new FileHelperEngine<NlFramed> { NewLineForWrite = OtherNewLine };
        string o = OtherNewLine;

        // Act
        string written = engine.WriteString(engine.ReadString("h1\nh2\n1,a\n2,b\nf1\nf2\n"));

        // Assert: 3.5.2 on Linux with "\r\n" wrote "h1\nh2\n\r\n1,a\r\n2,b\r\nf1\nf2\n\r\n"
        await Assert.That(written).IsEqualTo($"h1{NL}h2{NL}1,a{o}2,b{o}f1{NL}f2{NL}");
    }

    [Test]
    [DisplayName("Write framing keeps the 3.x rule: NewLineForWrite is added unless the text already ends with it")]
    public async Task WriteFraming_AddsNewLineUnlessTerminated()
    {
        // Arrange
        var records = new[] { new NlRecord { Id = 1, Name = "a" } };
        var plain = new FileHelperEngine<NlRecord> { HeaderText = "H", FooterText = "F", NewLineForWrite = "\r\n" };
        var terminated = new FileHelperEngine<NlRecord> { HeaderText = "H\r\n", FooterText = "F\r\n", NewLineForWrite = "\r\n" };
        var empty = new FileHelperEngine<NlRecord> { HeaderText = string.Empty, FooterText = null, NewLineForWrite = "\r\n" };

        // Act + Assert (3.5.2: H\r\n1,x\r\nF\r\n in both cases; nothing for "" and null)
        await Assert.That(plain.WriteString(records)).IsEqualTo("H\r\n1,a\r\nF\r\n");
        await Assert.That(terminated.WriteString(records)).IsEqualTo("H\r\n1,a\r\nF\r\n");
        await Assert.That(empty.WriteString(records)).IsEqualTo("1,a\r\n");
    }
}
