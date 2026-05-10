#pragma warning disable CA1707
#pragma warning disable MA0004

using Xunit;

namespace ModernLibs.FileHelpers.Mutation.Tests;

/// <summary>
/// xUnit mutation-target tests for <see cref="SpanLineReader"/>.
/// Covers all parse paths: empty lines, trailing newline, multi-line,
/// large buffers, LF/CRLF/CR handling, and line numbering (MUT-03).
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
}
