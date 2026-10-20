// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using ModernLibs.FileHelpers.Tests.Async.MasterDetail;
using ModernLibs.FileHelpers.Tests.Async.MultiRecord;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// Multi-record and master/detail engines terminate records with <c>NewLineForWrite</c> (default
/// <c>"\r\n"</c> on every platform), like <see cref="FileHelperEngine{T}"/>.
/// </summary>
[Category("Write")]
public sealed class NewLineForWriteTests
{
    private static MultiRecordEngine NewMultiRecordEngine() => new(
        MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
        MultiRecordType.For<MultiAsyncDetail>(line => line.StartsWith("D|", StringComparison.Ordinal)));

    private static readonly object[] s_mixed =
    [
        new MultiAsyncHeader { Kind = "H", Batch = "B1" },
        new MultiAsyncDetail { Kind = "D", Id = 1, Amount = 2.5m },
    ];

    private static MasterDetails<MdAsyncMaster, MdAsyncDetail>[] Groups() =>
    [
        new(new MdAsyncMaster { Kind = "M", Id = "A" }, [new MdAsyncDetail { Kind = "D", MasterId = "A", Quantity = 2 }]),
    ];

    private static MasterDetailEngine<MdAsyncMaster, MdAsyncDetail> NewMasterDetailEngine() =>
        new(line => line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master : RecordAction.Detail);

    [Test]
    [DisplayName("MultiRecordEngine writes \\r\\n by default and honours NewLineForWrite")]
    public async Task MultiRecordEngine_NewLineForWrite()
    {
        // Arrange: default and custom terminator
        var engine = NewMultiRecordEngine();

        // Act + Assert: default is CRLF regardless of platform
        await Assert.That(engine.NewLineForWrite).IsEqualTo("\r\n");
        await Assert.That(engine.WriteString(s_mixed)).IsEqualTo("H|B1\r\nD|1|2.5\r\n");

        engine.NewLineForWrite = "\n";
        await Assert.That(engine.WriteString(s_mixed)).IsEqualTo("H|B1\nD|1|2.5\n");
    }

    [Test]
    [DisplayName("MasterDetailEngine writes \\r\\n by default and honours NewLineForWrite")]
    public async Task MasterDetailEngine_NewLineForWrite()
    {
        // Arrange: default and custom terminator
        var engine = NewMasterDetailEngine();

        // Act + Assert: default is CRLF regardless of platform
        await Assert.That(engine.NewLineForWrite).IsEqualTo("\r\n");
        await Assert.That(engine.WriteString(Groups())).IsEqualTo("M|A\r\nD|A|2\r\n");

        engine.NewLineForWrite = "\n";
        await Assert.That(engine.WriteString(Groups())).IsEqualTo("M|A\nD|A|2\n");
    }
}
