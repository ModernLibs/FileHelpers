// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests.
#pragma warning disable CA1849
// CA1859: Interface-typed variables are intentional — tests verify interface-based usage.
#pragma warning disable CA1859

using System.Globalization;
using System.Text;
using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// <c>ReadAsync(Stream)</c>, <c>ReadAsync(TextReader)</c> and <c>WriteStreamAsync</c>: the same records and text as the
/// synchronous twins, asynchronous I/O only, the caller's stream or writer never disposed.
/// </summary>
[Category("Async")]
public sealed class AsyncStreamTests
{
    // CsvOrder: [IgnoreFirst(1)], so the first line is the header.
    private const string Header = "id,name,amount";

    // HeaderFooterListRecord: [IgnoreFirst(1)] and [IgnoreLast(1)]; line 3 does not parse.
    private const string HeaderFooterText = "head\r\n1,a\r\nx,b\r\n3,c\r\nfoot\r\n";

    private static string Text(int count)
    {
        var sb = new StringBuilder(Header).Append("\r\n");
        for (int i = 1; i <= count; i++)
            sb.Append(CultureInfo.InvariantCulture, $"{i},name{i},{i}.5\r\n");
        return sb.ToString();
    }

    private static byte[] Build(int count) => Encoding.UTF8.GetBytes(Text(count));

    private static List<CsvOrder> Records(int count)
    {
        var records = new List<CsvOrder>(count);
        for (int i = 1; i <= count; i++)
            records.Add(new CsvOrder { OrderId = i, CustomerName = $"name{i}", Amount = i + 0.5m });
        return records;
    }

    private static async Task<List<T>> Collect<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }

    private static async Task AssertSame(IReadOnlyList<CsvOrder> actual, IReadOnlyList<CsvOrder> expected)
    {
        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i].OrderId).IsEqualTo(expected[i].OrderId);
            await Assert.That(actual[i].CustomerName).IsEqualTo(expected[i].CustomerName);
            await Assert.That(actual[i].Amount).IsEqualTo(expected[i].Amount);
        }
    }

    /// <summary>Yields <paramref name="records"/>, then throws <see cref="InvalidOperationException"/>("boom").</summary>
    private static IEnumerable<CsvOrder> ThrowAfter(IEnumerable<CsvOrder> records)
    {
        foreach (var record in records)
            yield return record;
        throw new InvalidOperationException("boom");
    }

    /// <summary>Yields <paramref name="count"/> records and cancels <paramref name="cts"/> after the last one.</summary>
    private static IEnumerable<CsvOrder> CancelAfter(int count, CancellationTokenSource cts)
    {
        for (int i = 1; ; i++)
        {
            yield return new CsvOrder { OrderId = i, CustomerName = $"name{i}", Amount = i + 0.5m };
            if (i == count)
                cts.Cancel();
        }
    }

    [Test]
    public async Task AsyncOnlyStream_Detects_The_Synchronous_Twins()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        var readStream = new AsyncOnlyStream(new MemoryStream(Build(2)));
        await Assert.That(() => engine.ReadStream(readStream)).Throws<NotSupportedException>();
        await Assert.That(readStream.SyncCalls).IsGreaterThan(0);

        var writeStream = new AsyncOnlyStream(new MemoryStream());
        await Assert.That(() =>
        {
            using var writer = new StreamWriter(writeStream, Encoding.UTF8, 1024, leaveOpen: true);
            engine.WriteStream(writer, Records(2));
        }).Throws<NotSupportedException>();
        await Assert.That(writeStream.SyncCalls).IsGreaterThan(0);
    }

    // =========================================================================
    // Reads
    // =========================================================================

    [Test]
    public async Task ReadAsync_Stream_Equals_ReadString_And_Does_No_Sync_IO()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        var expected = engine.ReadString(Text(5000));
        var stream = new AsyncOnlyStream(new MemoryStream(Build(5000)));

        var actual = await Collect(engine.ReadAsync(stream));

        await AssertSame(actual, expected);
        await Assert.That(engine.HeaderText).IsEqualTo(Header);
        await Assert.That(engine.TotalRecords).IsEqualTo(5000);
        await Assert.That(engine.LineNumber).IsEqualTo(5001);
        await Assert.That(stream.SyncCalls).IsEqualTo(0);
        await Assert.That(stream.Disposed).IsFalse();
    }

    [Test]
    public async Task ReadAsync_TextReader_Equals_ReadString()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        var expected = engine.ReadString(Text(5000));
        var reader = new StringReader(Text(5000));

        var actual = await Collect(engine.ReadAsync(reader));

        await AssertSame(actual, expected);
        await Assert.That(engine.HeaderText).IsEqualTo(Header);
        await Assert.That(engine.TotalRecords).IsEqualTo(5000);
        await Assert.That(engine.LineNumber).IsEqualTo(5001);
        await Assert.That(reader.Peek()).IsEqualTo(-1); // not disposed: Peek would throw ObjectDisposedException
    }

    [Test]
    public async Task ReadAsync_Stream_Honours_The_Byte_Order_Mark_And_The_Current_Position()
    {
        var engine = new FileHelperEngine<CsvOrder> { Encoding = Encoding.Latin1 };
        var expected = engine.ReadString(Text(3));
        byte[] prefix = [1, 2, 3];
        var inner = new MemoryStream([.. prefix, .. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(Text(3))]);
        inner.Position = prefix.Length;

        var actual = await Collect(engine.ReadAsync(new AsyncOnlyStream(inner)));

        await AssertSame(actual, expected);
    }

    [Test]
    public async Task AsyncEngine_ReadAsync_Stream_And_TextReader_Equal_The_Sync_Engine()
    {
        var sync = new FileHelperEngine<CsvOrder>();
        var expected = sync.ReadString(Text(3000));
        var engine = new FileHelperAsyncEngine<CsvOrder>();

        var stream = new AsyncOnlyStream(new MemoryStream(Build(3000)));
        var fromStream = await Collect(engine.ReadAsync(stream));
        await AssertSame(fromStream, expected);
        await Assert.That(engine.HeaderText).IsEqualTo(sync.HeaderText);
        await Assert.That(engine.TotalRecords).IsEqualTo(sync.TotalRecords);
        await Assert.That(engine.LineNumber).IsEqualTo(sync.LineNumber);
        await Assert.That(stream.SyncCalls).IsEqualTo(0);
        await Assert.That(stream.Disposed).IsFalse();

        var reader = new StringReader(Text(3000));
        var fromReader = await Collect(engine.ReadAsync(reader));
        await AssertSame(fromReader, expected);
        await Assert.That(engine.HeaderText).IsEqualTo(sync.HeaderText);
        await Assert.That(engine.TotalRecords).IsEqualTo(sync.TotalRecords);
        await Assert.That(engine.LineNumber).IsEqualTo(sync.LineNumber);
        await Assert.That(reader.Peek()).IsEqualTo(-1);
    }

    [Test]
    public async Task Header_Footer_LineNumber_And_Errors_Match_ReadString()
    {
        var sync = new FileHelperEngine<HeaderFooterListRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        var expected = sync.ReadString(HeaderFooterText);
        await Assert.That(expected.Length).IsEqualTo(2);
        await Assert.That(sync.ErrorManager.ErrorCount).IsEqualTo(1);

        var engine = new FileHelperEngine<HeaderFooterListRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        var asyncEngine = new FileHelperAsyncEngine<HeaderFooterListRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        var runs = new (Func<Task<List<HeaderFooterListRecord>>> Read, Func<(string? Header, string? Footer, int Line, int Total, ErrorManager Errors)> State)[]
        {
            (() => Collect(engine.ReadAsync(new AsyncOnlyStream(new MemoryStream(Encoding.UTF8.GetBytes(HeaderFooterText))))),
                () => (engine.HeaderText, engine.FooterText, engine.LineNumber, engine.TotalRecords, engine.ErrorManager)),
            (() => Collect(engine.ReadAsync(new StringReader(HeaderFooterText))),
                () => (engine.HeaderText, engine.FooterText, engine.LineNumber, engine.TotalRecords, engine.ErrorManager)),
            (() => Collect(asyncEngine.ReadAsync(new AsyncOnlyStream(new MemoryStream(Encoding.UTF8.GetBytes(HeaderFooterText))))),
                () => (asyncEngine.HeaderText, asyncEngine.FooterText, asyncEngine.LineNumber, asyncEngine.TotalRecords, asyncEngine.ErrorManager)),
            (() => Collect(asyncEngine.ReadAsync(new StringReader(HeaderFooterText))),
                () => (asyncEngine.HeaderText, asyncEngine.FooterText, asyncEngine.LineNumber, asyncEngine.TotalRecords, asyncEngine.ErrorManager)),
        };

        foreach (var (read, state) in runs)
        {
            var actual = await read();
            var (header, footer, line, total, errors) = state();

            await Assert.That(actual.Select(r => r.Id)).IsEquivalentTo(expected.Select(r => r.Id));
            await Assert.That(header).IsEqualTo(sync.HeaderText);
            await Assert.That(footer).IsEqualTo(sync.FooterText);
            await Assert.That(line).IsEqualTo(sync.LineNumber);
            await Assert.That(total).IsEqualTo(sync.TotalRecords);
            await Assert.That(errors.ErrorCount).IsEqualTo(1);
            await Assert.That(errors.Errors[0].LineNumber).IsEqualTo(sync.ErrorManager.Errors[0].LineNumber);
            await Assert.That(errors.Errors[0].RecordString).IsEqualTo(sync.ErrorManager.Errors[0].RecordString);
        }
    }

    [Test]
    public async Task Leaving_The_Loop_Early_Leaves_The_Stream_Open_And_The_Engine_Usable()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        var stream = new AsyncOnlyStream(new MemoryStream(Build(5000)));
        int seen = 0;
        await foreach (var _ in engine.ReadAsync(stream))
        {
            if (++seen == 3)
                break;
        }

        await Assert.That(stream.Disposed).IsFalse();
        await Assert.That(stream.SyncCalls).IsEqualTo(0);
        await Assert.That(engine.ReadString(Text(2)).Length).IsEqualTo(2); // same engine reads again
    }

    [Test]
    public async Task Cancellation_Between_Records_Throws_OperationCanceledException_And_Leaves_The_Stream_Open()
    {
        using var cts = new CancellationTokenSource();
        var stream = new AsyncOnlyStream(new MemoryStream(Build(5000)));
        int seen = 0;
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in new FileHelperEngine<CsvOrder>().ReadAsync(stream, cts.Token))
            {
                if (++seen == 10)
                    await cts.CancelAsync();
            }
        });

        await Assert.That(stream.Disposed).IsFalse();
        await Assert.That(seen).IsLessThan(5000);
    }

    [Test]
    public async Task TextReader_Cancellation_Throws_OperationCanceledException_And_Leaves_The_Reader_Open()
    {
        using var cts = new CancellationTokenSource();
        var reader = new StringReader(Text(5000));
        int seen = 0;
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in new FileHelperEngine<CsvOrder>().ReadAsync(reader, cts.Token))
            {
                if (++seen == 10)
                    await cts.CancelAsync();
            }
        });

        await Assert.That(seen).IsEqualTo(10);
        await Assert.That(reader.Peek()).IsGreaterThanOrEqualTo(0); // not disposed (Peek would throw), not drained
    }

    [Test]
    public async Task AsyncEngine_Leaving_Early_Frees_The_Engine_And_A_Session_Blocks_ReadAsync()
    {
        var engine = new FileHelperAsyncEngine<CsvOrder>();
        var stream = new AsyncOnlyStream(new MemoryStream(Build(100)));
        await foreach (var _ in engine.ReadAsync(stream))
            break;
        await Assert.That(stream.Disposed).IsFalse();

        engine.BeginReadString(Text(2));
        await Assert.That(async () => await Collect(engine.ReadAsync(new StringReader(Text(2)))))
            .Throws<InvalidOperationException>();
        await Assert.That(async () => await Collect(engine.ReadAsync(new MemoryStream(Build(2)))))
            .Throws<InvalidOperationException>();
        engine.Close();

        await Assert.That((await Collect(engine.ReadAsync(new StringReader(Text(2))))).Count).IsEqualTo(2);
    }

    [Test]
    public async Task Null_Arguments_Throw_ArgumentNullException()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        var asyncEngine = new FileHelperAsyncEngine<CsvOrder>();
        var records = Records(1);

        await Assert.That(async () => await Collect(engine.ReadAsync((Stream)null!))).Throws<ArgumentNullException>();
        await Assert.That(async () => await Collect(engine.ReadAsync((TextReader)null!))).Throws<ArgumentNullException>();
        await Assert.That(async () => await Collect(asyncEngine.ReadAsync((Stream)null!))).Throws<ArgumentNullException>();
        await Assert.That(async () => await Collect(asyncEngine.ReadAsync((TextReader)null!))).Throws<ArgumentNullException>();
        await Assert.That(async () => await engine.WriteStreamAsync((Stream)null!, records)).Throws<ArgumentNullException>();
        await Assert.That(async () => await engine.WriteStreamAsync((TextWriter)null!, records)).Throws<ArgumentNullException>();
        await Assert.That(async () => await engine.WriteStreamAsync(new MemoryStream(), null!)).Throws<ArgumentNullException>();
        await Assert.That(async () => await engine.WriteStreamAsync(new StringWriter(), null!)).Throws<ArgumentNullException>();
    }

    // =========================================================================
    // Writes
    // =========================================================================

    [Test]
    public async Task WriteStreamAsync_Stream_Writes_The_Bytes_Of_WriteFile_And_Does_No_Sync_IO()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        var records = Records(5000);
        string temp = Path.GetTempFileName();
        byte[] expected;
        try
        {
            engine.WriteFile(temp, records);
            expected = File.ReadAllBytes(temp);
        }
        finally
        {
            File.Delete(temp);
        }

        var inner = new MemoryStream();
        var stream = new AsyncOnlyStream(inner);
        await engine.WriteStreamAsync(stream, records);

        await Assert.That(inner.ToArray().AsSpan().StartsWith(Encoding.UTF8.Preamble)).IsTrue();
        await Assert.That(inner.ToArray().SequenceEqual(expected)).IsTrue();
        await Assert.That(engine.TotalRecords).IsEqualTo(5000);
        await Assert.That(stream.SyncCalls).IsEqualTo(0);
        await Assert.That(stream.Disposed).IsFalse();
    }

    [Test]
    public async Task WriteStreamAsync_Stream_Uses_The_Engine_Encoding()
    {
        var engine = new FileHelperEngine<CsvOrder>(Encoding.Unicode) { HeaderText = Header };
        var records = Records(3);
        var inner = new MemoryStream();

        await engine.WriteStreamAsync(new AsyncOnlyStream(inner), records);

        byte[] expected = [.. Encoding.Unicode.Preamble, .. Encoding.Unicode.GetBytes(engine.WriteString(records))];
        await Assert.That(inner.ToArray().SequenceEqual(expected)).IsTrue();
    }

    [Test]
    public async Task WriteStreamAsync_TextWriter_Equals_WriteString_With_Header_Footer_And_Events()
    {
        var engine = new FileHelperEngine<CsvOrder> { HeaderText = Header, FooterText = "end" };
        engine.BeforeWriteRecord += (_, e) => e.SkipThisRecord = e.Record.OrderId == 2;
        engine.AfterWriteRecord += (_, e) => e.RecordLine = e.RecordLine.ToUpperInvariant();
        var records = Records(6000);

        string expected = engine.WriteString(records);
        int expectedTotal = engine.TotalRecords;
        var writer = new StringWriter();
        await engine.WriteStreamAsync(writer, records);

        await Assert.That(writer.ToString()).IsEqualTo(expected);
        await Assert.That(engine.TotalRecords).IsEqualTo(expectedTotal);
        await Assert.That(expectedTotal).IsEqualTo(5999);
        await Assert.That(expected).Contains("NAME3");
        await Assert.That(expected).DoesNotContain("\r\n2,");
        writer.Write('x'); // not disposed: Write would throw ObjectDisposedException
    }

    [Test]
    public async Task A_Throwing_Source_Propagates_And_Keeps_What_Was_Written()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        var inner = new MemoryStream();
        var stream = new AsyncOnlyStream(inner);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.WriteStreamAsync(stream, ThrowAfter(Records(3))));

        await Assert.That(thrown!.Message).IsEqualTo("boom");
        await stream.FlushAsync();
        string written = new UTF8Encoding(false).GetString(inner.ToArray().AsSpan(Encoding.UTF8.Preamble.Length));
        await Assert.That(written).IsEqualTo(engine.WriteString(Records(3)));
        await Assert.That(stream.SyncCalls).IsEqualTo(0);
        await Assert.That(stream.Disposed).IsFalse();
    }

    [Test]
    public async Task A_Throwing_Source_Through_A_TextWriter_Keeps_The_Complete_Lines()
    {
        var engine = new FileHelperEngine<CsvOrder> { FooterText = "end" };
        var writer = new StringWriter();

        await Assert.ThrowsAsync<InvalidOperationException>(() => engine.WriteStreamAsync(writer, ThrowAfter(Records(3))));

        await Assert.That(writer.ToString()).IsEqualTo(new FileHelperEngine<CsvOrder>().WriteString(Records(3)));
    }

    [Test]
    public async Task Cancellation_Throws_OperationCanceledException_Without_Sync_IO()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        using var cts = new CancellationTokenSource();
        var inner = new MemoryStream();
        var stream = new AsyncOnlyStream(inner);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.WriteStreamAsync(stream, CancelAfter(20_000, cts), cts.Token));

        await Assert.That(stream.SyncCalls).IsEqualTo(0);
        await Assert.That(stream.Disposed).IsFalse();

        // At least one flush reached the stream before the cancellation, and it holds whole lines only.
        string written = new UTF8Encoding(false).GetString(inner.ToArray().AsSpan(Encoding.UTF8.Preamble.Length));
        await Assert.That(written.Length).IsGreaterThan(0);
        await Assert.That(written.EndsWith("\r\n", StringComparison.Ordinal)).IsTrue();
        await Assert.That(written.Split("\r\n").Length - 1).IsLessThanOrEqualTo(20_001);
    }
}
