// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using System.Text;
using ModernLibs.FileHelpers.Tests.Async.MasterDetail;
using ModernLibs.FileHelpers.Tests.Async.MultiRecord;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>A stream passed to <c>ReadAsync(Stream)</c> belongs to the caller and stays open.</summary>
[Category("Async")]
public sealed class StreamOwnershipTests
{
    [Test]
    [DisplayName("MultiRecordAsyncEngine.ReadAsync(Stream) leaves the caller's stream open")]
    public async Task MultiRecordAsync_ReadAsyncStream_LeavesStreamOpen()
    {
        // Arrange: engine and a caller-owned stream
        var engine = new MultiRecordAsyncEngine(
            MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<MultiAsyncDetail>(line => line.StartsWith("D|", StringComparison.Ordinal)));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("H|B1\nD|1|2.5\n"));

        // Act: read everything
        int count = 0;
        await foreach (var _ in engine.ReadAsync(stream))
            count++;

        // Assert: records read, stream still usable
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(stream.CanRead).IsTrue();
        stream.Position = 0;
        await Assert.That(stream.ReadByte()).IsEqualTo((int)'H');
    }

    [Test]
    [DisplayName("MasterDetailAsyncEngine.ReadAsync(Stream) leaves the caller's stream open")]
    public async Task MasterDetailAsync_ReadAsyncStream_LeavesStreamOpen()
    {
        // Arrange: engine and a caller-owned stream
        var engine = new MasterDetailAsyncEngine<MdAsyncMaster, MdAsyncDetail>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master : RecordAction.Detail);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("M|A\nD|A|2\n"));

        // Act: read everything
        int count = 0;
        await foreach (var _ in engine.ReadAsync(stream))
            count++;

        // Assert: group read, stream still usable
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(stream.CanRead).IsTrue();
        stream.Position = 0;
        await Assert.That(stream.ReadByte()).IsEqualTo((int)'M');
    }
}
