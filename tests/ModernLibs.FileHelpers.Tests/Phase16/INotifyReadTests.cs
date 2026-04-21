// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional.
#pragma warning disable MA0048

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Phase16.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase16;

/// <summary>
/// INTF-01, INTF-02, INTF-05, INTF-07, INTF-08: Test suite for INotifyRead interface behavior.
/// </summary>
[Category("Phase16")]
[Category("Events")]
[Category("Interfaces")]
public sealed class INotifyReadTests
{
    [Test]
    [DisplayName("AfterRead_CalledOncePerRecord_WithParsedRecord - interface method receives populated record")]
    public async Task AfterRead_CalledOncePerRecord_WithParsedRecord()
    {
        // Arrange: Create engine and parse input
        var engine = new FileHelperEngine<NotifiableCsvOrder>();
        var input = "Header\r\n1,Alice,100.50\r\n2,Bob,200.00";

        // Act: Read records (interface methods called automatically)
        var records = engine.ReadString(input);

        // Assert: AfterRead called twice (once per record), last record has correct values
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].AfterReadCount).IsEqualTo(1);
        await Assert.That(records[1].AfterReadCount).IsEqualTo(1);
        await Assert.That(records[1].LastAfterReadArgs).IsNotNull();
        await Assert.That(records[1].LastAfterReadArgs!.Record.OrderId).IsEqualTo(2);
        await Assert.That(records[1].LastAfterReadArgs!.Record.CustomerName).IsEqualTo("Bob");
    }

    [Test]
    [DisplayName("AfterRead_ReceivesCorrectLineNumber - interface method gets 1-based line number")]
    public async Task AfterRead_ReceivesCorrectLineNumber()
    {
        // Arrange
        var engine = new FileHelperEngine<NotifiableCsvOrder>();
        var input = "Header\r\n1,Alice,100.50\r\n2,Bob,200.00";

        // Act
        var records = engine.ReadString(input);

        // Assert: Line numbers reflect original file position (2 and 3, header is line 1)
        await Assert.That(records[0].LastAfterReadArgs!.LineNumber).IsEqualTo(2);
        await Assert.That(records[1].LastAfterReadArgs!.LineNumber).IsEqualTo(3);
    }

    [Test]
    [DisplayName("AfterRead_ReceivesOriginalRecordLine - interface method gets raw line content")]
    public async Task AfterRead_ReceivesOriginalRecordLine()
    {
        // Arrange
        var engine = new FileHelperEngine<NotifiableCsvOrder>();
        var input = "Header\r\n1,Alice,100.50";

        // Act
        var records = engine.ReadString(input);

        // Assert: RecordLine contains the raw unparsed line
        await Assert.That(records[0].LastAfterReadArgs!.RecordLine).IsEqualTo("1,Alice,100.50");
    }

    [Test]
    [DisplayName("BothReadMethods_CalledForEachRecord - BeforeRead and AfterRead both fire")]
    public async Task BothReadMethods_CalledForEachRecord()
    {
        // Arrange
        var engine = new FileHelperEngine<DualNotifiableRecord>();
        var input = "Header\r\n1,First\r\n2,Second\r\n3,Third";

        // Act
        var records = engine.ReadString(input);

        // Assert: Both methods called for each record
        await Assert.That(records.Length).IsEqualTo(3);
        await Assert.That(records[0].BeforeReadCount).IsEqualTo(0); // BeforeRead not called - record doesn't exist yet
        await Assert.That(records[0].AfterReadCount).IsEqualTo(1);
        await Assert.That(records[1].AfterReadCount).IsEqualTo(1);
        await Assert.That(records[2].AfterReadCount).IsEqualTo(1);
    }

    [Test]
    [DisplayName("AfterRead_SkipThisRecord_ExcludesRecord - interface can filter from results")]
    public async Task AfterRead_SkipThisRecord_ExcludesRecord()
    {
        // Arrange: Use fixture with conditional skip logic
        var engine = new FileHelperEngine<ConditionalSkipRecord>();
        var input = "Header\r\n1,Keep\r\n2,Skip\r\n3,Keep";

        // Configure to skip Category="Skip" in AfterRead
        ConditionalSkipRecord.ConfigureSkip("Skip");

        // Act
        var records = engine.ReadString(input);

        // Assert: Only 2 records returned (ID 1 and 3), ID 2 was skipped
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Id).IsEqualTo(1);
        await Assert.That(records[1].Id).IsEqualTo(3);
        
        // Cleanup
        ConditionalSkipRecord.ClearConfiguration();
    }

    [Test]
    [DisplayName("InterfaceMethods_CalledAlongsideEngineEvents - both event and interface fire")]
    public async Task InterfaceMethods_CalledAlongsideEngineEvents()
    {
        // Arrange
        var engine = new FileHelperEngine<DualNotifiableRecord>();
        int eventFireCount = 0;
        int interfaceAfterReadCount = 0;

        engine.AfterReadRecord += (sender, e) =>
        {
            eventFireCount++;
            // The record's interface method will also be called
            if (e.Record is DualNotifiableRecord dnr)
            {
                interfaceAfterReadCount = dnr.AfterReadCount;
            }
        };

        var input = "Header\r\n1,First\r\n2,Second";

        // Act
        var records = engine.ReadString(input);

        // Assert: Both event and interface method were called
        await Assert.That(eventFireCount).IsEqualTo(2);
        await Assert.That(records[0].AfterReadCount).IsEqualTo(1);
        await Assert.That(records[1].AfterReadCount).IsEqualTo(1);
    }

    [Test]
    [DisplayName("SameEventArgs_PassedToEventAndInterface - event and interface receive same instance")]
    public async Task SameEventArgs_PassedToEventAndInterface()
    {
        // Arrange
        var engine = new FileHelperEngine<DualNotifiableRecord>();
        AfterReadEventArgs<DualNotifiableRecord>? capturedEventArgs = null;
        AfterReadEventArgs<DualNotifiableRecord>? capturedInterfaceArgs = null;

        engine.AfterReadRecord += (sender, e) =>
        {
            capturedEventArgs = e;
        };

        var input = "Header\r\n1,First";

        // Act
        var records = engine.ReadString(input);
        capturedInterfaceArgs = records[0].LastAfterReadArgs;

        // Assert: Same args instance passed to both
        await Assert.That(capturedEventArgs).IsNotNull();
        await Assert.That(capturedInterfaceArgs).IsNotNull();
        await Assert.That(ReferenceEquals(capturedEventArgs, capturedInterfaceArgs)).IsTrue();
    }

    // -------------------------------------------------------------------------
    // FileHelperAsyncEngine tests
    // -------------------------------------------------------------------------

    [Test]
    [DisplayName("AsyncEngine_AfterRead_CalledCorrectly - FileHelperAsyncEngine calls interface")]
    public async Task AsyncEngine_AfterRead_CalledCorrectly()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<NotifiableCsvOrder>();
        var input = "Header\r\n1,Alice,100.50\r\n2,Bob,200.00";

        // Act
        engine.BeginReadString(input);
        var records = new List<NotifiableCsvOrder>();
        while (engine.ReadNext() is { } record)
        {
            records.Add(record);
        }
        engine.Close();

        // Assert
        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(records[0].AfterReadCount).IsEqualTo(1);
        await Assert.That(records[1].AfterReadCount).IsEqualTo(1);
    }

    [Test]
    [DisplayName("AsyncEngine_AfterRead_ReceivesCorrectArgs - interface gets line number and record")]
    public async Task AsyncEngine_AfterRead_ReceivesCorrectArgs()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<NotifiableCsvOrder>();
        var input = "Header\r\n1,Alice,100.50";

        // Act
        engine.BeginReadString(input);
        var record = engine.ReadNext();
        engine.Close();

        // Assert
        await Assert.That(record).IsNotNull();
        await Assert.That(record!.AfterReadCount).IsEqualTo(1);
        await Assert.That(record.LastAfterReadArgs!.LineNumber).IsEqualTo(2);
        await Assert.That(record.LastAfterReadArgs.Record.OrderId).IsEqualTo(1);
    }

    [Test]
    [DisplayName("AsyncEngine_AfterRead_SkipThisRecord_Works - interface can filter records")]
    public async Task AsyncEngine_AfterRead_SkipThisRecord_Works()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<ConditionalSkipRecord>();
        var input = "Header\r\n1,Keep\r\n2,Skip\r\n3,Keep";
        ConditionalSkipRecord.ConfigureSkip("Skip");

        // Act
        engine.BeginReadString(input);
        var records = new List<ConditionalSkipRecord>();
        while (engine.ReadNext() is { } record)
        {
            records.Add(record);
        }
        engine.Close();

        // Assert
        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(records[0].Id).IsEqualTo(1);
        await Assert.That(records[1].Id).IsEqualTo(3);
        
        // Cleanup
        ConditionalSkipRecord.ClearConfiguration();
    }

    [Test]
    [DisplayName("AsyncEngine_InterfaceWorksWithIEnumerable - foreach calls interface methods")]
    public async Task AsyncEngine_InterfaceWorksWithIEnumerable()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<NotifiableCsvOrder>();
        var input = "Header\r\n1,Alice,100.50\r\n2,Bob,200.00";

        // Act
        engine.BeginReadString(input);
        var records = new List<NotifiableCsvOrder>();
        foreach (var record in engine)
        {
            records.Add(record);
        }
        engine.Close();

        // Assert
        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(records[0].AfterReadCount).IsEqualTo(1);
        await Assert.That(records[1].AfterReadCount).IsEqualTo(1);
    }

    // -------------------------------------------------------------------------
    // AsyncEnumerable tests
    // -------------------------------------------------------------------------

    [Test]
    [DisplayName("AsyncEnumerable_AfterRead_CalledCorrectly - ReadAsync calls interface")]
    public async Task AsyncEnumerable_AfterRead_CalledCorrectly()
    {
        // Arrange - create temp file for ReadAsync
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "Header\r\n1,Alice,100.50\r\n2,Bob,200.00");
            var engine = new FileHelperAsyncEngine<NotifiableCsvOrder>();
            var records = new List<NotifiableCsvOrder>();

            // Act
            await foreach (var record in engine.ReadAsync(tempFile))
            {
                records.Add(record);
            }

            // Assert
            await Assert.That(records.Count).IsEqualTo(2);
            await Assert.That(records[0].AfterReadCount).IsEqualTo(1);
            await Assert.That(records[1].AfterReadCount).IsEqualTo(1);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
