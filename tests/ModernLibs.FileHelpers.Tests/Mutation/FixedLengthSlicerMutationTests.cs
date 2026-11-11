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
/// Mutation-target tests for <see cref="FixedLengthSlicer"/> (moved from the former xUnit project).
/// Covers all parse paths: exact, shorter, longer, variable-length modes,
/// negative offset, non-positive width, and overflow clamping.
/// The ref struct cannot cross an await, so every slice is turned into a string or bool first.
/// </summary>
[Category("Mutation")]
public sealed class FixedLengthSlicerMutationTests
{
    [Test]
    public async Task Slice_BasicThreeFields_ExtractsCorrectly()
    {
        // Arrange: 15-char line with three 5-char fields
        var line = "0123456789ABCDE".AsSpan();
        var slicer = new FixedLengthSlicer(line);

        // Act
        string f0 = slicer.Slice(0, 5).ToString();
        string f1 = slicer.Slice(5, 5).ToString();
        string f2 = slicer.Slice(10, 5).ToString();

        // Assert
        await Assert.That(f0).IsEqualTo("01234");
        await Assert.That(f1).IsEqualTo("56789");
        await Assert.That(f2).IsEqualTo("ABCDE");
    }

    [Test]
    public async Task Slice_NegativeOffset_ReturnsEmpty()
    {
        // Arrange
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: negative offset guard
        bool isEmpty = slicer.Slice(-1, 3).IsEmpty;

        // Assert
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task Slice_ZeroWidth_ReturnsEmpty()
    {
        // Arrange
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: zero width guard
        bool isEmpty = slicer.Slice(0, 0).IsEmpty;

        // Assert
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task Slice_NegativeWidth_ReturnsEmpty()
    {
        // Arrange
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: negative width guard
        bool isEmpty = slicer.Slice(0, -1).IsEmpty;

        // Assert
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task Slice_OffsetBeyondEnd_ReturnsEmpty()
    {
        // Arrange
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: offset >= line.Length guard
        bool isEmpty = slicer.Slice(5, 3).IsEmpty;

        // Assert
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task Slice_OffsetAtEnd_ReturnsEmpty()
    {
        // Arrange
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: offset exactly at end
        bool isEmpty = slicer.Slice(5, 1).IsEmpty;

        // Assert
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task Slice_LineShorterThanWidth_ClampsToAvailable()
    {
        // Arrange: 5-char line, requesting 10 chars
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: width > available, clamps
        string result = slicer.Slice(2, 10).ToString();

        // Assert
        await Assert.That(result).IsEqualTo("llo");
    }

    [Test]
    public async Task Slice_ExactFit_ReturnsFullWidth()
    {
        // Arrange: 5-char line, requesting exactly 5 chars
        var slicer = new FixedLengthSlicer("hello".AsSpan());

        // Act: exact fit
        string result = slicer.Slice(0, 5).ToString();

        // Assert
        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task Slice_EmptyLine_ReturnsEmpty()
    {
        // Arrange
        var slicer = new FixedLengthSlicer("".AsSpan());

        // Act
        bool first = slicer.Slice(0, 1).IsEmpty;
        bool second = slicer.Slice(-1, 1).IsEmpty;

        // Assert
        await Assert.That(first).IsTrue();
        await Assert.That(second).IsTrue();
    }

    [Test]
    public async Task Slice_MiddleField_ExtractsCorrectly()
    {
        // Arrange: extract middle field
        var slicer = new FixedLengthSlicer("ABCDEFGHIJ".AsSpan());

        // Act
        string result = slicer.Slice(3, 4).ToString();

        // Assert
        await Assert.That(result).IsEqualTo("DEFG");
    }

    // --- Mutation-sensitive tests targeting surviving mutants ---

    [Test]
    public async Task Slice_OffsetZero_WidthExactlyLineLength_ReturnsFullLine()
    {
        // Targets: offset=0, width=line.Length — clamping boundary
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        string result = slicer.Slice(0, 5).ToString();
        await Assert.That(result).IsEqualTo("hello");
    }

    [Test]
    public async Task Slice_OffsetOne_WidthFour_ClampsToAvailable()
    {
        // Targets: offset + width > line.Length clamping
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        string result = slicer.Slice(1, 4).ToString();
        await Assert.That(result).IsEqualTo("ello");
    }

    [Test]
    public async Task Slice_OffsetOne_WidthFive_ClampsToFour()
    {
        // Targets: clamping calculation: min(width, line.Length - offset)
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        string result = slicer.Slice(1, 5).ToString();
        await Assert.That(result).IsEqualTo("ello");
    }

    [Test]
    public async Task Slice_OffsetTwo_WidthTen_ClampsToThree()
    {
        // Targets: width much larger than available
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        string result = slicer.Slice(2, 10).ToString();
        await Assert.That(result).IsEqualTo("llo");
    }

    [Test]
    public async Task Slice_OffsetZero_WidthOne_ReturnsSingleChar()
    {
        // Targets: minimal width extraction
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        string result = slicer.Slice(0, 1).ToString();
        await Assert.That(result).IsEqualTo("h");
    }

    [Test]
    public async Task Slice_OffsetFour_WidthOne_ReturnsLastChar()
    {
        // Targets: last character extraction
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        string result = slicer.Slice(4, 1).ToString();
        await Assert.That(result).IsEqualTo("o");
    }

    [Test]
    public async Task Slice_LargeOffsetNegative_WidthPositive_ReturnsEmpty()
    {
        // Targets: negative offset guard
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        bool isEmpty = slicer.Slice(-100, 5).IsEmpty;
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task Slice_OffsetZero_WidthNegativeOne_ReturnsEmpty()
    {
        // Targets: negative width guard
        var slicer = new FixedLengthSlicer("hello".AsSpan());
        bool isEmpty = slicer.Slice(0, -1).IsEmpty;
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task Slice_SingleCharLine_OffsetZero_WidthOne_ReturnsChar()
    {
        // Targets: minimal line, exact extraction
        var slicer = new FixedLengthSlicer("X".AsSpan());
        string result = slicer.Slice(0, 1).ToString();
        await Assert.That(result).IsEqualTo("X");
    }

    [Test]
    public async Task Slice_SingleCharLine_OffsetOne_ReturnsEmpty()
    {
        // Targets: offset exactly at line.Length
        var slicer = new FixedLengthSlicer("X".AsSpan());
        bool isEmpty = slicer.Slice(1, 1).IsEmpty;
        await Assert.That(isEmpty).IsTrue();
    }

    [Test]
    public async Task Slice_EmptyLine_AnyOffset_ReturnsEmpty()
    {
        // Targets: empty line handling
        var slicer = new FixedLengthSlicer("".AsSpan());
        bool first = slicer.Slice(0, 1).IsEmpty;
        bool second = slicer.Slice(1, 1).IsEmpty;
        bool third = slicer.Slice(-1, 1).IsEmpty;
        await Assert.That(first).IsTrue();
        await Assert.That(second).IsTrue();
        await Assert.That(third).IsTrue();
    }

    [Test]
    public async Task Slice_UnicodeChars_PreservesCharacters()
    {
        // Targets: Unicode handling — mutation of slice bounds would corrupt multi-byte chars
        var slicer = new FixedLengthSlicer("名前年齢".AsSpan());
        string first = slicer.Slice(0, 2).ToString();
        string second = slicer.Slice(2, 2).ToString();
        await Assert.That(first).IsEqualTo("名前");
        await Assert.That(second).IsEqualTo("年齢");
    }
}
