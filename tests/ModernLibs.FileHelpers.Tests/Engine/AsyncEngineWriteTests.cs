// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// Acceptance tests for <see cref="FileHelperAsyncEngine{T}"/> write operations.
/// Covers ENGI-12 (BeginWrite*) and ENGI-13 (WriteNext).
/// </summary>
[Category("Phase08")]
[Category("AsyncEngine")]
public sealed class AsyncEngineWriteTests
{
    // -------------------------------------------------------------------------
    // ENGI-12 — BeginWrite* opens writer
    // -------------------------------------------------------------------------

    /// <summary>ENGI-12: BeginWriteFile on temp path; WriteNext one record; Close; file exists with content.</summary>
    [Test]
    [DisplayName("BeginWriteFile creates file, WriteNext writes record, Close flushes content to disk")]
    public async Task BeginWriteFile_CreatesFile_WriteNextFlushes()
    {
        // Use Path.GetTempFileName() for safe temp file path resolution (threat model V5.1).
        // Arrange: Get temp file name and create async engine
        string tempFile = Path.GetTempFileName();
        try
        {
            using var engine = new FileHelperAsyncEngine<CsvOrder>();
            engine.BeginWriteFile(tempFile);
            engine.WriteNext(new CsvOrder { OrderId = 1, CustomerName = "Alice", Amount = 100.50m });
            engine.Close();

            // Act: Read file content after close
            string content = await File.ReadAllTextAsync(tempFile);

            // Assert: File contains record data
            await Assert.That(content).IsNotNull();
            await Assert.That(content.Length).IsGreaterThan(0);
            await Assert.That(content).Contains("Alice");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>ENGI-12: BeginWriteStream(StringWriter); WriteNext; Close; StringWriter has content.</summary>
    [Test]
    [DisplayName("BeginWriteStream writes to StringWriter and content is retrievable after Close")]
    public async Task BeginWriteStream_WritesToTextWriter()
    {
        // Arrange: Create StringWriter and async engine, open write stream
        using var sw = new StringWriter();
        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginWriteStream(sw);
        engine.WriteNext(new CsvOrder { OrderId = 5, CustomerName = "Eve", Amount = 50.00m });
        engine.Close();

        // Act: Get StringWriter content after close
        string result = sw.ToString();

        // Assert: StringWriter contains the written record data
        await Assert.That(result).Contains("Eve");
    }

    /// <summary>ENGI-12: BeginWriteString; WriteNext 2 records; EndWriteString; accumulated content has 2 lines.</summary>
    [Test]
    [DisplayName("BeginWriteString accumulates 2 records and EndWriteString returns combined content")]
    public async Task BeginWriteString_AccumulatesContent()
    {
        // Arrange: Create async engine, open string writer, write 2 records
        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginWriteString();
        engine.WriteNext(new CsvOrder { OrderId = 1, CustomerName = "Alice", Amount = 10.00m });
        engine.WriteNext(new CsvOrder { OrderId = 2, CustomerName = "Bob", Amount = 20.00m });
        string result = engine.EndWriteString();

        // Two records → two non-empty lines
        // Act: Split result into lines
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert: Exactly 2 lines for 2 records
        await Assert.That(lines.Length).IsEqualTo(2);
    }

    // -------------------------------------------------------------------------
    // ENGI-13 — WriteNext
    // -------------------------------------------------------------------------

    /// <summary>ENGI-13: BeginWriteString; WriteNext(csvOrder); EndWriteString; output contains "1,Alice,100.50".</summary>
    [Test]
    [DisplayName("WriteNext formats single CsvOrder record correctly as CSV line")]
    public async Task WriteNext_SingleRecord_FormatsCorrectly()
    {
        // Arrange: Create async engine, open string writer, write single record
        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginWriteString();
        engine.WriteNext(new CsvOrder { OrderId = 1, CustomerName = "Alice", Amount = 100.50m });

        // Act: End write and get result
        string result = engine.EndWriteString();

        // CsvOrder is [DelimitedRecord(",")], so output is comma-separated
        // Assert: Output contains OrderId, CustomerName, and Amount values
        await Assert.That(result).Contains("1");
        await Assert.That(result).Contains("Alice");
        await Assert.That(result).Contains("100.50");
    }

    /// <summary>ENGI-13: BeginWriteString; WriteNext(IEnumerable of 3); EndWriteString; output has 3 lines.</summary>
    [Test]
    [DisplayName("WriteNext with IEnumerable writes all 3 records from array")]
    public async Task WriteNext_Enumerable_WritesAllRecords()
    {
        // Arrange: Create array of 3 CsvOrder records
        var records = new[]
        {
            new CsvOrder { OrderId = 1, CustomerName = "A", Amount = 1.00m },
            new CsvOrder { OrderId = 2, CustomerName = "B", Amount = 2.00m },
            new CsvOrder { OrderId = 3, CustomerName = "C", Amount = 3.00m },
        };

        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginWriteString();
        engine.WriteNext((IEnumerable<CsvOrder>)records);
        string result = engine.EndWriteString();

        // Act: Split result into lines
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert: Exactly 3 lines for 3 records
        await Assert.That(lines.Length).IsEqualTo(3);
    }

    /// <summary>ENGI-13: BeginWriteString; WriteNext 3 times; TotalRecords == 3.</summary>
    [Test]
    [DisplayName("WriteNext increments TotalRecords to 3 after writing three records")]
    public async Task WriteNext_TotalRecords_TracksCount()
    {
        // Arrange: Create async engine and open string writer
        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginWriteString();
        engine.WriteNext(new CsvOrder { OrderId = 1, CustomerName = "A", Amount = 1.00m });
        engine.WriteNext(new CsvOrder { OrderId = 2, CustomerName = "B", Amount = 2.00m });

        // Act: Write third record
        engine.WriteNext(new CsvOrder { OrderId = 3, CustomerName = "C", Amount = 3.00m });

        // Assert: TotalRecords equals 3 after writing three records
        await Assert.That(engine.TotalRecords).IsEqualTo(3);

        engine.EndWriteString();
    }
}
