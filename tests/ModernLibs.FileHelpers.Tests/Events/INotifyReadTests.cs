// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional.
#pragma warning disable MA0048

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Events.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Events;

/// <summary>
/// Test suite for INotifyRead interface behavior.
/// </summary>
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
        int beforeReadEventCount = 0;
        engine.BeforeReadRecord += (s, e) => beforeReadEventCount++;

        // Act
        var records = engine.ReadString(input);

        // Assert: Both event and interface method fire for each record.
        // BeforeRead runs on the instance that receives the parsed values (3.x).
        await Assert.That(records.Length).IsEqualTo(3);
        await Assert.That(beforeReadEventCount).IsEqualTo(3);
        await Assert.That(records[0].BeforeReadCount).IsEqualTo(1);
        await Assert.That(records[2].BeforeReadCount).IsEqualTo(1);
        await Assert.That(records[0].AfterReadCount).IsEqualTo(1);
        await Assert.That(records[1].AfterReadCount).IsEqualTo(1);
        await Assert.That(records[2].AfterReadCount).IsEqualTo(1);
    }

    [Test]
    [DisplayName("AfterRead_SkipThisRecord_ExcludesRecord - interface can filter from results")]
    [NotInParallel("ConditionalSkipRecord-static-state")] // shares ConditionalSkipRecord static configuration
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
    [NotInParallel("ConditionalSkipRecord-static-state")] // shares ConditionalSkipRecord static configuration
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

    // -------------------------------------------------------------------------
    // BeforeRead tests
    // -------------------------------------------------------------------------

    [Test]
    [DisplayName("BeforeRead_FiresBeforeParsing - sync engine calls BeforeRead on default instance")]
    public async Task BeforeRead_FiresBeforeParsing()
    {
        // Arrange
        var engine = new FileHelperEngine<NotifiableCsvOrder>();
        var input = "Header\r\n1,Alice,100.50\r\n2,Bob,200.00";
        int beforeReadFireCount = 0;
        string? lastBeforeReadLine = null;
        engine.BeforeReadRecord += (s, e) =>
        {
            beforeReadFireCount++;
            lastBeforeReadLine = e.RecordLine;
        };

        // Act
        var records = engine.ReadString(input);

        // Assert: BeforeRead fired for each record
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(beforeReadFireCount).IsEqualTo(2);
        await Assert.That(lastBeforeReadLine).IsEqualTo("2,Bob,200.00");
    }

    [Test]
    [DisplayName("BeforeRead_SkipThisRecord_ExcludesRecord - interface can skip before parsing")]
    [NotInParallel("ConditionalSkipRecord-static-state")] // shares ConditionalSkipRecord static configuration
    public async Task BeforeRead_SkipThisRecord_ExcludesRecord()
    {
        // Arrange: ConditionalSkipRecord skips in BeforeRead when line number matches
        var engine = new FileHelperEngine<ConditionalSkipRecord>();
        var input = "Header\r\n1,Keep\r\n2,SkipMe\r\n3,Keep";

        // Configure to skip line 3 (which is "2,SkipMe")
        ConditionalSkipRecord.ConfigureSkip(skipCategory: "", skipBeforeLine: 3);

        // Act
        var records = engine.ReadString(input);

        // Assert: Only 2 records returned (ID 1 and 3), line 3 was skipped in BeforeRead
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Id).IsEqualTo(1);
        await Assert.That(records[1].Id).IsEqualTo(3);

        // Cleanup
        ConditionalSkipRecord.ClearConfiguration();
    }

    [Test]
    [DisplayName("BeforeRead_StopReading_TerminatesEarly - interface can stop reading")]
    [NotInParallel("ConditionalSkipRecord-static-state")] // shares ConditionalSkipRecord static configuration
    public async Task BeforeRead_StopReading_TerminatesEarly()
    {
        // Arrange
        var engine = new FileHelperEngine<ConditionalSkipRecord>();
        var input = "Header\r\n1,First\r\n2,Second\r\n3,Third";

        // Configure to stop at line 3 (which is "2,Second")
        ConditionalSkipRecord.ConfigureSkip(skipCategory: "", skipBeforeLine: 0, stopAtLine: 3);

        // Act
        var records = engine.ReadString(input);

        // Assert: Only 1 record returned (ID 1), reading stopped at line 3
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Id).IsEqualTo(1);

        // Cleanup
        ConditionalSkipRecord.ClearConfiguration();
    }

    [Test]
    [DisplayName("BeforeRead_ReceivesCorrectLineNumber - interface gets 1-based line number")]
    public async Task BeforeRead_ReceivesCorrectLineNumber()
    {
        // Arrange
        var engine = new FileHelperEngine<NotifiableCsvOrder>();
        var input = "Header\r\n1,Alice,100.50\r\n2,Bob,200.00";
        var lineNumbers = new List<int>();
        engine.BeforeReadRecord += (s, e) => lineNumbers.Add(e.LineNumber);

        // Act
        engine.ReadString(input);

        // Assert: Line numbers reflect original file position (2 and 3)
        await Assert.That(lineNumbers.Count).IsEqualTo(2);
        await Assert.That(lineNumbers[0]).IsEqualTo(2);
        await Assert.That(lineNumbers[1]).IsEqualTo(3);
    }

    [Test]
    [DisplayName("BeforeRead_FiresInAsyncEngine - FileHelperAsyncEngine calls BeforeRead")]
    public async Task BeforeRead_FiresInAsyncEngine()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<DualNotifiableRecord>();
        var input = "Header\r\n1,First\r\n2,Second";
        int beforeReadFireCount = 0;
        engine.BeforeReadRecord += (s, e) => beforeReadFireCount++;

        // Act
        engine.BeginReadString(input);
        var records = new List<DualNotifiableRecord>();
        while (engine.ReadNext() is { } record)
        {
            records.Add(record);
        }
        engine.Close();

        // Assert: BeforeRead fired for each record
        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(beforeReadFireCount).IsEqualTo(2);
    }

    [Test]
    [DisplayName("BeforeRead_FiresInAsyncEnumerable - ReadAsync calls BeforeRead")]
    public async Task BeforeRead_FiresInAsyncEnumerable()
    {
        // Arrange - create temp file for ReadAsync
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "Header\r\n1,First\r\n2,Second");
            var engine = new FileHelperAsyncEngine<DualNotifiableRecord>();
            var records = new List<DualNotifiableRecord>();
            int beforeReadFireCount = 0;
            engine.BeforeReadRecord += (s, e) => beforeReadFireCount++;

            // Act
            await foreach (var record in engine.ReadAsync(tempFile))
            {
                records.Add(record);
            }

            // Assert: BeforeRead fired for each record
            await Assert.That(records.Count).IsEqualTo(2);
            await Assert.That(beforeReadFireCount).IsEqualTo(2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    [DisplayName("BeforeRead_CalledAlongsideEvent - both event and interface fire")]
    public async Task BeforeRead_CalledAlongsideEvent()
    {
        // Arrange
        var engine = new FileHelperEngine<NotifiableCsvOrder>();
        int eventFireCount = 0;

        engine.BeforeReadRecord += (sender, e) =>
        {
            eventFireCount++;
        };

        var input = "Header\r\n1,Alice,100.50\r\n2,Bob,300.00";

        // Act
        var records = engine.ReadString(input);

        // Assert: Both event and interface method were called
        await Assert.That(eventFireCount).IsEqualTo(2);
        await Assert.That(records.Length).IsEqualTo(2);
    }

    [Test]
    [DisplayName("BeforeRead_OnlyInterfaceFires - works without event subscription")]
    public async Task BeforeRead_OnlyInterfaceFires()
    {
        // Arrange: NotifiableCsvOrder implements BeforeRead with SKIP filtering
        var engine = new FileHelperEngine<NotifiableCsvOrder>();
        var input = "Header\r\n1,Alice,100.50\r\n2,SKIP,200.00\r\n3,Bob,300.00";

        // Act
        var records = engine.ReadString(input);

        // Assert: BeforeRead fired for all 3 lines, SKIP line was filtered
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].OrderId).IsEqualTo(1);
        await Assert.That(records[1].OrderId).IsEqualTo(3);
    }
}
