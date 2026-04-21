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
    [DisplayName("BeginReadFile opens file and ReadNext returns first CsvOrder record with correct values")]
    public async Task BeginReadFile_DelimitedRecord_OpensReader()
    {
        // Arrange: Write temp CSV file with header + 1 data row
        string tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "OrderId,CustomerName,Amount\r\n1,Alice,100.50\r\n");

            // Act: Open file with BeginReadFile and read first record
            using var engine = new FileHelperAsyncEngine<CsvOrder>();
            engine.BeginReadFile(tempFile);
            var record = engine.ReadNext();

            // Assert: Record parsed with correct OrderId and CustomerName
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
    [DisplayName("BeginReadStream with StringReader parses first CsvOrder record correctly")]
    public async Task BeginReadStream_FromTextReader_OpensReader()
    {
        // Arrange: Create StringReader with header + data row
        const string content = "OrderId,CustomerName,Amount\r\n42,Bob,200.00\r\n";
        using var reader = new StringReader(content);

        // Act: Open stream with BeginReadStream and read first record
        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadStream(reader);
        var record = engine.ReadNext();

        // Assert: Record parsed with correct OrderId and CustomerName
        await Assert.That(record).IsNotNull();
        await Assert.That(record!.OrderId).IsEqualTo(42);
        await Assert.That(record.CustomerName).IsEqualTo("Bob");
    }

    /// <summary>ENGI-09: BeginReadString with CSV content; ReadNext returns first CsvOrder with correct OrderId.</summary>
    [Test]
    [DisplayName("BeginReadString parses first data record skipping header line")]
    public async Task BeginReadString_ParsesFirstRecord()
    {
        // CsvOrder has [IgnoreFirst(1)] so first line is header
        // Arrange: Create CSV content with header line followed by data
        const string content = "header\r\n7,Charlie,50.00\r\n";

        // Act: Open string with BeginReadString and read first data record
        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadString(content);
        var record = engine.ReadNext();

        // Assert: Record parsed with correct OrderId=7
        await Assert.That(record).IsNotNull();
        await Assert.That(record!.OrderId).IsEqualTo(7);
    }

    // -------------------------------------------------------------------------
    // ENGI-10 — ReadNext() / ReadNext(int)
    // -------------------------------------------------------------------------

    /// <summary>ENGI-10: BeginReadString on single-record content; second ReadNext() call returns null.</summary>
    [Test]
    [DisplayName("ReadNext returns null when reaching end of single-record input")]
    public async Task ReadNext_ReturnsNullAtEof()
    {
        // Arrange: Create content with header + 1 data row, open with BeginReadString
        const string content = "header\r\n1,Alice,10.00\r\n";

        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadString(content);

        var first = engine.ReadNext();

        // Act: Attempt to read second record past EOF
        var second = engine.ReadNext();

        // Assert: First record present, second is null at EOF
        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNull();
    }

    /// <summary>ENGI-10: BeginReadString on 5-record content; ReadNext(3) returns array of length 3.</summary>
    [Test]
    [DisplayName("ReadNext(3) returns batch of exactly 3 records from 5-record input")]
    public async Task ReadNext_WithCount_ReturnsBatch()
    {
        // Arrange: Create content with header + 5 data rows, open with BeginReadString
        const string content = "header\r\n1,A,1.00\r\n2,B,2.00\r\n3,C,3.00\r\n4,D,4.00\r\n5,E,5.00\r\n";

        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadString(content);

        // Act: Read batch of 3 records
        var batch = engine.ReadNext(3);

        // Assert: Batch contains exactly 3 records with correct OrderIds
        await Assert.That(batch.Length).IsEqualTo(3);
        await Assert.That(batch[0].OrderId).IsEqualTo(1);
        await Assert.That(batch[2].OrderId).IsEqualTo(3);
    }

    /// <summary>ENGI-10: BeginReadString with 1 header line via CsvOrder [IgnoreFirst(1)]; ReadNext returns first data record.</summary>
    [Test]
    [DisplayName("ReadNext skips header line and returns first data record with HeaderText captured")]
    public async Task ReadNext_IgnoreFirst_SkipsHeaderLines()
    {
        // Arrange: Create content with header line, open with BeginReadString
        const string content = "OrderId,CustomerName,Amount\r\n99,Dave,999.00\r\n";

        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadString(content);

        // Act: Read first data record (skipping header)
        var record = engine.ReadNext();

        // Assert: Record has OrderId=99 and HeaderText captured the skipped line
        await Assert.That(record).IsNotNull();
        await Assert.That(record!.OrderId).IsEqualTo(99);
        await Assert.That(engine.HeaderText).IsNotNull();
    }

    /// <summary>
    /// Regression: quoted delimiters must not be counted as field separators
    /// by pre-parse validation.
    /// </summary>
    [Test]
    [DisplayName("ReadNext handles quoted delimiter without throwing field count precheck error")]
    public async Task ReadNext_QuotedDelimiter_DoesNotThrowFieldCountPrecheckError()
    {
        // Arrange: Create engine for QuotedWithTrailing and open string with quoted field containing delimiter
        using var engine = new FileHelperAsyncEngine<QuotedWithTrailing>();
        engine.BeginReadString("\"a,b\",7");

        // Act: Read record with quoted field containing comma
        var record = engine.ReadNext();

        // Assert: Record parsed with correct Description and Count values
        await Assert.That(record).IsNotNull();
        await Assert.That(record!.Description).IsEqualTo("a,b");
        await Assert.That(record.Count).IsEqualTo(7);
    }

    /// <summary>
    /// Regression: with IgnoreLast ring buffering enabled, line numbers must reflect
    /// the physical line number of the returned record.
    /// </summary>
    [Test]
    [DisplayName("ReadNext with IgnoreLast tracks correct line number for each returned record")]
    public async Task ReadNext_WithIgnoreLast_LineNumberTracksReturnedRecordLine()
    {
        // Arrange: Create engine for FooteredRecord and open string with 2 data rows + footer
        using var engine = new FileHelperAsyncEngine<FooteredRecord>();
        engine.BeginReadString("1,A\r\n2,B\r\nFOOTER");

        // Act & Assert: First record at line 1
        var first = engine.ReadNext();
        await Assert.That(first).IsNotNull();
        await Assert.That(engine.LineNumber).IsEqualTo(1);

        // Act & Assert: Second record at line 2
        var second = engine.ReadNext();
        await Assert.That(second).IsNotNull();
        await Assert.That(engine.LineNumber).IsEqualTo(2);

        // Act: Read past end (footer skipped)
        var end = engine.ReadNext();
        await Assert.That(end).IsNull();
    }

    // -------------------------------------------------------------------------
    // ENGI-11 — ReadAsync IAsyncEnumerable (deferred to Plan 04)
    // -------------------------------------------------------------------------

    /// <summary>ENGI-11: ReadAsync(tempFilePath) via await foreach; total count matches expected.</summary>
    [Test]
    [DisplayName("ReadAsync with file path yields all 3 records via await foreach")]
    public async Task ReadAsync_FilePath_YieldsAllRecords()
    {
        // Arrange: Write temp CSV file with header + 3 data rows
        string tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "OrderId,CustomerName,Amount\r\n1,Alice,100.50\r\n2,Bob,200.00\r\n3,Charlie,300.75\r\n");

            // Act: Enumerate all records via await foreach ReadAsync
            using var engine = new FileHelperAsyncEngine<CsvOrder>();
            var records = new System.Collections.Generic.List<CsvOrder>();
            await foreach (var record in engine.ReadAsync(tempFile))
            {
                records.Add(record);
            }

            // Assert: All three records yielded with correct OrderIds
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
    [DisplayName("ReadAsync with pre-cancelled token throws OperationCanceledException immediately")]
    public async Task ReadAsync_CancellationToken_StopsIteration()
    {
        // Arrange: Write temp CSV file with data rows, create pre-cancelled token
        string tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "OrderId,CustomerName,Amount\r\n1,Alice,100.50\r\n2,Bob,200.00\r\n");

            using var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();

            using var engine = new FileHelperAsyncEngine<CsvOrder>();
            bool threwCancellation = false;

            // Act: Attempt to enumerate with cancelled token
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

            // Assert: OperationCanceledException was thrown
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
    [DisplayName("GetEnumerator iterates open session yielding all 3 records")]
    public async Task GetEnumerator_IteratesOpenSession()
    {
        // Arrange: Create content with header + 3 data rows, open with BeginReadString
        const string content = "header\r\n1,A,1.00\r\n2,B,2.00\r\n3,C,3.00\r\n";

        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadString(content);

        // Act: Enumerate records using foreach
        var results = new System.Collections.Generic.List<CsvOrder>();
        foreach (var record in engine)
        {
            results.Add(record);
        }

        // Assert: All three records yielded with correct OrderIds
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].OrderId).IsEqualTo(1);
        await Assert.That(results[2].OrderId).IsEqualTo(3);
    }

    /// <summary>ENGI-14: BeginReadString, Dispose, then second Dispose does not throw (idempotent).</summary>
    [Test]
    [DisplayName("Dispose closes reader and double-dispose is idempotent without throwing")]
    public async Task Dispose_ClosesReader_NoPendingResources()
    {
        // Arrange: Create content, open session, read one record, then dispose twice
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

        // Act: Read record from new session after double-dispose
        var next = engine.ReadNext();

        // Assert: New session works correctly with OrderId=2
        await Assert.That(next).IsNotNull();
        await Assert.That(next!.OrderId).IsEqualTo(2);
    }

    /// <summary>
    /// Regression: a new read session must not keep stale HeaderText from a prior session.
    /// </summary>
    [Test]
    [DisplayName("BeginReadString resets HeaderText when opening new session")]
    public async Task BeginReadString_NewSession_ResetsHeaderText()
    {
        // Arrange: Open first session with header, read record to capture HeaderText
        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.BeginReadString("HEADER\r\n1,Alice,10.00");
        _ = engine.ReadNext();

        // Assert: HeaderText captured from first session
        await Assert.That(engine.HeaderText).IsEqualTo("HEADER");
        engine.Close();

        // Act: Open new session with empty content
        engine.BeginReadString(string.Empty);

        // Assert: HeaderText reset to null for new session
        await Assert.That(engine.HeaderText).IsNull();
    }

    /// <summary>
    /// Regression: a new read session must not keep stale FooterText from a prior session.
    /// </summary>
    [Test]
    [DisplayName("BeginReadString resets FooterText when opening new session")]
    public async Task BeginReadString_NewSession_ResetsFooterText()
    {
        // Arrange: Open first session with footer, read all records to capture FooterText
        using var engine = new FileHelperAsyncEngine<FooteredRecord>();
        engine.BeginReadString("1,A\r\n2,B\r\nFOOTER");
        _ = engine.ReadNext();
        _ = engine.ReadNext();
        _ = engine.ReadNext();

        // Assert: FooterText captured from first session
        await Assert.That(engine.FooterText).IsEqualTo("FOOTER");
        engine.Close();

        // Act: Open new session with empty content and attempt read
        engine.BeginReadString(string.Empty);
        _ = engine.ReadNext();

        // Assert: FooterText reset to null for new session
        await Assert.That(engine.FooterText).IsNull();
    }
}
