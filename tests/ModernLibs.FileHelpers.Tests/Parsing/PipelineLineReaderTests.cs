// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using System.Text;
using ModernLibs.FileHelpers.Parsing;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// Acceptance tests for <c>PipelineLineReader</c>.
/// Covers LF, CRLF, CR, no-trailing-newline, empty stream, UTF-8 multi-byte, and cancellation.
/// </summary>
[Category("Pipeline")]
public sealed class PipelineLineReaderTests
{
    // -------------------------------------------------------------------------
    // Helper: create a MemoryStream from a UTF-8 string
    // -------------------------------------------------------------------------

    private static MemoryStream ToStream(string content, Encoding? encoding = null)
    {
        var enc = encoding ?? Encoding.UTF8;
        return new MemoryStream(enc.GetBytes(content));
    }

    private static async Task<List<string>> ReadAllLines(
        string content,
        Encoding? encoding = null,
        CancellationToken cancellationToken = default)
    {
        await using var reader = new PipelineLineReader(ToStream(content, encoding), encoding ?? Encoding.UTF8);
        var lines = new List<string>();
        await foreach (var line in reader.ReadLinesAsync(cancellationToken))
        {
            lines.Add(line.ToString());
        }
        return lines;
    }

    // -------------------------------------------------------------------------
    // PipelineLineReader
    // -------------------------------------------------------------------------

    /// <summary>Stream with LF-only separators; each ReadOnlyMemory&lt;char&gt; matches expected line without newline char.</summary>
    [Test]
    [DisplayName("ReadLinesAsync yields correct lines for LF-terminated input")]
    public async Task ReadLinesAsync_LFTerminated_YieldsCorrectLines()
    {
        // Arrange & Act: Read lines from LF-terminated input
        var lines = await ReadAllLines("alpha\nbeta\ngamma\n");

        // Assert: Three lines returned without LF characters
        await Assert.That(lines.Count).IsEqualTo(3);
        await Assert.That(lines[0]).IsEqualTo("alpha");
        await Assert.That(lines[1]).IsEqualTo("beta");
        await Assert.That(lines[2]).IsEqualTo("gamma");
    }

    /// <summary>Stream with CRLF separators; lines yielded without CRLF.</summary>
    [Test]
    [DisplayName("ReadLinesAsync yields correct lines for CRLF-terminated input")]
    public async Task ReadLinesAsync_CRLFTerminated_YieldsCorrectLines()
    {
        // Arrange & Act: Read lines from CRLF-terminated input
        var lines = await ReadAllLines("one\r\ntwo\r\nthree\r\n");

        // Assert: Three lines returned without CRLF characters
        await Assert.That(lines.Count).IsEqualTo(3);
        await Assert.That(lines[0]).IsEqualTo("one");
        await Assert.That(lines[1]).IsEqualTo("two");
        await Assert.That(lines[2]).IsEqualTo("three");
    }

    /// <summary>Stream with CR-only separators (old Mac); correct lines yielded.</summary>
    [Test]
    [DisplayName("ReadLinesAsync yields correct lines for CR-terminated (old Mac) input")]
    public async Task ReadLinesAsync_CRTerminated_YieldsCorrectLines()
    {
        // Arrange & Act: Read lines from CR-terminated input
        var lines = await ReadAllLines("x\ry\rz\r");

        // Assert: Three lines returned without CR characters
        await Assert.That(lines.Count).IsEqualTo(3);
        await Assert.That(lines[0]).IsEqualTo("x");
        await Assert.That(lines[1]).IsEqualTo("y");
        await Assert.That(lines[2]).IsEqualTo("z");
    }

    /// <summary>Stream without trailing newline; last line is still yielded.</summary>
    [Test]
    [DisplayName("ReadLinesAsync yields last line when input lacks trailing newline")]
    public async Task ReadLinesAsync_LastLineNoNewline_YieldsLastLine()
    {
        // Arrange & Act: Read lines from input without trailing newline
        var lines = await ReadAllLines("first\nsecond");

        // Assert: Both lines returned including the one without trailing newline
        await Assert.That(lines.Count).IsEqualTo(2);
        await Assert.That(lines[0]).IsEqualTo("first");
        await Assert.That(lines[1]).IsEqualTo("second");
    }

    /// <summary>Empty byte stream; zero iterations (no lines yielded).</summary>
    [Test]
    [DisplayName("ReadLinesAsync yields empty list for empty stream input")]
    public async Task ReadLinesAsync_EmptyStream_YieldsNothing()
    {
        // Arrange & Act: Read lines from empty input
        var lines = await ReadAllLines(string.Empty);

        // Assert: No lines yielded from empty stream
        await Assert.That(lines.Count).IsEqualTo(0);
    }

    /// <summary>Stream containing UTF-8 multi-byte characters (e.g. "héllo"); correct char output decoded.</summary>
    [Test]
    [DisplayName("ReadLinesAsync correctly decodes UTF-8 multi-byte characters")]
    public async Task ReadLinesAsync_MultiByteUtf8_DecodesCorrectly()
    {
        // "héllo" contains é (U+00E9) which is a 2-byte UTF-8 sequence: 0xC3 0xA9
        // Arrange & Act: Read lines containing multi-byte UTF-8 characters
        var lines = await ReadAllLines("héllo\nwörld\n");

        // Assert: Multi-byte characters correctly decoded
        await Assert.That(lines.Count).IsEqualTo(2);
        await Assert.That(lines[0]).IsEqualTo("héllo");
        await Assert.That(lines[1]).IsEqualTo("wörld");
    }

    /// <summary>CancellationTokenSource cancelled before iteration; OperationCanceledException is thrown.
    /// NOTE: Implementation must apply [EnumeratorCancellation] to the CancellationToken parameter.</summary>
    [Test]
    [DisplayName("ReadLinesAsync throws OperationCanceledException when token is pre-cancelled")]
    public async Task ReadLinesAsync_CancellationToken_StopsIteration()
    {
        // NOTE: IAsyncEnumerable method must apply [EnumeratorCancellation] to its CancellationToken parameter.
        // Arrange: Create pre-cancelled CancellationTokenSource
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancelled

        bool threw = false;
        try
        {
            await using var reader = new PipelineLineReader(ToStream("line1\nline2\n"), Encoding.UTF8);
            await foreach (var _ in reader.ReadLinesAsync(cts.Token))
            {
                // Should not reach here
            }
        }
        catch (OperationCanceledException)
        {
            // Act: Exception caught
            threw = true;
        }

        // Assert: OperationCanceledException was thrown
        await Assert.That(threw).IsTrue();
    }
}
