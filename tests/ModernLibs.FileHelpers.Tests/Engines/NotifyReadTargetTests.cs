// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>Settable class record: its descriptor can parse into an existing instance.</summary>
[DelimitedRecord(",")]
public sealed class NotifyTargetRecord : INotifyRead<NotifyTargetRecord>
{
    [FieldHidden] private int _beforeReadCalls;
    [FieldHidden] private string? _lineSeenBeforeRead;

    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;

    public int BeforeReadCalls() => _beforeReadCalls;

    public string? LineSeenBeforeRead() => _lineSeenBeforeRead;

    public void BeforeRead(BeforeReadEventArgs<NotifyTargetRecord> e)
    {
        _beforeReadCalls++;
        _lineSeenBeforeRead = e.RecordLine;
    }

    public void AfterRead(AfterReadEventArgs<NotifyTargetRecord> e)
    {
    }
}

/// <summary>
/// <see cref="INotifyRead{T}.BeforeRead"/> runs on the instance that then receives the parsed values (3.x),
/// so state set in BeforeRead survives into the returned record.
/// </summary>
[Category("ReadCore")]
public sealed class NotifyReadTargetTests
{
    [Test]
    [DisplayName("BeforeRead is called on the returned record (batch read)")]
    public async Task BeforeRead_CalledOnReturnedRecord()
    {
        // Arrange
        var engine = new FileHelperEngine<NotifyTargetRecord>();

        // Act
        var records = engine.ReadString("1,a\r\n2,b\r\n");

        // Assert: every returned record saw exactly its own BeforeRead
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].BeforeReadCalls()).IsEqualTo(1);
        await Assert.That(records[0].LineSeenBeforeRead()).IsEqualTo("1,a");
        await Assert.That(records[1].BeforeReadCalls()).IsEqualTo(1);
        await Assert.That(records[1].LineSeenBeforeRead()).IsEqualTo("2,b");
        await Assert.That(records[1].Name).IsEqualTo("b");
    }

    [Test]
    [DisplayName("BeforeRead is called on the recycled instance when RecycleRecords is on")]
    public async Task BeforeRead_CalledOnRecycledInstance()
    {
        // Arrange
        var engine = new FileHelperEngine<NotifyTargetRecord> { RecycleRecords = true };
        var seen = new List<(NotifyTargetRecord Instance, int Calls, string Name)>();

        // Act: callback read with recycling
        engine.ReadString("1,a\r\n2,b\r\n", r => seen.Add((r, r.BeforeReadCalls(), r.Name)));

        // Assert: one instance, BeforeRead called on it before each parse
        await Assert.That(seen.Count).IsEqualTo(2);
        await Assert.That(ReferenceEquals(seen[0].Instance, seen[1].Instance)).IsTrue();
        await Assert.That(seen[0].Calls).IsEqualTo(1);
        await Assert.That(seen[1].Calls).IsEqualTo(2);
        await Assert.That(seen[1].Name).IsEqualTo("b");
    }

    [Test]
    [DisplayName("A record skipped in BeforeRead is not returned")]
    public async Task BeforeRead_SkipStillWorks()
    {
        // Arrange: event skips record 1
        var engine = new FileHelperEngine<NotifyTargetRecord>();
        engine.BeforeReadRecord += (_, e) => e.SkipThisRecord = e.RecordLine.StartsWith('1');

        // Act
        var records = engine.ReadString("1,a\r\n2,b\r\n");

        // Assert
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Id).IsEqualTo(2);
        await Assert.That(records[0].BeforeReadCalls()).IsEqualTo(1);
    }
}
