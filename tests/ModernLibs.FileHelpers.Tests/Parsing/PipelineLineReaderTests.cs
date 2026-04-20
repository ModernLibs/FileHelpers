// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using System.Text;
using ModernLibs.FileHelpers.Parsing;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// Acceptance tests for <c>PipelineLineReader</c> (PIPE-04).
/// Covers LF, CRLF, CR, no-trailing-newline, empty stream, UTF-8 multi-byte, and cancellation.
/// </summary>
[Category("Phase08")]
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
    // PIPE-04 — PipelineLineReader
    // -------------------------------------------------------------------------

    /// <summary>PIPE-04: Stream with LF-only separators; each ReadOnlyMemory&lt;char&gt; matches expected line without newline char.</summary>
    [Test]
    public async Task ReadLinesAsync_LFTerminated_YieldsCorrectLines()
    {
        var lines = await ReadAllLines("alpha\nbeta\ngamma\n");
        await Assert.That(lines.Count).IsEqualTo(3);
        await Assert.That(lines[0]).IsEqualTo("alpha");
        await Assert.That(lines[1]).IsEqualTo("beta");
        await Assert.That(lines[2]).IsEqualTo("gamma");
    }

    /// <summary>PIPE-04: Stream with CRLF separators; lines yielded without CRLF.</summary>
    [Test]
    public async Task ReadLinesAsync_CRLFTerminated_YieldsCorrectLines()
    {
        var lines = await ReadAllLines("one\r\ntwo\r\nthree\r\n");
        await Assert.That(lines.Count).IsEqualTo(3);
        await Assert.That(lines[0]).IsEqualTo("one");
        await Assert.That(lines[1]).IsEqualTo("two");
        await Assert.That(lines[2]).IsEqualTo("three");
    }

    /// <summary>PIPE-04: Stream with CR-only separators (old Mac); correct lines yielded.</summary>
    [Test]
    public async Task ReadLinesAsync_CRTerminated_YieldsCorrectLines()
    {
        var lines = await ReadAllLines("x\ry\rz\r");
        await Assert.That(lines.Count).IsEqualTo(3);
        await Assert.That(lines[0]).IsEqualTo("x");
        await Assert.That(lines[1]).IsEqualTo("y");
        await Assert.That(lines[2]).IsEqualTo("z");
    }

    /// <summary>PIPE-04: Stream without trailing newline; last line is still yielded.</summary>
    [Test]
    public async Task ReadLinesAsync_LastLineNoNewline_YieldsLastLine()
    {
        var lines = await ReadAllLines("first\nsecond");
        await Assert.That(lines.Count).IsEqualTo(2);
        await Assert.That(lines[0]).IsEqualTo("first");
        await Assert.That(lines[1]).IsEqualTo("second");
    }

    /// <summary>PIPE-04: Empty byte stream; zero iterations (no lines yielded).</summary>
    [Test]
    public async Task ReadLinesAsync_EmptyStream_YieldsNothing()
    {
        var lines = await ReadAllLines(string.Empty);
        await Assert.That(lines.Count).IsEqualTo(0);
    }

    /// <summary>PIPE-04: Stream containing UTF-8 multi-byte characters (e.g. "héllo"); correct char output decoded.</summary>
    [Test]
    public async Task ReadLinesAsync_MultiByteUtf8_DecodesCorrectly()
    {
        // "héllo" contains é (U+00E9) which is a 2-byte UTF-8 sequence: 0xC3 0xA9
        var lines = await ReadAllLines("héllo\nwörld\n");
        await Assert.That(lines.Count).IsEqualTo(2);
        await Assert.That(lines[0]).IsEqualTo("héllo");
        await Assert.That(lines[1]).IsEqualTo("wörld");
    }

    /// <summary>PIPE-04: CancellationTokenSource cancelled before iteration; OperationCanceledException is thrown.
    /// NOTE: Implementation must apply [EnumeratorCancellation] to the CancellationToken parameter.</summary>
    [Test]
    public async Task ReadLinesAsync_CancellationToken_StopsIteration()
    {
        // NOTE: IAsyncEnumerable method must apply [EnumeratorCancellation] to its CancellationToken parameter.
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
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }
}
