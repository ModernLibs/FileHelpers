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
    public async Task Slice_BasicThreeFields_ExtractsCorrectly()
    {
        // ref struct cannot cross await boundaries — extract results synchronously first.
        var slicer = new FixedLengthSlicer("AAPL 100 ".AsSpan());
        string field0 = slicer.Slice(0, 4).ToString();
        string field1 = slicer.Slice(5, 3).ToString();
        string field2 = slicer.Slice(8, 1).ToString();
        await Assert.That(field0).IsEqualTo("AAPL");
        await Assert.That(field1).IsEqualTo("100");
        await Assert.That(field2).IsEqualTo(" ");
    }

    [Test]
    public async Task Slice_FieldNarrowLine_ClampsToEnd()
    {
        // ref struct cannot cross await boundaries — extract results synchronously first.
        var slicer = new FixedLengthSlicer("AAPL".AsSpan());
        // Requesting width=20 but only 4 chars available from offset 0
        string clamped = slicer.Slice(0, 20).ToString();
        // Offset beyond end returns empty
        bool beyondEndIsEmpty = slicer.Slice(10, 5).IsEmpty;
        await Assert.That(clamped).IsEqualTo("AAPL");
        await Assert.That(beyondEndIsEmpty).IsTrue();
    }
}
