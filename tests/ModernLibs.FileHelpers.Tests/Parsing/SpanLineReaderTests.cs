// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// Unit tests for SpanLineReader (PIPE-01).
/// Covers LF, CRLF, CR line endings, trailing newline, empty input, and LineNumber tracking.
/// Span-based and ref struct work is done synchronously; only string/bool results are awaited.
/// </summary>
[Category("Phase05")]
[Category("Parsing")]
public sealed class SpanLineReaderTests
{
    [Test]
    public async Task TryReadLine_SingleLineLF_ReturnsLine()
    {
        var reader = new SpanLineReader("hello\n".AsSpan());
        bool ok = reader.TryReadLine(out ReadOnlySpan<char> line);
        // Convert ref-type values to primitives before any await.
        string lineStr = line.ToString();

        await Assert.That(ok).IsTrue();
        await Assert.That(lineStr).IsEqualTo("hello");
    }

    [Test]
    public async Task TryReadLine_MultiLineCRLF_SplitsCorrectly()
    {
        var reader = new SpanLineReader("a\r\nb".AsSpan());
        bool first = reader.TryReadLine(out ReadOnlySpan<char> line1);
        bool second = reader.TryReadLine(out ReadOnlySpan<char> line2);
        bool third = reader.TryReadLine(out _);
        string s1 = line1.ToString();
        string s2 = line2.ToString();

        await Assert.That(first).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(second).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(third).IsFalse();
    }

    [Test]
    public async Task TryReadLine_TrailingNewline_DoesNotProduceEmptyLine()
    {
        var reader = new SpanLineReader("abc\n".AsSpan());
        bool first = reader.TryReadLine(out ReadOnlySpan<char> line);
        bool second = reader.TryReadLine(out _);
        string lineStr = line.ToString();

        await Assert.That(first).IsTrue();
        await Assert.That(lineStr).IsEqualTo("abc");
        await Assert.That(second).IsFalse();
    }

    [Test]
    public async Task TryReadLine_CR_SplitsCorrectly()
    {
        var reader = new SpanLineReader("x\ry".AsSpan());
        bool first = reader.TryReadLine(out ReadOnlySpan<char> line1);
        bool second = reader.TryReadLine(out ReadOnlySpan<char> line2);
        string s1 = line1.ToString();
        string s2 = line2.ToString();

        await Assert.That(first).IsTrue();
        await Assert.That(s1).IsEqualTo("x");
        await Assert.That(second).IsTrue();
        await Assert.That(s2).IsEqualTo("y");
    }

    [Test]
    public async Task TryReadLine_EmptyInput_ReturnsFalse()
    {
        var reader = new SpanLineReader(ReadOnlySpan<char>.Empty);
        bool result = reader.TryReadLine(out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task LineNumber_AdvancesPerLine()
    {
        var reader = new SpanLineReader("line1\nline2\n".AsSpan());
        int n0 = reader.LineNumber;
        reader.TryReadLine(out _);
        int n1 = reader.LineNumber;
        reader.TryReadLine(out _);
        int n2 = reader.LineNumber;

        await Assert.That(n0).IsEqualTo(0);
        await Assert.That(n1).IsEqualTo(1);
        await Assert.That(n2).IsEqualTo(2);
    }
}
