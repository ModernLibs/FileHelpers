// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: The 56-byte fixture record belongs to this test.
#pragma warning disable MA0048

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// The record buffers of an array read go back to the shared pool only up to 1 MB, so the pool does not
/// keep large buffers (and, for records with references, their slots for every full collection) alive.
/// </summary>
[Category("Engines")]
public sealed class RecordBufferPoolingTests
{
    [Test]
    [DisplayName("Record buffers up to 1 MB go back to the pool, larger ones do not, for reference and value records")]
    public async Task PoolsRecordBuffer_CapsAtOneMegabyte()
    {
        // 8-byte elements (a class reference, a struct of two ints): 131072 elements are exactly 1 MB
        await Assert.That(Unsafe.SizeOf<RcRecord>()).IsEqualTo(8);
        await Assert.That(FileHelperEngine<RcRecord>.PoolsRecordBuffer(131_072)).IsTrue();
        await Assert.That(FileHelperEngine<RcRecord>.PoolsRecordBuffer(131_073)).IsFalse();
        await Assert.That(FileHelperEngine<RcRecord>.PoolsRecordBuffer(262_144)).IsFalse();
        await Assert.That(Unsafe.SizeOf<RcStruct>()).IsEqualTo(8);
        await Assert.That(FileHelperEngine<RcStruct>.PoolsRecordBuffer(131_072)).IsTrue();
        await Assert.That(FileHelperEngine<RcStruct>.PoolsRecordBuffer(262_144)).IsFalse();

        // 56-byte records (seven references): the cap is between 18724 and 18725 elements
        await Assert.That(Unsafe.SizeOf<PoolingSevenStrings>()).IsEqualTo(56);
        await Assert.That(FileHelperEngine<PoolingSevenStrings>.PoolsRecordBuffer(16_384)).IsTrue();
        await Assert.That(FileHelperEngine<PoolingSevenStrings>.PoolsRecordBuffer(18_724)).IsTrue();
        await Assert.That(FileHelperEngine<PoolingSevenStrings>.PoolsRecordBuffer(18_725)).IsFalse();
        await Assert.That(FileHelperEngine<PoolingSevenStrings>.PoolsRecordBuffer(32_768)).IsFalse();

        // No overflow for the largest array length
        await Assert.That(FileHelperEngine<PoolingSevenStrings>.PoolsRecordBuffer(int.MaxValue)).IsFalse();
    }
}

/// <summary>A 56-byte struct record: seven string fields.</summary>
[DelimitedRecord(",")]
[StructLayout(LayoutKind.Auto)]
public record struct PoolingSevenStrings
{
    [FieldOrder(1)] public string? A { get; set; }
    [FieldOrder(2)] public string? B { get; set; }
    [FieldOrder(3)] public string? C { get; set; }
    [FieldOrder(4)] public string? D { get; set; }
    [FieldOrder(5)] public string? E { get; set; }
    [FieldOrder(6)] public string? F { get; set; }
    [FieldOrder(7)] public string? G { get; set; }
}
