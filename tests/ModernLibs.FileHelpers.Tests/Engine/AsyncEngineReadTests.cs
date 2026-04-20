// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// Acceptance tests for <see cref="FileHelperAsyncEngine{T}"/> read operations.
/// Covers ENGI-09 (BeginRead*), ENGI-10 (ReadNext), ENGI-11 (ReadAsync IAsyncEnumerable — skipped),
/// and ENGI-14 (IEnumerable&lt;T&gt; / Dispose).
/// </summary>
[Category("Phase08")]
[Category("AsyncEngine")]
public sealed class AsyncEngineReadTests
{
    // -------------------------------------------------------------------------
    // ENGI-09 — BeginRead* opens reader
    // -------------------------------------------------------------------------

    /// <summary>ENGI-09: BeginReadFile on a temp CSV file; ReadNext returns first record.</summary>
    [Test]
    public async Task BeginReadFile_DelimitedRecord_OpensReader()
    {
        // Arrange — write a temp CSV file with 1 header + 1 data row
        string tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "OrderId,CustomerName,Amount\r\n1,Alice,100.50\r\n");

            // Act
            using var engine = new FileHelperAsyncEngine<CsvOrder>();
            engine.BeginReadFile(tempFile);
            var record = engine.ReadNext();

            // Assert
            await Assert.That(record).IsNotNull();
            await Assert.That(record!.OrderId).IsEqualTo(1);
            await Assert.That(record.CustomerName).IsEqualTo("Alice");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>ENGI-09: BeginReadStream with a StringReader; ReadNext returns a record.</summary>
    [Test]
    public async Task BeginReadStream_FromTextReader_OpensReader()
    {
        // Arrange
        const string content = "OrderId,CustomerName,Amount\r\n42,Bob,200.00\r\n";
        using var reader = new StringReader(content);

        // Act
        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadStream(reader);
        var record = engine.ReadNext();

        // Assert
        await Assert.That(record).IsNotNull();
        await Assert.That(record!.OrderId).IsEqualTo(42);
        await Assert.That(record.CustomerName).IsEqualTo("Bob");
    }

    /// <summary>ENGI-09: BeginReadString with CSV content; ReadNext returns first CsvOrder with correct OrderId.</summary>
    [Test]
    public async Task BeginReadString_ParsesFirstRecord()
    {
        // CsvOrder has [IgnoreFirst(1)] so first line is header
        const string content = "header\r\n7,Charlie,50.00\r\n";

        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadString(content);
        var record = engine.ReadNext();

        await Assert.That(record).IsNotNull();
        await Assert.That(record!.OrderId).IsEqualTo(7);
    }

    // -------------------------------------------------------------------------
    // ENGI-10 — ReadNext() / ReadNext(int)
    // -------------------------------------------------------------------------

    /// <summary>ENGI-10: BeginReadString on single-record content; second ReadNext() call returns null.</summary>
    [Test]
    public async Task ReadNext_ReturnsNullAtEof()
    {
        const string content = "header\r\n1,Alice,10.00\r\n";

        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadString(content);

        var first = engine.ReadNext();
        var second = engine.ReadNext();

        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNull();
    }

    /// <summary>ENGI-10: BeginReadString on 5-record content; ReadNext(3) returns array of length 3.</summary>
    [Test]
    public async Task ReadNext_WithCount_ReturnsBatch()
    {
        const string content = "header\r\n1,A,1.00\r\n2,B,2.00\r\n3,C,3.00\r\n4,D,4.00\r\n5,E,5.00\r\n";

        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadString(content);
        var batch = engine.ReadNext(3);

        await Assert.That(batch.Length).IsEqualTo(3);
        await Assert.That(batch[0].OrderId).IsEqualTo(1);
        await Assert.That(batch[2].OrderId).IsEqualTo(3);
    }

    /// <summary>ENGI-10: BeginReadString with 1 header line via CsvOrder [IgnoreFirst(1)]; ReadNext returns first data record.</summary>
    [Test]
    public async Task ReadNext_IgnoreFirst_SkipsHeaderLines()
    {
        const string content = "OrderId,CustomerName,Amount\r\n99,Dave,999.00\r\n";

        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadString(content);
        var record = engine.ReadNext();

        await Assert.That(record).IsNotNull();
        await Assert.That(record!.OrderId).IsEqualTo(99);
        // HeaderText should capture the skipped line
        await Assert.That(engine.HeaderText).IsNotNull();
    }

    // -------------------------------------------------------------------------
    // ENGI-11 — ReadAsync IAsyncEnumerable (deferred to Plan 04)
    // -------------------------------------------------------------------------

    /// <summary>ENGI-11: ReadAsync(tempFilePath) via await foreach; total count matches expected.</summary>
    [Test]
    public async Task ReadAsync_FilePath_YieldsAllRecords()
    {
        // Arrange — write a temp CSV file with 1 header + 3 data rows
        string tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "OrderId,CustomerName,Amount\r\n1,Alice,100.50\r\n2,Bob,200.00\r\n3,Charlie,300.75\r\n");

            // Act
            using var engine = new FileHelperAsyncEngine<CsvOrder>();
            var records = new System.Collections.Generic.List<CsvOrder>();
            await foreach (var record in engine.ReadAsync(tempFile))
            {
                records.Add(record);
            }

            // Assert
            await Assert.That(records.Count).IsEqualTo(3);
            await Assert.That(records[0].OrderId).IsEqualTo(1);
            await Assert.That(records[1].OrderId).IsEqualTo(2);
            await Assert.That(records[2].OrderId).IsEqualTo(3);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>ENGI-11: ReadAsync with a pre-cancelled CancellationToken throws OperationCanceledException.</summary>
    [Test]
    public async Task ReadAsync_CancellationToken_StopsIteration()
    {
        // Arrange — write a temp CSV file with data rows
        string tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "OrderId,CustomerName,Amount\r\n1,Alice,100.50\r\n2,Bob,200.00\r\n");

            // Act — use a pre-cancelled token
            using var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();

            using var engine = new FileHelperAsyncEngine<CsvOrder>();
            bool threwCancellation = false;
            try
            {
                await foreach (var record in engine.ReadAsync(tempFile, cts.Token))
                {
                    // Should not reach here
                }
            }
            catch (OperationCanceledException)
            {
                threwCancellation = true;
            }

            // Assert
            await Assert.That(threwCancellation).IsTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    // -------------------------------------------------------------------------
    // ENGI-14 — IEnumerable<T> / Dispose
    // -------------------------------------------------------------------------

    /// <summary>ENGI-14: BeginReadString then foreach over engine instance yields expected records.</summary>
    [Test]
    public async Task GetEnumerator_IteratesOpenSession()
    {
        const string content = "header\r\n1,A,1.00\r\n2,B,2.00\r\n3,C,3.00\r\n";

        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadString(content);

        var results = new System.Collections.Generic.List<CsvOrder>();
        foreach (var record in engine)
        {
            results.Add(record);
        }

        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].OrderId).IsEqualTo(1);
        await Assert.That(results[2].OrderId).IsEqualTo(3);
    }

    /// <summary>ENGI-14: BeginReadString, Dispose, then second Dispose does not throw (idempotent).</summary>
    [Test]
    public async Task Dispose_ClosesReader_NoPendingResources()
    {
        const string content = "header\r\n1,Alice,10.00\r\n";
        var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadString(content);
        engine.ReadNext();

        // First dispose
        engine.Dispose();
        // Second dispose — must not throw (idempotent)
        engine.Dispose();

        // Verify engine is usable again: open a new session after double-dispose succeeds
        engine.BeginReadString("header\r\n2,Bob,20.00\r\n");
        var next = engine.ReadNext();
        await Assert.That(next).IsNotNull();
        await Assert.That(next!.OrderId).IsEqualTo(2);
    }
}
