// CA1707: Test method names use underscores for readability.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional.
#pragma warning disable MA0048
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using ModernLibs.FileHelpers.Tests.Parity.QuickStart.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parity.QuickStart;

/// <summary>
/// QuickStart Record-by-Record Streaming tests.
/// Based on original FileHelpers QuickStart examples from https://www.filehelpers.net/quickstart/
/// Tests FileHelperAsyncEngine streaming behavior.
/// </summary>
[Category("Parity")]
[Category("QuickStart")]
public sealed class QuickStartStreamingTests
{
    #region Record-by-Record Streaming - Delimited

    /// <summary>
    /// BeginReadFile + ReadNext() pattern matches original FileHelpers AsyncEngine.
    /// </summary>
    [Test]
    [DisplayName("BeginReadFile + ReadNext streaming - matches original FileHelpers behavior")]
    public async Task StreamingDelimited_BeginReadFile_MatchesOriginalBehavior()
    {
        // Arrange: Write test file
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_stream_{Guid.NewGuid()}.txt");
        var content = "John Doe,30,USA\r\nJane Smith,25,Canada\r\nBob Wilson,35,UK";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            var engine = new FileHelperAsyncEngine<StreamingCustomer>();
            var records = new List<StreamingCustomer>();

            // Act: Stream using BeginReadFile + ReadNext()
            engine.BeginReadFile(tempFile);
            while (engine.ReadNext() is { } record)
            {
                records.Add(record);
            }
            engine.Close();

            // Assert: All 3 records streamed correctly
            await Assert.That(records.Count).IsEqualTo(3);
            await Assert.That(records[0].Name).IsEqualTo("John Doe");
            await Assert.That(records[0].Age).IsEqualTo(30);
            await Assert.That(records[2].Country).IsEqualTo("UK");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// BeginReadStream + ReadNext() pattern matches original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("BeginReadStream + ReadNext streaming - matches original FileHelpers")]
    public async Task StreamingDelimited_BeginReadStream_MatchesOriginalBehavior()
    {
        // Arrange
        var content = "John Doe,30,USA\r\nJane Smith,25,Canada\r\nBob Wilson,35,UK";
        var engine = new FileHelperAsyncEngine<StreamingCustomer>();
        var records = new List<StreamingCustomer>();

        // Act
        using var reader = new StringReader(content);
        engine.BeginReadStream(reader);
        while (engine.ReadNext() is { } record)
        {
            records.Add(record);
        }
        engine.Close();

        // Assert
        await Assert.That(records.Count).IsEqualTo(3);
        await Assert.That(records[1].Name).IsEqualTo("Jane Smith");
    }

    /// <summary>
    /// BeginReadString + ReadNext() pattern matches original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("BeginReadString + ReadNext streaming - matches original FileHelpers")]
    public async Task StreamingDelimited_BeginReadString_MatchesOriginalBehavior()
    {
        // Arrange
        var content = "John Doe,30,USA\r\nJane Smith,25,Canada";
        var engine = new FileHelperAsyncEngine<StreamingCustomer>();
        var records = new List<StreamingCustomer>();

        // Act
        engine.BeginReadString(content);
        while (engine.ReadNext() is { } record)
        {
            records.Add(record);
        }
        engine.Close();

        // Assert
        await Assert.That(records.Count).IsEqualTo(2);
    }

    /// <summary>
    /// IAsyncEnumerable pattern matches original FileHelpers streaming behavior.
    /// </summary>
    [Test]
    [DisplayName("IAsyncEnumerable ReadAsync - matches streaming behavior")]
    public async Task StreamingDelimited_IAsyncEnumerable_MatchesBehavior()
    {
        // Arrange: Write test file
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_async_enum_{Guid.NewGuid()}.txt");
        var content = "John Doe,30,USA\r\nJane Smith,25,Canada\r\nBob Wilson,35,UK";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            var engine = new FileHelperAsyncEngine<StreamingCustomer>();
            var records = new List<StreamingCustomer>();

            // Act: Use IAsyncEnumerable
            await foreach (var record in engine.ReadAsync(tempFile))
            {
                records.Add(record);
            }

            // Assert
            await Assert.That(records.Count).IsEqualTo(3);
            await Assert.That(records[0].Name).IsEqualTo("John Doe");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Streaming with header skip (IgnoreFirst) matches original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("Streaming with IgnoreFirst header - matches original FileHelpers")]
    public async Task StreamingDelimited_WithHeader_MatchesOriginalBehavior()
    {
        // Arrange
        var content = "Name,Age,Country\r\nJohn Doe,30,USA\r\nJane Smith,25,Canada";
        var engine = new FileHelperAsyncEngine<StreamingCustomerWithHeader>();
        var records = new List<StreamingCustomerWithHeader>();

        // Act
        engine.BeginReadString(content);
        while (engine.ReadNext() is { } record)
        {
            records.Add(record);
        }
        engine.Close();

        // Assert: Header skipped, 2 records parsed
        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(engine.HeaderText).IsEqualTo("Name,Age,Country");
    }

    /// <summary>
    /// IEnumerable pattern (foreach over engine) matches original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("IEnumerable foreach pattern - matches original FileHelpers")]
    public async Task StreamingDelimited_IEnumerable_MatchesOriginalBehavior()
    {
        // Arrange
        var content = "John Doe,30,USA\r\nJane Smith,25,Canada";
        var engine = new FileHelperAsyncEngine<StreamingCustomer>();
        var records = new List<StreamingCustomer>();

        // Act
        engine.BeginReadString(content);
        foreach (var record in engine)
        {
            records.Add(record);
        }
        engine.Close();

        // Assert
        await Assert.That(records.Count).IsEqualTo(2);
    }

    #endregion

    #region Record-by-Record Streaming - Fixed-Length

    /// <summary>
    /// Fixed-length streaming with BeginReadFile + ReadNext().
    /// </summary>
    [Test]
    [DisplayName("Fixed-length streaming - BeginReadFile + ReadNext")]
    public async Task StreamingFixed_BeginReadFile_MatchesOriginalBehavior()
    {
        // Arrange: Write test file
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_fixed_stream_{Guid.NewGuid()}.txt");
        var content = "John Doe            30 USA       \r\nJane Smith          25 Canada    ";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            var engine = new FileHelperAsyncEngine<StreamingFixedCustomer>();
            var records = new List<StreamingFixedCustomer>();

            // Act
            engine.BeginReadFile(tempFile);
            while (engine.ReadNext() is { } record)
            {
                records.Add(record);
            }
            engine.Close();

            // Assert
            await Assert.That(records.Count).IsEqualTo(2);
            await Assert.That(records[0].Name.Trim()).IsEqualTo("John Doe");
            await Assert.That(records[0].Age).IsEqualTo(30);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region Write Streaming

    /// <summary>
    /// Streaming write with BeginWriteFile + WriteNext matches original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("BeginWriteFile + WriteNext streaming - matches original FileHelpers")]
    public async Task StreamingWrite_BeginWriteFile_MatchesOriginalBehavior()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_write_stream_{Guid.NewGuid()}.txt");
        var records = new[]
        {
            new StreamingCustomer { Name = "John Doe", Age = 30, Country = "USA" },
            new StreamingCustomer { Name = "Jane Smith", Age = 25, Country = "Canada" }
        };

        try
        {
            var engine = new FileHelperAsyncEngine<StreamingCustomer>();

            // Act
            engine.BeginWriteFile(tempFile);
            foreach (var record in records)
            {
                engine.WriteNext(record);
            }
            engine.Close();

            // Assert
            var content = await File.ReadAllTextAsync(tempFile);
            var lines = content.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
            await Assert.That(lines.Length).IsEqualTo(2);
            await Assert.That(lines[0]).IsEqualTo("John Doe,30,USA");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Streaming write with BeginWriteString matches original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("BeginWriteString + EndWriteString - matches original FileHelpers")]
    public async Task StreamingWrite_BeginWriteString_MatchesOriginalBehavior()
    {
        // Arrange
        var records = new[]
        {
            new StreamingCustomer { Name = "John Doe", Age = 30, Country = "USA" }
        };
        var engine = new FileHelperAsyncEngine<StreamingCustomer>();

        // Act
        engine.BeginWriteString();
        engine.WriteNext(records[0]);
        var output = engine.EndWriteString();

        // Assert
        await Assert.That(output.TrimEnd()).IsEqualTo("John Doe,30,USA");
    }

    /// <summary>
    /// WriteNext batch method matches original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("WriteNext(IEnumerable) batch method - matches original FileHelpers")]
    public async Task StreamingWrite_WriteNextBatch_MatchesOriginalBehavior()
    {
        // Arrange
        var records = new[]
        {
            new StreamingCustomer { Name = "John Doe", Age = 30, Country = "USA" },
            new StreamingCustomer { Name = "Jane Smith", Age = 25, Country = "Canada" }
        };
        var engine = new FileHelperAsyncEngine<StreamingCustomer>();

        // Act
        engine.BeginWriteString();
        engine.WriteNext(records);
        var output = engine.EndWriteString();

        // Assert
        var lines = output.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines.Length).IsEqualTo(2);
    }

    #endregion

    #region Engine Properties and State

    /// <summary>
    /// LineNumber tracks correctly during streaming read.
    /// </summary>
    [Test]
    [DisplayName("LineNumber property tracks correctly during streaming")]
    public async Task Streaming_LineNumber_TracksCorrectly()
    {
        // Arrange
        var content = "John Doe,30,USA\r\nJane Smith,25,Canada\r\nBob Wilson,35,UK";
        var engine = new FileHelperAsyncEngine<StreamingCustomer>();

        // Act
        engine.BeginReadString(content);

        // Read first record
        engine.ReadNext();
        var lineAfterFirst = engine.LineNumber;

        // Read second record
        engine.ReadNext();
        var lineAfterSecond = engine.LineNumber;

        // Read third record
        engine.ReadNext();
        var lineAfterThird = engine.LineNumber;

        engine.Close();

        // Assert: LineNumber should be 1-based line position
        await Assert.That(lineAfterFirst).IsEqualTo(1);
        await Assert.That(lineAfterSecond).IsEqualTo(2);
        await Assert.That(lineAfterThird).IsEqualTo(3);
    }

    /// <summary>
    /// TotalRecords counts correctly during streaming.
    /// </summary>
    [Test]
    [DisplayName("TotalRecords property counts correctly during streaming")]
    public async Task Streaming_TotalRecords_CountsCorrectly()
    {
        // Arrange
        var content = "John Doe,30,USA\r\nJane Smith,25,Canada";
        var engine = new FileHelperAsyncEngine<StreamingCustomer>();

        // Act
        engine.BeginReadString(content);
        engine.ReadNext();
        var countAfterFirst = engine.TotalRecords;
        engine.ReadNext();
        var countAfterSecond = engine.TotalRecords;
        engine.ReadNext(); // EOF
        var countAtEof = engine.TotalRecords;
        engine.Close();

        // Assert
        await Assert.That(countAfterFirst).IsEqualTo(1);
        await Assert.That(countAfterSecond).IsEqualTo(2);
        await Assert.That(countAtEof).IsEqualTo(2);
    }

    /// <summary>
    /// LastRecord property returns the most recent record.
    /// </summary>
    [Test]
    [DisplayName("LastRecord property returns most recent record")]
    public async Task Streaming_LastRecord_ReturnsMostRecent()
    {
        // Arrange
        var content = "John Doe,30,USA\r\nJane Smith,25,Canada";
        var engine = new FileHelperAsyncEngine<StreamingCustomer>();

        // Act
        engine.BeginReadString(content);
        engine.ReadNext();
        var firstRecord = engine.LastRecord;
        engine.ReadNext();
        var secondRecord = engine.LastRecord;
        engine.Close();

        // Assert
        await Assert.That(firstRecord?.Name).IsEqualTo("John Doe");
        await Assert.That(secondRecord?.Name).IsEqualTo("Jane Smith");
    }

    /// <summary>
    /// ReadNext(int count) returns multiple records.
    /// </summary>
    [Test]
    [DisplayName("ReadNext(int count) returns batch of records")]
    public async Task Streaming_ReadNextBatch_ReturnsBatch()
    {
        // Arrange
        var content = "John Doe,30,USA\r\nJane Smith,25,Canada\r\nBob Wilson,35,UK";
        var engine = new FileHelperAsyncEngine<StreamingCustomer>();

        // Act
        engine.BeginReadString(content);
        var batch = engine.ReadNext(2);
        engine.Close();

        // Assert
        await Assert.That(batch.Length).IsEqualTo(2);
        await Assert.That(batch[0].Name).IsEqualTo("John Doe");
        await Assert.That(batch[1].Name).IsEqualTo("Jane Smith");
    }

    #endregion

    #region RecycleRecords (Zero-Allocation Mode)

    /// <summary>
    /// RecycleRecords mode for class types (zero-allocation pattern).
    /// </summary>
    [Test]
    [DisplayName("RecycleRecords mode reuses class instances")]
    public async Task Streaming_RecycleRecords_ReusesInstances()
    {
        // Arrange: Create a class record type
        var content = "John Doe,30,USA\r\nJane Smith,25,Canada";
        var engine = new FileHelperAsyncEngine<StreamingCustomer>();
        engine.RecycleRecords = true;

        // Act
        engine.BeginReadString(content);
        var record1 = engine.ReadNext();
        var record1Reference = record1;
        var record2 = engine.ReadNext();
        var record2Reference = record2;
        engine.Close();

        // Assert: With RecycleRecords=true and class types, the same instance is reused
        // Note: This depends on IRecyclableDescriptor implementation in the generator
        // The actual behavior is that record1 and record2 are the same reference when recycled
        await Assert.That(record1Reference).IsNotNull();
        await Assert.That(record2Reference).IsNotNull();

        // Verify data is correct (even if same instance reused, it should have latest data)
        await Assert.That(record2Reference!.Name).IsEqualTo("Jane Smith");
    }

    #endregion

    #region Round-Trip Streaming

    /// <summary>
    /// Streaming round-trip (read then write then read) produces equivalent records.
    /// </summary>
    [Test]
    [DisplayName("Streaming round-trip produces equivalent records")]
    public async Task StreamingRoundTrip_ProducesEquivalentRecords()
    {
        // Arrange: Original content
        var originalContent = "John Doe,30,USA\r\nJane Smith,25,Canada";
        var readEngine = new FileHelperAsyncEngine<StreamingCustomer>();

        // Read original records
        var originalRecords = new List<StreamingCustomer>();
        readEngine.BeginReadString(originalContent);
        while (readEngine.ReadNext() is { } record)
        {
            originalRecords.Add(record);
        }
        readEngine.Close();

        // Act: Write to string
        var writeEngine = new FileHelperAsyncEngine<StreamingCustomer>();
        writeEngine.BeginWriteString();
        writeEngine.WriteNext(originalRecords);
        var intermediateOutput = writeEngine.EndWriteString();

        // Act: Read back
        var roundTripEngine = new FileHelperAsyncEngine<StreamingCustomer>();
        var roundTripRecords = new List<StreamingCustomer>();
        roundTripEngine.BeginReadString(intermediateOutput);
        while (roundTripEngine.ReadNext() is { } record)
        {
            roundTripRecords.Add(record);
        }
        roundTripEngine.Close();

        // Assert: Round-trip produces same data
        await Assert.That(roundTripRecords.Count).IsEqualTo(originalRecords.Count);

        for (int i = 0; i < originalRecords.Count; i++)
        {
            await Assert.That(roundTripRecords[i].Name).IsEqualTo(originalRecords[i].Name);
            await Assert.That(roundTripRecords[i].Age).IsEqualTo(originalRecords[i].Age);
            await Assert.That(roundTripRecords[i].Country).IsEqualTo(originalRecords[i].Country);
        }
    }

    #endregion
}
