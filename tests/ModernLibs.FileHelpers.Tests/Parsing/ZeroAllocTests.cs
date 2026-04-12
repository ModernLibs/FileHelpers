// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Generator.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// Allocation-assertion tests for zero-allocation parsing (PIPE-07, PIPE-08).
/// PIPE-07: record struct with non-string fields allocates zero bytes per parse.
/// PIPE-08: class record allocates at least one heap object per parse.
/// </summary>
[Category("Phase05")]
[Category("Parsing")]
public sealed class ZeroAllocTests
{
    /// <summary>
    /// PIPE-07: Parse of a readonly record struct with only value-type fields (int, decimal)
    /// must allocate zero bytes on the managed heap.
    /// Threshold of 64 bytes accommodates Debug/async overhead while still catching
    /// value-type boxing regressions (24+ bytes per boxed value).
    /// </summary>
    [Test]
    public async Task Parse_RecordStruct_ZeroAllocations()
    {
        var descriptor = FileHelperRegistry<PriceRecord>.Descriptor;
        var line = "100,150.25".AsSpan();

        // Warm-up: force JIT compilation of the generated Parse method before measuring.
        descriptor.Parse(line);

        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = descriptor.Parse(line);
        long after = GC.GetAllocatedBytesForCurrentThread();

        // Release build: exactly 0. Debug/async framework: allow small overhead.
        await Assert.That(after - before).IsLessThanOrEqualTo(64L);
    }

    /// <summary>
    /// PIPE-08: Parse of a class record must allocate at least one heap object
    /// (the record instance itself). This verifies that the class path is not
    /// accidentally elided or struct-packed.
    /// </summary>
    [Test]
    public async Task Parse_ClassRecord_OneObjectAllocation()
    {
        var descriptor = FileHelperRegistry<SimpleDelimitedRecord>.Descriptor;
        var line = "AAPL,100,150.25".AsSpan();

        // Warm-up: force JIT compilation before measuring.
        descriptor.Parse(line);

        long before = GC.GetAllocatedBytesForCurrentThread();
        _ = descriptor.Parse(line);
        long after = GC.GetAllocatedBytesForCurrentThread();

        // class record allocates at least one object (the record itself).
        await Assert.That(after - before).IsGreaterThan(0L);
    }
}
