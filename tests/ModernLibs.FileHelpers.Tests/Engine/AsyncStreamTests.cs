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

    private static List<CsvOrder> Records(int count, string name = "name")
    {
        var records = new List<CsvOrder>(count);
        for (int i = 1; i <= count; i++)
            records.Add(new CsvOrder { OrderId = i, CustomerName = $"{name}{i}", Amount = i + 0.5m });
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

    /// <summary>Yields <paramref name="records"/>, then throws <paramref name="exception"/> (default <see cref="InvalidOperationException"/>("boom")).</summary>
    private static IEnumerable<CsvOrder> ThrowAfter(IEnumerable<CsvOrder> records, Exception? exception = null)
    {
        foreach (var record in records)
            yield return record;
        throw exception ?? new InvalidOperationException("boom");
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
        await Assert.That(seen).IsEqualTo(10); // the token is checked before each line
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

    private static Encoding EncodingNamed(string name) => name switch
    {
        "utf-8-bom" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        "utf-8" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        "utf-16" => Encoding.Unicode,
        "utf-16BE" => Encoding.BigEndianUnicode,
        "utf-32" => Encoding.UTF32,
        "latin1" => Encoding.Latin1,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static byte[] WriteFileBytes<T>(FileHelperEngine<T> engine, IEnumerable<T> records)
        where T : class
    {
        string temp = Path.GetTempFileName();
        try
        {
            engine.WriteFile(temp, records);
            return File.ReadAllBytes(temp);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    /// <summary>
    /// The text is a prefix of <paramref name="expected"/> that ends at a record boundary (or is empty); the callers
    /// write with the default <c>NewLineForWrite</c>, <see cref="Environment.NewLine"/>.
    /// </summary>
    private static async Task AssertWholeRecordPrefix(string written, string expected)
    {
        await Assert.That(expected.StartsWith(written, StringComparison.Ordinal)).IsTrue();
        await Assert.That(written.Length == 0 || written.EndsWith(Environment.NewLine, StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    [Arguments("utf-8-bom")]
    [Arguments("utf-8")]
    [Arguments("utf-16")]
    [Arguments("utf-16BE")]
    [Arguments("utf-32")]
    [Arguments("latin1")]
    public async Task WriteStreamAsync_Stream_Writes_The_Bytes_Of_WriteFile_And_Does_No_Sync_IO(string encodingName)
    {
        var encoding = EncodingNamed(encodingName);
        foreach (int count in new[] { 0, 3, 5000 })
        {
            foreach (string? header in new[] { null, Header })
            {
                var engine = new FileHelperEngine<CsvOrder>(encoding) { HeaderText = header, FooterText = header is null ? null : "end" };
                var records = Records(count, "né€\U0001F600"); // é, €, an emoji (a surrogate pair)
                byte[] expected = WriteFileBytes(engine, records);

                var inner = new MemoryStream();
                var stream = new AsyncOnlyStream(inner);
                await engine.WriteStreamAsync(stream, records);
                await Assert.That(inner.ToArray().SequenceEqual(expected)).IsTrue();
                await Assert.That(engine.TotalRecords).IsEqualTo(count);
                await Assert.That(stream.SyncCalls).IsEqualTo(0);
                await Assert.That(stream.Disposed).IsFalse();

                var seekable = new MemoryStream();
                await engine.WriteStreamAsync(seekable, records);
                await Assert.That(seekable.ToArray().SequenceEqual(expected)).IsTrue();

                // As StreamWriter: no preamble when the seekable stream is not at position 0.
                var appended = new MemoryStream();
                appended.Write([1, 2, 3]);
                await engine.WriteStreamAsync(appended, records);
                byte[] expectedAppended = [1, 2, 3, .. expected.AsSpan(encoding.Preamble.Length)];
                await Assert.That(appended.ToArray().SequenceEqual(expectedAppended)).IsTrue();
            }
        }
    }

    /// <summary>
    /// Every record starts with a low and ends with a high surrogate and the terminator is empty, so a pair is formed
    /// across each record boundary, also where a 16,384-character block ends. 1,000-character records: a block ends
    /// after the 17th record, so 17 records leave an empty final block with the high surrogate still pending in the
    /// encoder, and 34 records end exactly on a block end.
    /// </summary>
    [Test]
    [Arguments("utf-8", 17)]
    [Arguments("utf-8", 34)]
    [Arguments("utf-8", 40)]
    [Arguments("utf-16", 17)]
    [Arguments("utf-16", 34)]
    [Arguments("utf-16", 40)]
    [Arguments("utf-32", 17)]
    [Arguments("utf-32", 34)]
    [Arguments("utf-32", 40)]
    public async Task WriteStreamAsync_Stream_Splits_A_Surrogate_Pair_At_A_Block_End_Like_WriteFile(string encodingName, int count)
    {
        var engine = new FileHelperEngine<SurrogateTail>(EncodingNamed(encodingName)) { NewLineForWrite = string.Empty };
        var records = new List<SurrogateTail>();
        for (int i = 0; i < count; i++)
            records.Add(new SurrogateTail { Text = "\uDE00" + new string('a', 998) + "\uD83D" });
        byte[] expected = WriteFileBytes(engine, records);

        var stream = new MemoryStream();
        await engine.WriteStreamAsync(stream, records);

        await Assert.That(stream.ToArray().SequenceEqual(expected)).IsTrue();
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
    public async Task WriteStreamAsync_TextWriter_With_No_Records_Equals_WriteString()
    {
        var engine = new FileHelperEngine<CsvOrder> { HeaderText = Header, FooterText = "end" };
        var writer = new StringWriter();

        await engine.WriteStreamAsync(writer, []);

        await Assert.That(writer.ToString()).IsEqualTo(engine.WriteString([]));
        await Assert.That(engine.TotalRecords).IsEqualTo(0);
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
    public async Task A_Throwing_Write_Event_Propagates_And_Keeps_The_Records_Before_It()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        engine.BeforeWriteRecord += (_, e) =>
        {
            if (e.Record.OrderId == 3)
                throw new InvalidOperationException("handler");
        };
        var writer = new StringWriter();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.WriteStreamAsync(writer, Records(5)));

        await Assert.That(thrown!.Message).IsEqualTo("handler");
        await Assert.That(writer.ToString()).IsEqualTo(new FileHelperEngine<CsvOrder>().WriteString(Records(2)));
    }

    [Test]
    public async Task A_Source_Fault_Wins_When_The_Write_After_It_Fails_Or_Is_Cancelled()
    {
        var engine = new FileHelperEngine<CsvOrder>();

        // The writer fails on the write of the records before the fault.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.WriteStreamAsync(new FailingWriter(), ThrowAfter(Records(3))));
        await Assert.That(thrown!.Message).IsEqualTo("boom");

        // The stream fails on that write.
        thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.WriteStreamAsync(new ScriptedWriteStream { FailWrites = true }, ThrowAfter(Records(3))));
        await Assert.That(thrown!.Message).IsEqualTo("boom");

        // The token is cancelled during that write.
        using var cts = new CancellationTokenSource();
        thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.WriteStreamAsync(new CancellingWriter(cts), ThrowAfter(Records(3)), cts.Token));
        await Assert.That(thrown!.Message).IsEqualTo("boom");
    }

    [Test]
    public async Task A_Source_That_Throws_Its_Own_OperationCanceledException_Keeps_The_Records_Before_It()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        var writer = new StringWriter();

        await Assert.ThrowsAsync<SourceCanceledException>(
            () => engine.WriteStreamAsync(writer, ThrowAfter(Records(3), new SourceCanceledException())));

        await Assert.That(writer.ToString()).IsEqualTo(engine.WriteString(Records(3)));
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

        // At least one block reached the stream before the cancellation, and it holds whole records only.
        string written = new UTF8Encoding(false).GetString(inner.ToArray().AsSpan(Encoding.UTF8.Preamble.Length));
        await Assert.That(written.Length).IsGreaterThan(0);
        await AssertWholeRecordPrefix(written, engine.WriteString(Records(20_001)));
    }

    [Test]
    public async Task A_Token_Cancelled_Before_The_Call_Writes_Nothing()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var inner = new MemoryStream();
        var writer = new StringWriter();

        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.WriteStreamAsync(new AsyncOnlyStream(inner), Records(3), cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.WriteStreamAsync(writer, Records(3), cts.Token));

        await Assert.That(inner.Length).IsEqualTo(0); // not even the preamble
        await Assert.That(writer.ToString()).IsEqualTo(string.Empty);
    }

    [Test]
    [Arguments(1, true)]
    [Arguments(1, false)]
    [Arguments(2, true)]
    [Arguments(2, false)]
    [Arguments(7, true)]
    [Arguments(7, false)]
    [Arguments(40, true)]
    [Arguments(40, false)]
    public async Task A_Cancellation_Inside_A_Stream_Write_Leaves_Whole_Records(int cancelAtWrite, bool streamHonoursIt)
    {
        var engine = new FileHelperEngine<CsvOrder>(new UTF8Encoding(false));
        var records = Records(20_000, new string('n', 200));
        using var cts = new CancellationTokenSource();
        var stream = new ScriptedWriteStream { Cancel = cts, CancelAtWrite = cancelAtWrite, HonourCancellation = streamHonoursIt };

        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.WriteStreamAsync(stream, records, cts.Token));

        string written = Encoding.UTF8.GetString(stream.Inner.ToArray());
        await AssertWholeRecordPrefix(written, engine.WriteString(records));
        await Assert.That(stream.Writes).IsEqualTo(cancelAtWrite); // nothing is written after the cancellation
    }

    [Test]
    public async Task A_Cancellation_Inside_The_Write_Of_A_Record_Larger_Than_A_Block_Leaves_Whole_Records()
    {
        var engine = new FileHelperEngine<CsvOrder>(new UTF8Encoding(false));
        var records = Records(2);
        records.Add(new CsvOrder { OrderId = 3, CustomerName = new string('x', 100_000), Amount = 3.5m });
        records.AddRange(Records(2));
        using var cts = new CancellationTokenSource();
        var stream = new ScriptedWriteStream { Cancel = cts, CancelAtWrite = 1, HonourCancellation = false };

        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.WriteStreamAsync(stream, records, cts.Token));

        string written = Encoding.UTF8.GetString(stream.Inner.ToArray());
        await AssertWholeRecordPrefix(written, engine.WriteString(records));
        await Assert.That(written.Length).IsGreaterThan(100_000);
    }

#pragma warning disable RCS1194 // Test-only exception: thrown by the record source, never constructed with a message.
    private sealed class SourceCanceledException : OperationCanceledException;
#pragma warning restore RCS1194

    /// <summary>Fails every asynchronous write with <see cref="IOException"/>.</summary>
    private sealed class FailingWriter : StringWriter
    {
        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) =>
            Task.FromException(new IOException("disk full"));
    }

    /// <summary>Cancels the token on every asynchronous write, and honours it.</summary>
    private sealed class CancellingWriter(CancellationTokenSource cts) : StringWriter
    {
        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            cts.Cancel();
            return Task.FromCanceled(cancellationToken);
        }
    }

    /// <summary>
    /// Write-only, asynchronous-only stream over <see cref="Inner"/>. Write number <see cref="CancelAtWrite"/>
    /// cancels <see cref="Cancel"/>, yields, then writes its whole buffer, or nothing when
    /// <see cref="HonourCancellation"/> (it throws <see cref="OperationCanceledException"/> instead).
    /// <see cref="FailWrites"/> fails every write with <see cref="IOException"/>.
    /// </summary>
    private sealed class ScriptedWriteStream : Stream
    {
        public MemoryStream Inner { get; } = new();

        public CancellationTokenSource? Cancel { get; init; }

        public int CancelAtWrite { get; init; }

        public bool HonourCancellation { get; init; }

        public bool FailWrites { get; init; }

        public int Writes { get; private set; }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (FailWrites)
                throw new IOException("disk full");

            if (++Writes == CancelAtWrite)
            {
                await Cancel!.CancelAsync();
                await Task.Yield();
                if (HonourCancellation)
                    cancellationToken.ThrowIfCancellationRequested();
            }

            await Inner.WriteAsync(buffer, CancellationToken.None);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Flush() => throw new NotSupportedException("sync I/O");

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("sync I/O");
    }
}
