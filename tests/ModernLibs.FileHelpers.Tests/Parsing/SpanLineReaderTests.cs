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
    [DisplayName("SpanLineReader.TryReadLine returns line content without LF for single LF-terminated line")]
    public async Task TryReadLine_SingleLineLF_ReturnsLine()
    {
        // Arrange: Create SpanLineReader with LF-terminated input
        var reader = new SpanLineReader("hello\n".AsSpan());
        bool ok = reader.TryReadLine(out ReadOnlySpan<char> line);

        // Convert ref-type values to primitives before any await.
        // Act: Convert line span to string
        string lineStr = line.ToString();

        // Assert: Line returned successfully with content "hello" (no LF)
        await Assert.That(ok).IsTrue();
        await Assert.That(lineStr).IsEqualTo("hello");
    }

    [Test]
    [DisplayName("SpanLineReader.TryReadLine splits CRLF input correctly into separate lines")]
    public async Task TryReadLine_MultiLineCRLF_SplitsCorrectly()
    {
        // Arrange: Create SpanLineReader with CRLF-terminated input
        var reader = new SpanLineReader("a\r\nb".AsSpan());
        bool first = reader.TryReadLine(out ReadOnlySpan<char> line1);
        bool second = reader.TryReadLine(out ReadOnlySpan<char> line2);
        bool third = reader.TryReadLine(out _);
        string s1 = line1.ToString();

        // Act: Convert second line span to string
        string s2 = line2.ToString();

        // Assert: Two lines extracted ("a", "b"), third read returns false
        await Assert.That(first).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(second).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(third).IsFalse();
    }

    [Test]
    [DisplayName("SpanLineReader.TryReadLine does not produce empty line for trailing LF")]
    public async Task TryReadLine_TrailingNewline_DoesNotProduceEmptyLine()
    {
        // Arrange: Create SpanLineReader with trailing LF
        var reader = new SpanLineReader("abc\n".AsSpan());
        bool first = reader.TryReadLine(out ReadOnlySpan<char> line);
        bool second = reader.TryReadLine(out _);

        // Act: Convert line to string
        string lineStr = line.ToString();

        // Assert: First line is "abc", second read returns false (no empty line)
        await Assert.That(first).IsTrue();
        await Assert.That(lineStr).IsEqualTo("abc");
        await Assert.That(second).IsFalse();
    }

    [Test]
    [DisplayName("SpanLineReader.TryReadLine splits CR-terminated input correctly")]
    public async Task TryReadLine_CR_SplitsCorrectly()
    {
        // Arrange: Create SpanLineReader with CR-terminated input
        var reader = new SpanLineReader("x\ry".AsSpan());
        bool first = reader.TryReadLine(out ReadOnlySpan<char> line1);
        bool second = reader.TryReadLine(out ReadOnlySpan<char> line2);
        string s1 = line1.ToString();

        // Act: Convert second line to string
        string s2 = line2.ToString();

        // Assert: Two lines extracted ("x", "y")
        await Assert.That(first).IsTrue();
        await Assert.That(s1).IsEqualTo("x");
        await Assert.That(second).IsTrue();
        await Assert.That(s2).IsEqualTo("y");
    }

    [Test]
    [DisplayName("SpanLineReader.TryReadLine returns false for empty input")]
    public async Task TryReadLine_EmptyInput_ReturnsFalse()
    {
        // Arrange: Create SpanLineReader with empty input
        var reader = new SpanLineReader(ReadOnlySpan<char>.Empty);

        // Act: Attempt to read line from empty input
        bool result = reader.TryReadLine(out _);

        // Assert: Read returns false
        await Assert.That(result).IsFalse();
    }

    [Test]
    [DisplayName("SpanLineReader.LineNumber advances by 1 after each successful line read")]
    public async Task LineNumber_AdvancesPerLine()
    {
        // Arrange: Create SpanLineReader with two LF-terminated lines
        var reader = new SpanLineReader("line1\nline2\n".AsSpan());
        int n0 = reader.LineNumber;
        reader.TryReadLine(out _);
        int n1 = reader.LineNumber;
        reader.TryReadLine(out _);

        // Act: Get line number after second read
        int n2 = reader.LineNumber;

        // Assert: LineNumber starts at 0, advances to 1, then 2
        await Assert.That(n0).IsEqualTo(0);
        await Assert.That(n1).IsEqualTo(1);
        await Assert.That(n2).IsEqualTo(2);
    }
}
