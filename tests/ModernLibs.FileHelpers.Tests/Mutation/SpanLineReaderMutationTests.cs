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

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Mutation;

/// <summary>
/// Mutation-target tests for <see cref="SpanLineReader"/>.
/// Covers all parse paths: empty lines, trailing newline, multi-line,
/// large buffers, LF/CRLF/CR handling, and line numbering.
/// The ref struct cannot cross an await, so every read is done first and only strings, bools and ints are awaited.
/// </summary>
[Category("Mutation")]
public sealed class SpanLineReaderMutationTests
{
    [Test]
    public async Task TryReadLine_SingleLine_NoNewline_ReturnsLine()
    {
        // Arrange: single line with no trailing newline
        var reader = new SpanLineReader("hello".AsSpan());

        // Act
        bool ok = reader.TryReadLine(out var line);
        string s = line.ToString();
        int lineNumber = reader.LineNumber;
        bool more = reader.TryReadLine(out _);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(s).IsEqualTo("hello");
        await Assert.That(lineNumber).IsEqualTo(1);
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_LFOnly_SplitsCorrectly()
    {
        // Arrange: LF-only line endings
        var reader = new SpanLineReader("a\nb\nc".AsSpan());

        // Act
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        bool ok2 = reader.TryReadLine(out var line2);
        string s2 = line2.ToString();
        bool ok3 = reader.TryReadLine(out var line3);
        string s3 = line3.ToString();
        bool more = reader.TryReadLine(out _);

        // Assert
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(ok3).IsTrue();
        await Assert.That(s3).IsEqualTo("c");
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_CRLF_SplitsAsOneLine()
    {
        // Arrange: CRLF line endings
        var reader = new SpanLineReader("a\r\nb\r\nc".AsSpan());

        // Act
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        bool ok2 = reader.TryReadLine(out var line2);
        string s2 = line2.ToString();
        bool ok3 = reader.TryReadLine(out var line3);
        string s3 = line3.ToString();
        bool more = reader.TryReadLine(out _);

        // Assert
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(ok3).IsTrue();
        await Assert.That(s3).IsEqualTo("c");
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_CROnly_SplitsCorrectly()
    {
        // Arrange: CR-only line endings (old Mac style)
        var reader = new SpanLineReader("a\rb\rc".AsSpan());

        // Act
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        bool ok2 = reader.TryReadLine(out var line2);
        string s2 = line2.ToString();
        bool ok3 = reader.TryReadLine(out var line3);
        string s3 = line3.ToString();
        bool more = reader.TryReadLine(out _);

        // Assert
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(ok3).IsTrue();
        await Assert.That(s3).IsEqualTo("c");
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_MixedLineEndings_HandlesAll()
    {
        // Arrange: mixed LF, CRLF, CR
        var reader = new SpanLineReader("a\nb\r\nc\rd".AsSpan());

        // Act
        bool ok1 = reader.TryReadLine(out var line);
        string s1 = line.ToString();
        bool ok2 = reader.TryReadLine(out line);
        string s2 = line.ToString();
        bool ok3 = reader.TryReadLine(out line);
        string s3 = line.ToString();
        bool ok4 = reader.TryReadLine(out line);
        string s4 = line.ToString();
        bool more = reader.TryReadLine(out _);

        // Assert
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(ok3).IsTrue();
        await Assert.That(s3).IsEqualTo("c");
        await Assert.That(ok4).IsTrue();
        await Assert.That(s4).IsEqualTo("d");
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_EmptyInput_ReturnsFalse()
    {
        // Arrange: empty input
        var reader = new SpanLineReader("".AsSpan());

        // Act: exhaustion guard
        bool ok = reader.TryReadLine(out _);
        int lineNumber = reader.LineNumber;

        // Assert
        await Assert.That(ok).IsFalse();
        await Assert.That(lineNumber).IsEqualTo(0);
    }

    [Test]
    public async Task TryReadLine_TrailingNewline_NoEmptyLastLine()
    {
        // Arrange: trailing newline — reader consumes it, no empty line produced
        var reader = new SpanLineReader("a\n".AsSpan());

        // Act
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        bool more = reader.TryReadLine(out _);

        // Assert
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_MultipleEmptyLines_ReturnsEmptyLines()
    {
        // Arrange: consecutive empty lines
        var reader = new SpanLineReader("\n\n\n".AsSpan());

        // Act
        bool ok1 = reader.TryReadLine(out var line1);
        bool empty1 = line1.IsEmpty;
        bool ok2 = reader.TryReadLine(out var line2);
        bool empty2 = line2.IsEmpty;
        bool ok3 = reader.TryReadLine(out var line3);
        bool empty3 = line3.IsEmpty;
        bool more = reader.TryReadLine(out _);

        // Assert
        await Assert.That(ok1).IsTrue();
        await Assert.That(empty1).IsTrue();
        await Assert.That(ok2).IsTrue();
        await Assert.That(empty2).IsTrue();
        await Assert.That(ok3).IsTrue();
        await Assert.That(empty3).IsTrue();
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_LineNumber_IncrementsCorrectly()
    {
        // Arrange
        var reader = new SpanLineReader("a\nb\nc".AsSpan());

        // Act
        bool ok1 = reader.TryReadLine(out _);
        int n1 = reader.LineNumber;
        bool ok2 = reader.TryReadLine(out _);
        int n2 = reader.LineNumber;
        bool ok3 = reader.TryReadLine(out _);
        int n3 = reader.LineNumber;

        // Assert
        await Assert.That(ok1).IsTrue();
        await Assert.That(n1).IsEqualTo(1);
        await Assert.That(ok2).IsTrue();
        await Assert.That(n2).IsEqualTo(2);
        await Assert.That(ok3).IsTrue();
        await Assert.That(n3).IsEqualTo(3);
    }

    [Test]
    public async Task TryReadLine_CRLFDoesNotProduceExtraEmptyLine()
    {
        // Arrange: CRLF should count as one terminator, not two
        var reader = new SpanLineReader("a\r\nb".AsSpan());

        // Act
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        bool ok2 = reader.TryReadLine(out var line2);
        string s2 = line2.ToString();
        bool more = reader.TryReadLine(out _);

        // Assert
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_CRLFatEnd_NoEmptyLineAfter()
    {
        // Arrange: trailing CRLF — reader consumes both chars, no empty line produced
        var reader = new SpanLineReader("a\r\n".AsSpan());

        // Act
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        bool more = reader.TryReadLine(out _);

        // Assert
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_CRFollowedByLF_AsSeparateLines()
    {
        // Arrange: CR followed by LF as separate characters (not CRLF pair)
        var reader = new SpanLineReader("a\r\nb".AsSpan());

        // Act: CR at position 1, then \n is consumed as part of CRLF handling
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        bool ok2 = reader.TryReadLine(out var line2);
        string s2 = line2.ToString();
        bool more = reader.TryReadLine(out _);

        // Assert
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_LargeBuffer_HandlesCorrectly()
    {
        // Arrange: large buffer with many lines
        var content = string.Join("\n", Enumerable.Range(0, 1000).Select(i => $"line{i}"));
        var reader = new SpanLineReader(content.AsSpan());

        // Act
        var reads = new List<(bool Ok, string Line, int LineNumber)>(1000);
        for (int i = 0; i < 1000; i++)
        {
            bool ok = reader.TryReadLine(out var line);
            reads.Add((ok, line.ToString(), reader.LineNumber));
        }
        bool more = reader.TryReadLine(out _);

        // Assert
        for (int i = 0; i < 1000; i++)
        {
            await Assert.That(reads[i].Ok).IsTrue();
            await Assert.That(reads[i].Line).IsEqualTo($"line{i}");
            await Assert.That(reads[i].LineNumber).IsEqualTo(i + 1);
        }
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_SingleCR_ReturnsLine()
    {
        // Arrange: single CR terminator
        var reader = new SpanLineReader("hello\rworld".AsSpan());

        // Act
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        bool ok2 = reader.TryReadLine(out var line2);
        string s2 = line2.ToString();
        bool more = reader.TryReadLine(out _);

        // Assert
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("hello");
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("world");
        await Assert.That(more).IsFalse();
    }

    // --- Mutation-sensitive tests targeting surviving mutants ---

    [Test]
    public async Task TryReadLine_SingleLF_NoTrailingLine()
    {
        // Targets: LF-only with trailing newline — no empty line produced
        var reader = new SpanLineReader("a\n".AsSpan());
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        int n1 = reader.LineNumber;
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(n1).IsEqualTo(1);
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_SingleCRLF_NoTrailingLine()
    {
        // Targets: CRLF handling — both \r and \n consumed
        var reader = new SpanLineReader("a\r\n".AsSpan());
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        int n1 = reader.LineNumber;
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(n1).IsEqualTo(1);
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_TwoLines_LF_BothReturned()
    {
        // Targets: multiple lines with LF
        var reader = new SpanLineReader("a\nb".AsSpan());
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        int n1 = reader.LineNumber;
        bool ok2 = reader.TryReadLine(out var line2);
        string s2 = line2.ToString();
        int n2 = reader.LineNumber;
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(n1).IsEqualTo(1);
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(n2).IsEqualTo(2);
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_TwoLines_CRLF_BothReturned()
    {
        // Targets: multiple lines with CRLF
        var reader = new SpanLineReader("a\r\nb".AsSpan());
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        int n1 = reader.LineNumber;
        bool ok2 = reader.TryReadLine(out var line2);
        string s2 = line2.ToString();
        int n2 = reader.LineNumber;
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(n1).IsEqualTo(1);
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(n2).IsEqualTo(2);
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_TwoLines_CR_BothReturned()
    {
        // Targets: multiple lines with CR only
        var reader = new SpanLineReader("a\rb".AsSpan());
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        int n1 = reader.LineNumber;
        bool ok2 = reader.TryReadLine(out var line2);
        string s2 = line2.ToString();
        int n2 = reader.LineNumber;
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(n1).IsEqualTo(1);
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(n2).IsEqualTo(2);
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_EmptyLine_BetweenContent()
    {
        // Targets: empty line between content lines
        var reader = new SpanLineReader("a\n\nb".AsSpan());
        bool ok1 = reader.TryReadLine(out var line1);
        string s1 = line1.ToString();
        bool ok2 = reader.TryReadLine(out var line2);
        bool empty2 = line2.IsEmpty;
        bool ok3 = reader.TryReadLine(out var line3);
        string s3 = line3.ToString();
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(ok2).IsTrue();
        await Assert.That(empty2).IsTrue();
        await Assert.That(ok3).IsTrue();
        await Assert.That(s3).IsEqualTo("b");
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_ThreeEmptyLines_LF()
    {
        // Targets: consecutive empty lines
        var reader = new SpanLineReader("\n\n\n".AsSpan());
        bool ok1 = reader.TryReadLine(out var l1);
        bool empty1 = l1.IsEmpty;
        bool ok2 = reader.TryReadLine(out var l2);
        bool empty2 = l2.IsEmpty;
        bool ok3 = reader.TryReadLine(out var l3);
        bool empty3 = l3.IsEmpty;
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok1).IsTrue();
        await Assert.That(empty1).IsTrue();
        await Assert.That(ok2).IsTrue();
        await Assert.That(empty2).IsTrue();
        await Assert.That(ok3).IsTrue();
        await Assert.That(empty3).IsTrue();
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_CRLFAtEnd_NoExtraLine()
    {
        // Targets: CRLF at end — both chars consumed, no extra empty line
        var reader = new SpanLineReader("a\r\nb\r\n".AsSpan());
        bool ok1 = reader.TryReadLine(out var l1);
        string s1 = l1.ToString();
        bool ok2 = reader.TryReadLine(out var l2);
        string s2 = l2.ToString();
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_MixedEndings_FourLines()
    {
        // Targets: mixed LF, CRLF, CR in same input
        var reader = new SpanLineReader("a\nb\r\nc\rd".AsSpan());
        bool ok1 = reader.TryReadLine(out var l1);
        string s1 = l1.ToString();
        bool ok2 = reader.TryReadLine(out var l2);
        string s2 = l2.ToString();
        bool ok3 = reader.TryReadLine(out var l3);
        string s3 = l3.ToString();
        bool ok4 = reader.TryReadLine(out var l4);
        string s4 = l4.ToString();
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(ok3).IsTrue();
        await Assert.That(s3).IsEqualTo("c");
        await Assert.That(ok4).IsTrue();
        await Assert.That(s4).IsEqualTo("d");
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_CRFollowedByLF_AsTwoSeparateLines()
    {
        // Targets: CR not followed by LF — treated as separate terminators
        var reader = new SpanLineReader("a\r\nb".AsSpan());
        bool ok1 = reader.TryReadLine(out var l1);
        string s1 = l1.ToString();
        bool ok2 = reader.TryReadLine(out var l2);
        string s2 = l2.ToString();
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok1).IsTrue();
        await Assert.That(s1).IsEqualTo("a");
        await Assert.That(ok2).IsTrue();
        await Assert.That(s2).IsEqualTo("b");
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_LargeInput_AllLinesCorrect()
    {
        // Targets: large buffer handling — mutation of loop would break line count
        var content = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"line{i}"));
        var reader = new SpanLineReader(content.AsSpan());
        var reads = new List<(bool Ok, string Line, int LineNumber)>(100);
        for (int i = 0; i < 100; i++)
        {
            bool ok = reader.TryReadLine(out var line);
            reads.Add((ok, line.ToString(), reader.LineNumber));
        }
        bool more = reader.TryReadLine(out _);
        for (int i = 0; i < 100; i++)
        {
            await Assert.That(reads[i].Ok).IsTrue();
            await Assert.That(reads[i].Line).IsEqualTo($"line{i}");
            await Assert.That(reads[i].LineNumber).IsEqualTo(i + 1);
        }
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_SingleCharNoNewline_ReturnsChar()
    {
        // Targets: single char without newline
        var reader = new SpanLineReader("X".AsSpan());
        bool ok = reader.TryReadLine(out var line);
        string s = line.ToString();
        int n = reader.LineNumber;
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok).IsTrue();
        await Assert.That(s).IsEqualTo("X");
        await Assert.That(n).IsEqualTo(1);
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_OnlyNewline_ReturnsEmptyThenFalse()
    {
        // Targets: input that is only a newline character
        var reader = new SpanLineReader("\n".AsSpan());
        bool ok = reader.TryReadLine(out var line);
        bool empty = line.IsEmpty;
        int n = reader.LineNumber;
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok).IsTrue();
        await Assert.That(empty).IsTrue();
        await Assert.That(n).IsEqualTo(1);
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_OnlyCRLF_ReturnsEmptyThenFalse()
    {
        // Targets: input that is only CRLF
        var reader = new SpanLineReader("\r\n".AsSpan());
        bool ok = reader.TryReadLine(out var line);
        bool empty = line.IsEmpty;
        int n = reader.LineNumber;
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok).IsTrue();
        await Assert.That(empty).IsTrue();
        await Assert.That(n).IsEqualTo(1);
        await Assert.That(more).IsFalse();
    }

    [Test]
    public async Task TryReadLine_OnlyCR_ReturnsEmptyThenFalse()
    {
        // Targets: input that is only CR
        var reader = new SpanLineReader("\r".AsSpan());
        bool ok = reader.TryReadLine(out var line);
        bool empty = line.IsEmpty;
        int n = reader.LineNumber;
        bool more = reader.TryReadLine(out _);
        await Assert.That(ok).IsTrue();
        await Assert.That(empty).IsTrue();
        await Assert.That(n).IsEqualTo(1);
        await Assert.That(more).IsFalse();
    }
}
