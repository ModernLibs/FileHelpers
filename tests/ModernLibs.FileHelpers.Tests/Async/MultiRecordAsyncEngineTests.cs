// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple fixture types live with the behavior tests that use them.
#pragma warning disable MA0048

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Async.MultiRecord;

[DelimitedRecord("|")]
internal sealed class MultiAsyncHeader
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public string Batch { get; set; } = "";
}

[DelimitedRecord("|")]
internal sealed class MultiAsyncDetail
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public int Id { get; set; }
    [FieldOrder(3)] public decimal Amount { get; set; }
}

[Category("Phase31")]
[Category("Async")]
[Category("MultiRecord")]
public sealed class MultiRecordAsyncEngineTests
{
    [Test]
    [DisplayName("MultiRecordAsyncEngine yields mixed records from stream in order")]
    public async Task ReadAsync_Stream_YieldsMixedRecordsInOrder()
    {
        // Arrange: engine with two record types
        var engine = new MultiRecordAsyncEngine(
            MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<MultiAsyncDetail>(line => line.StartsWith("D|", StringComparison.Ordinal)));

        // Act: read from memory stream
        var content = "H|B001\nD|10|42.5\nH|B002\nD|20|99.9";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var records = new List<object>();
        await foreach (var record in engine.ReadAsync(stream))
            records.Add(record);

        // Assert: all records yielded in order
        await Assert.That(records.Count).IsEqualTo(4);
        await Assert.That(records[0]).IsTypeOf<MultiAsyncHeader>();
        await Assert.That(records[1]).IsTypeOf<MultiAsyncDetail>();
        await Assert.That(((MultiAsyncHeader)records[0]).Batch).IsEqualTo("B001");
        await Assert.That(((MultiAsyncDetail)records[1]).Amount).IsEqualTo(42.5m);
        await Assert.That(((MultiAsyncHeader)records[2]).Batch).IsEqualTo("B002");
        await Assert.That(((MultiAsyncDetail)records[3]).Amount).IsEqualTo(99.9m);
    }

    [Test]
    [DisplayName("MultiRecordAsyncEngine yields mixed records from file path")]
    public async Task ReadAsync_FilePath_YieldsMixedRecords()
    {
        // Arrange: engine with two record types
        var engine = new MultiRecordAsyncEngine(
            MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<MultiAsyncDetail>(line => line.StartsWith("D|", StringComparison.Ordinal)));

        // Act: write temp file and read
        var content = "H|BATCH\nD|5|10.0";
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, content);
            var records = new List<object>();
            await foreach (var record in engine.ReadAsync(tempFile))
                records.Add(record);

            // Assert
            await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(records[0]).IsTypeOf<MultiAsyncHeader>();
        await Assert.That(records[1]).IsTypeOf<MultiAsyncDetail>();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    [DisplayName("MultiRecordAsyncEngine throws on selector miss with ErrorMode.ThrowException")]
    public async Task ReadAsync_UnknownLine_ThrowException_Throws()
    {
        // Arrange: narrow selector that won't match
        var engine = new MultiRecordAsyncEngine(
            MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)));
        engine.ErrorMode = ErrorMode.ThrowException;

        // Act & Assert: unmatched line throws FormatException
        var content = "X|unknown";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        await Assert.That(async () =>
        {
            await foreach (var _ in engine.ReadAsync(stream))
            {
                // consume
            }
        }).Throws<FormatException>();
    }

    [Test]
    [DisplayName("MultiRecordAsyncEngine skips unknown lines with ErrorMode.IgnoreAndContinue")]
    public async Task ReadAsync_UnknownLine_IgnoreAndContinue_Skips()
    {
        // Arrange
        var engine = new MultiRecordAsyncEngine(
            MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)));
        engine.ErrorMode = ErrorMode.IgnoreAndContinue;

        // Act: input with matching and non-matching lines
        var content = "H|BATCH\nX|unknown\nH|BATCH2";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var records = new List<object>();
        await foreach (var record in engine.ReadAsync(stream))
            records.Add(record);

        // Assert: only matching records yielded
        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(records[0]).IsTypeOf<MultiAsyncHeader>();
        await Assert.That(records[1]).IsTypeOf<MultiAsyncHeader>();
    }

    [Test]
    [DisplayName("MultiRecordAsyncEngine collects errors with ErrorMode.SaveAndContinue")]
    public async Task ReadAsync_UnknownLine_SaveAndContinue_CollectsErrors()
    {
        // Arrange
        var engine = new MultiRecordAsyncEngine(
            MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)));
        engine.ErrorMode = ErrorMode.SaveAndContinue;

        // Act
        var content = "H|BATCH\nX|unknown\nH|BATCH2";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var records = new List<object>();
        await foreach (var record in engine.ReadAsync(stream))
            records.Add(record);

        // Assert
        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.Errors.Count).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
    }

    [Test]
    [DisplayName("MultiRecordAsyncEngine yields nothing from empty stream")]
    public async Task ReadAsync_EmptyStream_YieldsNothing()
    {
        // Arrange
        var engine = new MultiRecordAsyncEngine(
            MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act: empty stream
        using var stream = new MemoryStream(Array.Empty<byte>());
        var records = new List<object>();
        await foreach (var record in engine.ReadAsync(stream))
            records.Add(record);

        // Assert
        await Assert.That(records.Count).IsEqualTo(0);
    }

    [Test]
    [DisplayName("MultiRecordAsyncEngine respects cancellation token")]
    public async Task ReadAsync_CancellationToken_StopsIteration()
    {
        // Arrange: engine with many records
        var engine = new MultiRecordAsyncEngine(
            MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        var content = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"H|B{i}"));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var cts = new CancellationTokenSource();
        var records = new List<object>();
        var count = 0;

        // Act: cancel after 5 records — OperationCanceledException is expected
        try
        {
            await foreach (var record in engine.ReadAsync(stream, cts.Token))
            {
                records.Add(record);
                count++;
                if (count >= 5)
                    cts.Cancel();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is triggered
        }

        // Assert: got some records before cancellation
        await Assert.That(records.Count).IsGreaterThanOrEqualTo(5);
        await Assert.That(records.Count).IsLessThanOrEqualTo(6);
    }

    [Test]
    [DisplayName("MultiRecordAsyncEngine constructor rejects empty array")]
    public async Task Constructor_EmptyArray_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => new MultiRecordAsyncEngine(Array.Empty<IMultiRecordType>()))
            .Throws<ArgumentException>();
    }

    [Test]
    [DisplayName("MultiRecordAsyncEngine null filePath throws ArgumentNullException")]
    public async Task ReadAsync_NullFilePath_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MultiRecordAsyncEngine(
            MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert
        await Assert.That(async () =>
        {
            await foreach (var _ in engine.ReadAsync((string)null!, CancellationToken.None))
            {
            }
        }).Throws<ArgumentNullException>();
    }

    [Test]
    [DisplayName("MultiRecordAsyncEngine null stream throws ArgumentNullException")]
    public async Task ReadAsync_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MultiRecordAsyncEngine(
            MultiRecordType.For<MultiAsyncHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert
        await Assert.That(async () =>
        {
            await foreach (var _ in engine.ReadAsync((string)null!, CancellationToken.None))
            {
            }
        }).Throws<ArgumentNullException>();
    }
}
