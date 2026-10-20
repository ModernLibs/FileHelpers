// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture record struct co-located with test class.
#pragma warning disable MA0048
// MA0008: Test fixture record struct — layout attribute not meaningful here.
#pragma warning disable MA0008

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.FixedLength;

/// <summary>
/// Fixed-width <see cref="FixedMode.ExactLength"/> parse for a value-only
/// <c>readonly record struct</c> should stay effectively heap-free (same guard as
/// <see cref="Parsing.ZeroAllocTests"/>).
/// </summary>
[FixedLengthRecord(FixedMode.ExactLength)]
internal readonly record struct FixedWidthStructAllocRecord(
    [property: FieldOrder(1)]
    [property: FieldFixedLength(4)]
    [property: FieldConverter(ConverterKind.Int32)]
    [property: FieldTrim(TrimMode.Both)]
    int Quantity,
    [property: FieldOrder(2)]
    [property: FieldFixedLength(4)]
    [property: FieldConverter(ConverterKind.Int32)]
    [property: FieldTrim(TrimMode.Both)]
    int Code);

/// <summary>Allocation regression guard for fixed-width struct path.</summary>
[Category("FixedWidth")]
[Category("Parsing")]
public sealed class FixedWidthStructAllocationTests
{
    /// <summary>
    /// Golden line length must equal the sum of <see cref="FieldFixedLengthAttribute"/> widths (8).
    /// Threshold of 64 bytes matches <c>ZeroAllocTests</c>: catches boxing regressions while allowing
    /// Debug / framework noise.
    /// </summary>
    [Test]
    [DisplayName("Parse fixed-width readonly record struct allocates at most 64 bytes on heap")]
    public async Task Parse_FixedWidth_ReadOnlyRecordStruct_UnderAllocationThreshold()
    {
        // Arrange: ExactLength record — "  12" + "  34" = 8 characters total.
        const string golden = "  12  34";
        var descriptor = FileHelperRegistry<FixedWidthStructAllocRecord>.Descriptor;

        // Warm-up: JIT the generated Parse before measuring.
        _ = descriptor.Parse(golden.AsSpan());

        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = descriptor.Parse(golden.AsSpan());
        long after = GC.GetAllocatedBytesForCurrentThread();

        await Assert.That(after - before).IsLessThanOrEqualTo(64L);

        var parsed = descriptor.Parse(golden.AsSpan());
        await Assert.That(parsed.Quantity).IsEqualTo(12);
        await Assert.That(parsed.Code).IsEqualTo(34);
    }
}
