#pragma warning disable CA1707
#pragma warning disable MA0004

using Xunit;

namespace ModernLibs.FileHelpers.Mutation.Tests;

/// <summary>
/// xUnit mutation-target tests for <see cref="FixedLengthSlicer"/>.
/// Covers all parse paths: exact, shorter, longer, variable-length modes,
/// negative offset, non-positive width, and overflow clamping.
/// </summary>
public sealed class FixedLengthSlicerMutationTests
{
    [Fact]
    public void Slice_BasicThreeFields_ExtractsCorrectly()
    {
        // Arrange: 15-char line with three 5-char fields
        var line = "0123456789ABCDE".AsSpan();
        var slicer = new FixedLengthSlicer(line);

        // Act & Assert
        Assert.Equal("01234", slicer.Slice(0, 5));
        Assert.Equal("56789", slicer.Slice(5, 5));
        Assert.Equal("ABCDE", slicer.Slice(10, 5));
    }

    [Fact]
    public void Slice_NegativeOffset_ReturnsEmpty()
    {
        // Arrange
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: negative offset guard
        var result = slicer.Slice(-1, 3);

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Slice_ZeroWidth_ReturnsEmpty()
    {
        // Arrange
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: zero width guard
        var result = slicer.Slice(0, 0);

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Slice_NegativeWidth_ReturnsEmpty()
    {
        // Arrange
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: negative width guard
        var result = slicer.Slice(0, -1);

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Slice_OffsetBeyondEnd_ReturnsEmpty()
    {
        // Arrange
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: offset >= line.Length guard
        var result = slicer.Slice(5, 3);

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Slice_OffsetAtEnd_ReturnsEmpty()
    {
        // Arrange
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: offset exactly at end
        var result = slicer.Slice(5, 1);

        // Assert
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void Slice_LineShorterThanWidth_ClampsToAvailable()
    {
        // Arrange: 5-char line, requesting 10 chars
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: width > available, clamps
        var result = slicer.Slice(2, 10);

        // Assert
        Assert.Equal("llo", result);
    }

    [Fact]
    public void Slice_ExactFit_ReturnsFullWidth()
    {
        // Arrange: 5-char line, requesting exactly 5 chars
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: exact fit
        var result = slicer.Slice(0, 5);

        // Assert
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Slice_EmptyLine_ReturnsEmpty()
    {
        // Arrange
        var slicer = new FixedLengthSlicer("".AsSpan());

        // Act & Assert
        Assert.True(slicer.Slice(0, 1).IsEmpty);
        Assert.True(slicer.Slice(-1, 1).IsEmpty);
    }

    [Fact]
    public void Slice_MiddleField_ExtractsCorrectly()
    {
        // Arrange: extract middle field
        var slicer = new FixedLengthSlicer("ABCDEFGHIJ".AsSpan());

        // Act
        var result = slicer.Slice(3, 4);

        // Assert
        Assert.Equal("DEFG", result);
    }

    // --- Mutation-sensitive tests targeting surviving mutants ---

    [Fact]
    public void Slice_OffsetZero_WidthExactlyLineLength_ReturnsFullLine()
    {
        // Targets: offset=0, width=line.Length — clamping boundary
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        Assert.Equal("hello", slicer.Slice(0, 5));
    }

    [Fact]
    public void Slice_OffsetOne_WidthFour_ClampsToAvailable()
    {
        // Targets: offset + width > line.Length clamping
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        Assert.Equal("ello", slicer.Slice(1, 4));
    }

    [Fact]
    public void Slice_OffsetOne_WidthFive_ClampsToFour()
    {
        // Targets: clamping calculation: min(width, line.Length - offset)
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        Assert.Equal("ello", slicer.Slice(1, 5));
    }

    [Fact]
    public void Slice_OffsetTwo_WidthTen_ClampsToThree()
    {
        // Targets: width much larger than available
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        Assert.Equal("llo", slicer.Slice(2, 10));
    }

    [Fact]
    public void Slice_OffsetZero_WidthOne_ReturnsSingleChar()
    {
        // Targets: minimal width extraction
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        Assert.Equal("h", slicer.Slice(0, 1));
    }

    [Fact]
    public void Slice_OffsetFour_WidthOne_ReturnsLastChar()
    {
        // Targets: last character extraction
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        Assert.Equal("o", slicer.Slice(4, 1));
    }

    [Fact]
    public void Slice_LargeOffsetNegative_WidthPositive_ReturnsEmpty()
    {
        // Targets: negative offset guard
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        Assert.True(slicer.Slice(-100, 5).IsEmpty);
    }

    [Fact]
    public void Slice_OffsetZero_WidthNegativeOne_ReturnsEmpty()
    {
        // Targets: negative width guard
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        Assert.True(slicer.Slice(0, -1).IsEmpty);
    }

    [Fact]
    public void Slice_SingleCharLine_OffsetZero_WidthOne_ReturnsChar()
    {
        // Targets: minimal line, exact extraction
        var slicer = new FixedLengthSlicer("X".AsSpan());
        Assert.Equal("X", slicer.Slice(0, 1));
    }

    [Fact]
    public void Slice_SingleCharLine_OffsetOne_ReturnsEmpty()
    {
        // Targets: offset exactly at line.Length
        var slicer = new FixedLengthSlicer("X".AsSpan());
        Assert.True(slicer.Slice(1, 1).IsEmpty);
    }

    [Fact]
    public void Slice_EmptyLine_AnyOffset_ReturnsEmpty()
    {
        // Targets: empty line handling
        var slicer = new FixedLengthSlicer("".AsSpan());
        Assert.True(slicer.Slice(0, 1).IsEmpty);
        Assert.True(slicer.Slice(1, 1).IsEmpty);
        Assert.True(slicer.Slice(-1, 1).IsEmpty);
    }

    [Fact]
    public void Slice_UnicodeChars_PreservesCharacters()
    {
        // Targets: Unicode handling — mutation of slice bounds would corrupt multi-byte chars
        var slicer = new FixedLengthSlicer("名前年齢".AsSpan());
        Assert.Equal("名前", slicer.Slice(0, 2));
        Assert.Equal("年齢", slicer.Slice(2, 2));
    }
}
