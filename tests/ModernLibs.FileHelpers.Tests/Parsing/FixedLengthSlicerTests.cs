// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// Tests for FixedLengthSlicer (PIPE-03).
/// </summary>
[Category("Phase05")]
[Category("Parsing")]
public sealed class FixedLengthSlicerTests
{
    [Test]
    [DisplayName("FixedLengthSlicer.Slice extracts three fields at specified offsets and widths")]
    public async Task Slice_BasicThreeFields_ExtractsCorrectly()
    {
        // ref struct cannot cross await boundaries — extract results synchronously first.
        // Arrange: Create FixedLengthSlicer with input "AAPL 100 "
        var slicer = new FixedLengthSlicer("AAPL 100 ".AsSpan());
        string field0 = slicer.Slice(0, 4).ToString();
        string field1 = slicer.Slice(5, 3).ToString();

        // Act: Slice third field
        string field2 = slicer.Slice(8, 1).ToString();

        // Assert: Three fields extracted with correct values ("AAPL", "100", " ")
        await Assert.That(field0).IsEqualTo("AAPL");
        await Assert.That(field1).IsEqualTo("100");
        await Assert.That(field2).IsEqualTo(" ");
    }

    [Test]
    [DisplayName("FixedLengthSlicer.Slice clamps width to line end when field exceeds input length")]
    public async Task Slice_FieldNarrowLine_ClampsToEnd()
    {
        // ref struct cannot cross await boundaries — extract results synchronously first.
        // Arrange: Create FixedLengthSlicer with short input "AAPL"
        var slicer = new FixedLengthSlicer("AAPL".AsSpan());

        // Requesting width=20 but only 4 chars available from offset 0
        string clamped = slicer.Slice(0, 20).ToString();

        // Offset beyond end returns empty
        // Act: Attempt to slice beyond input length
        bool beyondEndIsEmpty = slicer.Slice(10, 5).IsEmpty;

        // Assert: Clamped to available content, beyond-end slice returns empty
        await Assert.That(clamped).IsEqualTo("AAPL");
        await Assert.That(beyondEndIsEmpty).IsTrue();
    }
}
