#pragma warning disable CA1707
#pragma warning disable MA0004

using Xunit;

namespace ModernLibs.FileHelpers.Mutation.Tests;

/// <summary>
/// xUnit mutation-target tests for <see cref="SpanLineReader"/>.
/// Covers all parse paths: empty lines, trailing newline, multi-line,
/// large buffers, LF/CRLF/CR handling, and line numbering.
/// </summary>
public sealed class SpanLineReaderMutationTests
{
    [Fact]
    public void TryReadLine_SingleLine_NoNewline_ReturnsLine()
    {
        // Arrange: single line with no trailing newline
        var reader = new SpanLineReader("hello".AsSpan());

        // Act & Assert
        Assert.True(reader.TryReadLine(out var line));
        Assert.Equal("hello", line);
        Assert.Equal(1, reader.LineNumber);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_LFOnly_SplitsCorrectly()
    {
        // Arrange: LF-only line endings
        var reader = new SpanLineReader("a\nb\nc".AsSpan());

        // Act & Assert
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.True(reader.TryReadLine(out var line2));
        Assert.Equal("b", line2);
        Assert.True(reader.TryReadLine(out var line3));
        Assert.Equal("c", line3);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_CRLF_SplitsAsOneLine()
    {
        // Arrange: CRLF line endings
        var reader = new SpanLineReader("a\r\nb\r\nc".AsSpan());

        // Act & Assert
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.True(reader.TryReadLine(out var line2));
        Assert.Equal("b", line2);
        Assert.True(reader.TryReadLine(out var line3));
        Assert.Equal("c", line3);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_CROnly_SplitsCorrectly()
    {
        // Arrange: CR-only line endings (old Mac style)
        var reader = new SpanLineReader("a\rb\rc".AsSpan());

        // Act & Assert
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.True(reader.TryReadLine(out var line2));
        Assert.Equal("b", line2);
        Assert.True(reader.TryReadLine(out var line3));
        Assert.Equal("c", line3);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_MixedLineEndings_HandlesAll()
    {
        // Arrange: mixed LF, CRLF, CR
        var reader = new SpanLineReader("a\nb\r\nc\rd".AsSpan());

        // Act & Assert
        Assert.True(reader.TryReadLine(out var line));
        Assert.Equal("a", line);
        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("b", line);
        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("c", line);
        Assert.True(reader.TryReadLine(out line));
        Assert.Equal("d", line);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_EmptyInput_ReturnsFalse()
    {
        // Arrange: empty input
        var reader = new SpanLineReader("".AsSpan());

        // Act & Assert: exhaustion guard
        Assert.False(reader.TryReadLine(out _));
        Assert.Equal(0, reader.LineNumber);
    }

    [Fact]
    public void TryReadLine_TrailingNewline_NoEmptyLastLine()
    {
        // Arrange: trailing newline — reader consumes it, no empty line produced
        var reader = new SpanLineReader("a\n".AsSpan());

        // Act & Assert
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_MultipleEmptyLines_ReturnsEmptyLines()
    {
        // Arrange: consecutive empty lines
        var reader = new SpanLineReader("\n\n\n".AsSpan());

        // Act & Assert
        Assert.True(reader.TryReadLine(out var line1));
        Assert.True(line1.IsEmpty);
        Assert.True(reader.TryReadLine(out var line2));
        Assert.True(line2.IsEmpty);
        Assert.True(reader.TryReadLine(out var line3));
        Assert.True(line3.IsEmpty);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_LineNumber_IncrementsCorrectly()
    {
        // Arrange
        var reader = new SpanLineReader("a\nb\nc".AsSpan());

        // Act & Assert
        Assert.True(reader.TryReadLine(out _));
        Assert.Equal(1, reader.LineNumber);
        Assert.True(reader.TryReadLine(out _));
        Assert.Equal(2, reader.LineNumber);
        Assert.True(reader.TryReadLine(out _));
        Assert.Equal(3, reader.LineNumber);
    }

    [Fact]
    public void TryReadLine_CRLFDoesNotProduceExtraEmptyLine()
    {
        // Arrange: CRLF should count as one terminator, not two
        var reader = new SpanLineReader("a\r\nb".AsSpan());

        // Act & Assert
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.True(reader.TryReadLine(out var line2));
        Assert.Equal("b", line2);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_CRLFatEnd_NoEmptyLineAfter()
    {
        // Arrange: trailing CRLF — reader consumes both chars, no empty line produced
        var reader = new SpanLineReader("a\r\n".AsSpan());

        // Act & Assert
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_CRFollowedByLF_AsSeparateLines()
    {
        // Arrange: CR followed by LF as separate characters (not CRLF pair)
        var reader = new SpanLineReader("a\r\nb".AsSpan());

        // Act: CR at position 1, then \n is consumed as part of CRLF handling
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.True(reader.TryReadLine(out var line2));
        Assert.Equal("b", line2);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_LargeBuffer_HandlesCorrectly()
    {
        // Arrange: large buffer with many lines
        var content = string.Join("\n", Enumerable.Range(0, 1000).Select(i => $"line{i}"));
        var reader = new SpanLineReader(content.AsSpan());

        // Act & Assert
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(reader.TryReadLine(out var line));
            Assert.Equal($"line{i}", line);
            Assert.Equal(i + 1, reader.LineNumber);
        }
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_SingleCR_ReturnsLine()
    {
        // Arrange: single CR terminator
        var reader = new SpanLineReader("hello\rworld".AsSpan());

        // Act & Assert
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("hello", line1);
        Assert.True(reader.TryReadLine(out var line2));
        Assert.Equal("world", line2);
        Assert.False(reader.TryReadLine(out _));
    }

    // --- Mutation-sensitive tests targeting surviving mutants ---

    [Fact]
    public void TryReadLine_SingleLF_NoTrailingLine()
    {
        // Targets: LF-only with trailing newline — no empty line produced
        var reader = new SpanLineReader("a\n".AsSpan());
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.Equal(1, reader.LineNumber);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_SingleCRLF_NoTrailingLine()
    {
        // Targets: CRLF handling — both \r and \n consumed
        var reader = new SpanLineReader("a\r\n".AsSpan());
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.Equal(1, reader.LineNumber);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_TwoLines_LF_BothReturned()
    {
        // Targets: multiple lines with LF
        var reader = new SpanLineReader("a\nb".AsSpan());
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.Equal(1, reader.LineNumber);
        Assert.True(reader.TryReadLine(out var line2));
        Assert.Equal("b", line2);
        Assert.Equal(2, reader.LineNumber);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_TwoLines_CRLF_BothReturned()
    {
        // Targets: multiple lines with CRLF
        var reader = new SpanLineReader("a\r\nb".AsSpan());
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.Equal(1, reader.LineNumber);
        Assert.True(reader.TryReadLine(out var line2));
        Assert.Equal("b", line2);
        Assert.Equal(2, reader.LineNumber);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_TwoLines_CR_BothReturned()
    {
        // Targets: multiple lines with CR only
        var reader = new SpanLineReader("a\rb".AsSpan());
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.Equal(1, reader.LineNumber);
        Assert.True(reader.TryReadLine(out var line2));
        Assert.Equal("b", line2);
        Assert.Equal(2, reader.LineNumber);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_EmptyLine_BetweenContent()
    {
        // Targets: empty line between content lines
        var reader = new SpanLineReader("a\n\nb".AsSpan());
        Assert.True(reader.TryReadLine(out var line1));
        Assert.Equal("a", line1);
        Assert.True(reader.TryReadLine(out var line2));
        Assert.True(line2.IsEmpty);
        Assert.True(reader.TryReadLine(out var line3));
        Assert.Equal("b", line3);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_ThreeEmptyLines_LF()
    {
        // Targets: consecutive empty lines
        var reader = new SpanLineReader("\n\n\n".AsSpan());
        Assert.True(reader.TryReadLine(out var l1));
        Assert.True(l1.IsEmpty);
        Assert.True(reader.TryReadLine(out var l2));
        Assert.True(l2.IsEmpty);
        Assert.True(reader.TryReadLine(out var l3));
        Assert.True(l3.IsEmpty);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_CRLFAtEnd_NoExtraLine()
    {
        // Targets: CRLF at end — both chars consumed, no extra empty line
        var reader = new SpanLineReader("a\r\nb\r\n".AsSpan());
        Assert.True(reader.TryReadLine(out var l1));
        Assert.Equal("a", l1);
        Assert.True(reader.TryReadLine(out var l2));
        Assert.Equal("b", l2);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_MixedEndings_FourLines()
    {
        // Targets: mixed LF, CRLF, CR in same input
        var reader = new SpanLineReader("a\nb\r\nc\rd".AsSpan());
        Assert.True(reader.TryReadLine(out var l1));
        Assert.Equal("a", l1);
        Assert.True(reader.TryReadLine(out var l2));
        Assert.Equal("b", l2);
        Assert.True(reader.TryReadLine(out var l3));
        Assert.Equal("c", l3);
        Assert.True(reader.TryReadLine(out var l4));
        Assert.Equal("d", l4);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_CRFollowedByLF_AsTwoSeparateLines()
    {
        // Targets: CR not followed by LF — treated as separate terminators
        var reader = new SpanLineReader("a\r\nb".AsSpan());
        Assert.True(reader.TryReadLine(out var l1));
        Assert.Equal("a", l1);
        Assert.True(reader.TryReadLine(out var l2));
        Assert.Equal("b", l2);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_LargeInput_AllLinesCorrect()
    {
        // Targets: large buffer handling — mutation of loop would break line count
        var content = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"line{i}"));
        var reader = new SpanLineReader(content.AsSpan());
        for (int i = 0; i < 100; i++)
        {
            Assert.True(reader.TryReadLine(out var line));
            Assert.Equal($"line{i}", line);
            Assert.Equal(i + 1, reader.LineNumber);
        }
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_SingleCharNoNewline_ReturnsChar()
    {
        // Targets: single char without newline
        var reader = new SpanLineReader("X".AsSpan());
        Assert.True(reader.TryReadLine(out var line));
        Assert.Equal("X", line);
        Assert.Equal(1, reader.LineNumber);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_OnlyNewline_ReturnsEmptyThenFalse()
    {
        // Targets: input that is only a newline character
        var reader = new SpanLineReader("\n".AsSpan());
        Assert.True(reader.TryReadLine(out var line));
        Assert.True(line.IsEmpty);
        Assert.Equal(1, reader.LineNumber);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_OnlyCRLF_ReturnsEmptyThenFalse()
    {
        // Targets: input that is only CRLF
        var reader = new SpanLineReader("\r\n".AsSpan());
        Assert.True(reader.TryReadLine(out var line));
        Assert.True(line.IsEmpty);
        Assert.Equal(1, reader.LineNumber);
        Assert.False(reader.TryReadLine(out _));
    }

    [Fact]
    public void TryReadLine_OnlyCR_ReturnsEmptyThenFalse()
    {
        // Targets: input that is only CR
        var reader = new SpanLineReader("\r".AsSpan());
        Assert.True(reader.TryReadLine(out var line));
        Assert.True(line.IsEmpty);
        Assert.Equal(1, reader.LineNumber);
        Assert.False(reader.TryReadLine(out _));
    }
}
