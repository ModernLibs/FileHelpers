// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional.
#pragma warning disable MA0048
// CS8602: Null-forgiving operator used after IsNotNull assertion.
#pragma warning disable CS8602

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Phase16.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase16;

/// <summary>
/// INTF-03, INTF-04, INTF-06, INTF-07, INTF-08: Test suite for INotifyWrite interface behavior.
/// </summary>
[Category("Phase16")]
[Category("Events")]
[Category("Interfaces")]
public sealed class INotifyWriteTests
{
    [Test]
    [DisplayName("BeforeWrite_CalledOncePerRecord_WithCorrectArgs - interface method receives record")]
    public async Task BeforeWrite_CalledOncePerRecord_WithCorrectArgs()
    {
        // Arrange
        var engine = new FileHelperEngine<NotifiableWriteRecord>();
        var records = new[]
        {
            new NotifiableWriteRecord { Id = 1, Name = "First" },
            new NotifiableWriteRecord { Id = 2, Name = "Second" }
        };

        // Act
        engine.WriteString(records);

        // Assert: BeforeWrite called for each record with correct args
        await Assert.That(records[0].BeforeWriteCount).IsEqualTo(1);
        await Assert.That(records[1].BeforeWriteCount).IsEqualTo(1);
        await Assert.That(records[0].LastBeforeWriteArgs).IsNotNull();
        await Assert.That(records[0].LastBeforeWriteArgs!.Record.Id).IsEqualTo(1);
        await Assert.That(records[0].LastBeforeWriteArgs.RecordNumber).IsEqualTo(1);
    }

    [Test]
    [DisplayName("AfterWrite_CalledOncePerRecord_WithFormattedLine - interface method gets formatted output")]
    public async Task AfterWrite_CalledOncePerRecord_WithFormattedLine()
    {
        // Arrange
        var engine = new FileHelperEngine<NotifiableWriteRecord>();
        var records = new[]
        {
            new NotifiableWriteRecord { Id = 1, Name = "Test" }
        };

        // Act
        engine.WriteString(records);

        // Assert: AfterWrite called with formatted CSV line
        await Assert.That(records[0].AfterWriteCount).IsEqualTo(1);
        await Assert.That(records[0].LastAfterWriteArgs).IsNotNull();
        await Assert.That(records[0].LastAfterWriteArgs!.RecordLine).IsEqualTo("1,Test");
    }

    [Test]
    [DisplayName("BothWriteMethods_CalledForEachRecord - BeforeWrite and AfterWrite both fire")]
    public async Task BothWriteMethods_CalledForEachRecord()
    {
        // Arrange
        var engine = new FileHelperEngine<DualNotifiableRecord>();
        var records = new[]
        {
            new DualNotifiableRecord { Id = 1, Value = "First" },
            new DualNotifiableRecord { Id = 2, Value = "Second" }
        };

        // Act
        engine.WriteString(records);

        // Assert: Both methods called for each record
        await Assert.That(records[0].BeforeWriteCount).IsEqualTo(1);
        await Assert.That(records[0].AfterWriteCount).IsEqualTo(1);
        await Assert.That(records[1].BeforeWriteCount).IsEqualTo(1);
        await Assert.That(records[1].AfterWriteCount).IsEqualTo(1);
    }

    [Test]
    [DisplayName("BeforeWrite_SkipThisRecord_ExcludesRecord - interface can filter from output")]
    public async Task BeforeWrite_SkipThisRecord_ExcludesRecord()
    {
        // Arrange: Use fixture with skip logic for negative IDs
        var engine = new FileHelperEngine<NotifiableWriteRecord>();
        var records = new[]
        {
            new NotifiableWriteRecord { Id = 1, Name = "Keep" },
            new NotifiableWriteRecord { Id = -1, Name = "Skip" },
            new NotifiableWriteRecord { Id = 2, Name = "Keep" }
        };

        // Act
        var output = engine.WriteString(records);

        // Assert: Only 2 lines in output (the -1 record was skipped)
        var lines = output.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines.Length).IsEqualTo(2);
        await Assert.That(lines[0]).IsEqualTo("1,Keep");
        await Assert.That(lines[1]).IsEqualTo("2,Keep");
    }

    [Test]
    [DisplayName("AfterWrite_ModifyRecordLine_AffectsOutput - interface can transform output")]
    public async Task AfterWrite_ModifyRecordLine_AffectsOutput()
    {
        // Arrange: Use fixture that adds comment to ID=42 records
        var engine = new FileHelperEngine<NotifiableWriteRecord>();
        var records = new[]
        {
            new NotifiableWriteRecord { Id = 1, Name = "Normal" },
            new NotifiableWriteRecord { Id = 42, Name = "Special" },
            new NotifiableWriteRecord { Id = 2, Name = "Normal" }
        };

        // Act
        var output = engine.WriteString(records);

        // Assert: ID 42 has comment suffix added by AfterWrite
        var lines = output.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines.Length).IsEqualTo(3);
        await Assert.That(lines[0]).IsEqualTo("1,Normal");
        await Assert.That(lines[1]).IsEqualTo("42,Special # Special record");
        await Assert.That(lines[2]).IsEqualTo("2,Normal");
    }

    [Test]
    [DisplayName("InterfaceMethods_CalledAlongsideEngineEvents - both event and interface fire")]
    public async Task InterfaceMethods_CalledAlongsideEngineEvents()
    {
        // Arrange
        var engine = new FileHelperEngine<DualNotifiableRecord>();
        int eventBeforeWriteCount = 0;
        int eventAfterWriteCount = 0;

        engine.BeforeWriteRecord += (sender, e) => eventBeforeWriteCount++;
        engine.AfterWriteRecord += (sender, e) => eventAfterWriteCount++;

        var records = new[]
        {
            new DualNotifiableRecord { Id = 1, Value = "Test" }
        };

        // Act
        engine.WriteString(records);

        // Assert: Both event and interface methods were called
        await Assert.That(eventBeforeWriteCount).IsEqualTo(1);
        await Assert.That(eventAfterWriteCount).IsEqualTo(1);
        await Assert.That(records[0].BeforeWriteCount).IsEqualTo(1);
        await Assert.That(records[0].AfterWriteCount).IsEqualTo(1);
    }

    [Test]
    [DisplayName("SameEventArgs_PassedToEventAndInterface - event and interface receive same instance")]
    public async Task SameEventArgs_PassedToEventAndInterface()
    {
        // Arrange
        var engine = new FileHelperEngine<DualNotifiableRecord>();
        BeforeWriteEventArgs<DualNotifiableRecord>? capturedEventBeforeArgs = null;
        AfterWriteEventArgs<DualNotifiableRecord>? capturedEventAfterArgs = null;

        engine.BeforeWriteRecord += (sender, e) => capturedEventBeforeArgs = e;
        engine.AfterWriteRecord += (sender, e) => capturedEventAfterArgs = e;

        var records = new[] { new DualNotifiableRecord { Id = 1, Value = "Test" } };

        // Act
        engine.WriteString(records);

        // Assert: Same args instances passed to both
        await Assert.That(capturedEventBeforeArgs).IsNotNull();
        await Assert.That(capturedEventAfterArgs).IsNotNull();
        await Assert.That(ReferenceEquals(capturedEventBeforeArgs, records[0].LastBeforeWriteArgs)).IsTrue();
        await Assert.That(ReferenceEquals(capturedEventAfterArgs, records[0].LastAfterWriteArgs)).IsTrue();
    }

    // -------------------------------------------------------------------------
    // WriteStream tests
    // -------------------------------------------------------------------------

    [Test]
    [DisplayName("WriteStream_InterfaceMethodsFire - streaming write calls interface")]
    public async Task WriteStream_InterfaceMethodsFire()
    {
        // Arrange
        var engine = new FileHelperEngine<NotifiableWriteRecord>();
        var records = new[]
        {
            new NotifiableWriteRecord { Id = 1, Name = "Test" }
        };
        using var writer = new StringWriter();

        // Act
        engine.WriteStream(writer, records);

        // Assert
        await Assert.That(records[0].BeforeWriteCount).IsEqualTo(1);
        await Assert.That(records[0].AfterWriteCount).IsEqualTo(1);
    }

    // -------------------------------------------------------------------------
    // AppendToFile tests
    // -------------------------------------------------------------------------

    [Test]
    [DisplayName("AppendToFile_InterfaceMethodsFire - append calls interface methods")]
    public async Task AppendToFile_InterfaceMethodsFire()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var engine = new FileHelperEngine<NotifiableWriteRecord>();
            var record = new NotifiableWriteRecord { Id = 1, Name = "Append" };

            // Act
            engine.AppendToFile(tempFile, record);

            // Assert
            await Assert.That(record.BeforeWriteCount).IsEqualTo(1);
            await Assert.That(record.AfterWriteCount).IsEqualTo(1);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    [DisplayName("AppendToFile_SingleRecord_SkipThisRecord_Works - interface can skip in append")]
    public async Task AppendToFile_SingleRecord_SkipThisRecord_Works()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var engine = new FileHelperEngine<NotifiableWriteRecord>();
            var record = new NotifiableWriteRecord { Id = -1, Name = "Skip" }; // Negative ID triggers skip

            // Act
            engine.AppendToFile(tempFile, record);

            // Assert: File should be empty (no content written for skipped record)
            var content = await File.ReadAllTextAsync(tempFile);
            await Assert.That(content).IsEqualTo(string.Empty);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // -------------------------------------------------------------------------
    // FileHelperAsyncEngine tests
    // -------------------------------------------------------------------------

    [Test]
    [DisplayName("AsyncEngine_BeforeWrite_CalledCorrectly - FileHelperAsyncEngine calls interface")]
    public async Task AsyncEngine_BeforeWrite_CalledCorrectly()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<NotifiableWriteRecord>();
        engine.BeginWriteString();
        var record = new NotifiableWriteRecord { Id = 1, Name = "Test" };

        // Act
        engine.WriteNext(record);
        engine.Close();

        // Assert
        await Assert.That(record.BeforeWriteCount).IsEqualTo(1);
    }

    [Test]
    [DisplayName("AsyncEngine_AfterWrite_CalledCorrectly - FileHelperAsyncEngine calls interface")]
    public async Task AsyncEngine_AfterWrite_CalledCorrectly()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<NotifiableWriteRecord>();
        engine.BeginWriteString();
        var record = new NotifiableWriteRecord { Id = 1, Name = "Test" };

        // Act
        engine.WriteNext(record);
        engine.Close();

        // Assert
        await Assert.That(record.AfterWriteCount).IsEqualTo(1);
        await Assert.That(record.LastAfterWriteArgs).IsNotNull();
    }

    [Test]
    [DisplayName("AsyncEngine_BeforeWrite_SkipThisRecord_Works - interface can skip in async engine")]
    public async Task AsyncEngine_BeforeWrite_SkipThisRecord_Works()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<NotifiableWriteRecord>();
        engine.BeginWriteString();
        var record = new NotifiableWriteRecord { Id = -1, Name = "Skip" };

        // Act
        engine.WriteNext(record);
        var output = engine.EndWriteString();

        // Assert: Output should not contain the skipped record
        await Assert.That(output).DoesNotContain("Skip");
        await Assert.That(output).IsEqualTo(string.Empty);
    }

    [Test]
    [DisplayName("AsyncEngine_AfterWrite_ModifyRecordLine_Works - interface can modify async output")]
    public async Task AsyncEngine_AfterWrite_ModifyRecordLine_Works()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<NotifiableWriteRecord>();
        engine.BeginWriteString();
        var record = new NotifiableWriteRecord { Id = 42, Name = "Special" }; // ID 42 triggers modification

        // Act
        engine.WriteNext(record);
        var output = engine.EndWriteString();

        // Assert: Output contains the comment added by AfterWrite
        await Assert.That(output).Contains("# Special record");
    }

    [Test]
    [DisplayName("AsyncEngine_WriteNext_MultipleRecords_InterfaceCalledForEach - batch write calls interface per record")]
    public async Task AsyncEngine_WriteNext_MultipleRecords_InterfaceCalledForEach()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<NotifiableWriteRecord>();
        engine.BeginWriteString();
        var records = new[]
        {
            new NotifiableWriteRecord { Id = 1, Name = "First" },
            new NotifiableWriteRecord { Id = 2, Name = "Second" }
        };

        // Act
        engine.WriteNext(records);
        engine.Close();

        // Assert
        await Assert.That(records[0].BeforeWriteCount).IsEqualTo(1);
        await Assert.That(records[0].AfterWriteCount).IsEqualTo(1);
        await Assert.That(records[1].BeforeWriteCount).IsEqualTo(1);
        await Assert.That(records[1].AfterWriteCount).IsEqualTo(1);
    }

    // -------------------------------------------------------------------------
    // RecordNumber verification tests
    // -------------------------------------------------------------------------

    [Test]
    [DisplayName("RecordNumber_IncrementsCorrectly - interface receives correct 1-based record number")]
    public async Task RecordNumber_IncrementsCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<NotifiableWriteRecord>();
        var records = new[]
        {
            new NotifiableWriteRecord { Id = 1, Name = "First" },
            new NotifiableWriteRecord { Id = 2, Name = "Second" },
            new NotifiableWriteRecord { Id = 3, Name = "Third" }
        };

        // Act
        engine.WriteString(records);

        // Assert: Each record received correct sequential record number
        await Assert.That(records[0].LastBeforeWriteArgs!.RecordNumber).IsEqualTo(1);
        await Assert.That(records[0].LastAfterWriteArgs!.RecordNumber).IsEqualTo(1);
        await Assert.That(records[1].LastBeforeWriteArgs!.RecordNumber).IsEqualTo(2);
        await Assert.That(records[1].LastAfterWriteArgs!.RecordNumber).IsEqualTo(2);
        await Assert.That(records[2].LastBeforeWriteArgs!.RecordNumber).IsEqualTo(3);
        await Assert.That(records[2].LastAfterWriteArgs!.RecordNumber).IsEqualTo(3);
    }

    [Test]
    [DisplayName("RecordNumber_SkippedRecords_NotCounted - skipped records don't increment counter")]
    public async Task RecordNumber_SkippedRecords_NotCounted()
    {
        // Arrange: Second record has negative ID (triggers skip in BeforeWrite)
        var engine = new FileHelperEngine<NotifiableWriteRecord>();
        var records = new[]
        {
            new NotifiableWriteRecord { Id = 1, Name = "First" },
            new NotifiableWriteRecord { Id = -1, Name = "Skip" },
            new NotifiableWriteRecord { Id = 2, Name = "Third" }
        };

        // Act
        engine.WriteString(records);

        // Assert: Record numbers skip the skipped record (1, 2 for written records)
        await Assert.That(records[0].LastBeforeWriteArgs!.RecordNumber).IsEqualTo(1);
        await Assert.That(records[2].LastBeforeWriteArgs!.RecordNumber).IsEqualTo(2);
    }
}
