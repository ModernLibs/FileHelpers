// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Errors;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// E3–E11: every <see cref="FileHelperEngine{T}"/> read path runs the batch reference semantics
/// (header/footer, filters after multiline assembly, field-count validation, ErrorMode, events,
/// line numbers, per-read reset, recycling only for callback/streaming reads).
/// </summary>
[Category("ReadCore")]
public sealed class ReadCoreEngineTests
{
    private const string FramedInput = "HEADER\n1,a\n\n# note\nbad\n4,d\nTOTAL 2\nEND";

    private static async Task<T> WithTempFile<T>(string content, Func<string, Task<T>> action)
    {
        string path = Path.Combine(Path.GetTempPath(), "fh-readcore-" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(path, content);
        try
        {
            return await action(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---- E3: ReadFileAsync callback path ---------------------------------------------------

    [Test]
    [DisplayName("E3: ReadFileAsync applies header, footer, filters and SaveAndContinue like ReadFile")]
    public async Task ReadFileAsync_FullPipeline_MatchesReadFile()
    {
        // Arrange: framed input with one bad record; engine in SaveAndContinue
        var engine = new FileHelperEngine<RcFramed> { ErrorMode = ErrorMode.SaveAndContinue };
        var ids = new List<int>();

        // Act: async callback read
        int count = await WithTempFile(FramedInput, path => engine.ReadFileAsync(path, r => ids.Add(r.Id)));

        // Assert: records, header/footer, error line and position
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(string.Join(",", ids)).IsEqualTo("1,4");
        await Assert.That(engine.HeaderText).IsEqualTo("HEADER");
        await Assert.That(engine.FooterText).IsEqualTo("TOTAL 2\r\nEND");
        await Assert.That(engine.TotalRecords).IsEqualTo(2);
        await Assert.That(engine.LineNumber).IsEqualTo(6);
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(5);
    }

    [Test]
    [DisplayName("E3: ReadFileAsync throws OperationCanceledException when cancelled")]
    public async Task ReadFileAsync_Cancelled_Throws()
    {
        // Arrange: already cancelled token
        var engine = new FileHelperEngine<RcRecord>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act: read with the cancelled token
        var ex = await WithTempFile("1,a\n2,b", path => Capture.ExceptionAsync(() => engine.ReadFileAsync(path, _ => { }, cts.Token)));

        // Assert: cancellation surfaces as an exception, not a partial count
        await Assert.That(ex).IsAssignableTo<OperationCanceledException>();
    }

    [Test]
    [DisplayName("E3: ReadFileAsync raises BeforeReadRecord and honours SkipThisRecord")]
    public async Task ReadFileAsync_RaisesEvents()
    {
        // Arrange: skip records whose line starts with 2
        var engine = new FileHelperEngine<RcRecord>();
        engine.BeforeReadRecord += (_, e) => e.SkipThisRecord = e.RecordLine.StartsWith('2');
        var ids = new List<int>();

        // Act: async callback read
        await WithTempFile("1,a\n2,b\n3,c", path => engine.ReadFileAsync(path, r => ids.Add(r.Id)));

        // Assert: record 2 skipped
        await Assert.That(string.Join(",", ids)).IsEqualTo("1,3");
    }

    // ---- E4: footer = raw last lines on every path ----------------------------------------

    [Test]
    [DisplayName("E4: ReadSync never parses IgnoreLast footer lines and keeps their original text")]
    public async Task ReadSync_Footer_IsRawText()
    {
        // Arrange: footer lines that are not valid records; ThrowException mode
        var engine = new FileHelperEngine<RcFramed>();

        // Act: stream all records
        var ids = engine.ReadSync("HEADER\n1,a\n2,b\nTOTAL: 2\nEND").Select(r => r.Id).ToArray();

        // Assert: no parse attempt on footer, footer text verbatim
        await Assert.That(string.Join(",", ids)).IsEqualTo("1,2");
        await Assert.That(engine.FooterText).IsEqualTo("TOTAL: 2\r\nEND");
        await Assert.That(engine.HeaderText).IsEqualTo("HEADER");
    }

    [Test]
    [DisplayName("E4: ReadAsync and ReadFile(path, callback) treat the footer as raw lines (struct record)")]
    public async Task AsyncAndCallback_Footer_IsRawText()
    {
        // Arrange: struct record with one footer line that is not a record
        const string input = "1,10\n2,20\n-- end of file --\n";
        var asyncEngine = new FileHelperEngine<RcFooterStruct>();
        var callbackEngine = new FileHelperEngine<RcFooterStruct>();

        // Act: async enumerable and streaming callback reads
        var asyncValues = await WithTempFile(input, async path =>
        {
            var values = new List<int>();
            await foreach (var r in asyncEngine.ReadAsync(path))
                values.Add(r.Value);
            return values;
        });
        var callbackValues = await WithTempFile(input, path =>
        {
            var values = new List<int>();
            callbackEngine.ReadFile(path, r => values.Add(r.Value));
            return Task.FromResult(values);
        });

        // Assert: footer captured verbatim, never parsed
        await Assert.That(string.Join(",", asyncValues)).IsEqualTo("10,20");
        await Assert.That(asyncEngine.FooterText).IsEqualTo("-- end of file --");
        await Assert.That(string.Join(",", callbackValues)).IsEqualTo("10,20");
        await Assert.That(callbackEngine.FooterText).IsEqualTo("-- end of file --");
    }

    // ---- E5: SaveAndContinue never ends enumeration early -----------------------------------

    [Test]
    [DisplayName("E5: ReadAsync SaveAndContinue skips a bad record and keeps enumerating (class and struct)")]
    public async Task ReadAsync_SaveAndContinue_ContinuesPastBadRecord()
    {
        // Arrange: bad second line, SaveAndContinue on class and struct engines
        const string input = "1,10\nxx,20\n3,30";
        var classEngine = new FileHelperEngine<RcRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        var structEngine = new FileHelperEngine<RcStruct> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: enumerate both
        var (classIds, structIds) = await WithTempFile(input, async path =>
        {
            var c = new List<int>();
            await foreach (var r in classEngine.ReadAsync(path))
                c.Add(r.Id);
            var s = new List<int>();
            await foreach (var r in structEngine.ReadAsync(path))
                s.Add(r.Id);
            return (c, s);
        });

        // Assert: both continue, no default records, one error each at line 2
        await Assert.That(string.Join(",", classIds)).IsEqualTo("1,3");
        await Assert.That(string.Join(",", structIds)).IsEqualTo("1,3");
        await Assert.That(structEngine.TotalRecords).IsEqualTo(2);
        await Assert.That(classEngine.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
        await Assert.That(structEngine.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
    }

    // ---- E6: field-count validation in multiline mode --------------------------------------

    [Test]
    [DisplayName("E6: multiline batch read validates the delimited field count")]
    public async Task Multiline_ExtraField_IsReported()
    {
        // Arrange: three-field multiline record; second record has an extra field
        var engine = new FileHelperEngine<RcMultiline> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: read
        var records = engine.ReadString("1,\"a\nb\",2\n3,\"c\",4,EXTRA\n5,\"d\",6");

        // Assert: extra-field record rejected with its line number
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(3);
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsTypeOf<BadUsageException>();
    }

    // ---- E7: filters run after multiline assembly ------------------------------------------

    [Test]
    [DisplayName("E7: blank and comment-looking lines inside a quoted field are kept on every path")]
    public async Task FiltersAfterMultilineAssembly_KeepQuotedContent()
    {
        // Arrange: quoted field containing an empty line and a line starting with '#'
        const string input = "1,\"first\n\n# not a comment\nlast\"\n# real comment\n\n2,\"x\"";
        var engine = new FileHelperEngine<RcMultilineFiltered>();
        var asyncEngine = new FileHelperAsyncEngine<RcMultilineFiltered>();

        // Act: batch, streaming and pull-model reads
        var batch = engine.ReadString(input);
        var streamed = engine.ReadSync(input).ToArray();
        asyncEngine.BeginReadString(input);
        var pulled = asyncEngine.ReadRemaining();
        asyncEngine.Close();

        // Assert: quoted content intact, real comment/empty lines skipped
        const string expected = "first\n\n# not a comment\nlast";
        await Assert.That(batch.Length).IsEqualTo(2);
        await Assert.That(batch[0].Notes).IsEqualTo(expected);
        await Assert.That(streamed.Length).IsEqualTo(2);
        await Assert.That(streamed[0].Notes).IsEqualTo(expected);
        await Assert.That(pulled.Length).IsEqualTo(2);
        await Assert.That(pulled[0].Notes).IsEqualTo(expected);
    }

    // ---- E8: unclosed quote at EOF is an error ---------------------------------------------

    [Test]
    [DisplayName("E8: unclosed quote at end of input is reported by ReadSync, ReadAsync and ReadNext")]
    public async Task UnclosedQuoteAtEof_ReportedEverywhere()
    {
        // Arrange: second record never closes its quote
        const string input = "1,\"ok\",1\n2,\"never closed\n3";
        var syncEngine = new FileHelperEngine<RcMultiline> { ErrorMode = ErrorMode.SaveAndContinue };
        var asyncEngine = new FileHelperEngine<RcMultiline> { ErrorMode = ErrorMode.SaveAndContinue };
        var pullEngine = new FileHelperAsyncEngine<RcMultiline> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: read through three paths
        var synced = syncEngine.ReadSync(input).ToArray();
        var asynced = await WithTempFile(input, async path =>
        {
            var list = new List<RcMultiline>();
            await foreach (var r in asyncEngine.ReadAsync(path))
                list.Add(r);
            return list;
        });
        pullEngine.BeginReadString(input);
        var pulled = pullEngine.ReadRemaining();
        pullEngine.Close();

        // Assert: one record each, one error at line 2 (start of the unclosed record)
        foreach (var (count, manager) in new[] { (synced.Length, syncEngine.ErrorManager), (asynced.Count, asyncEngine.ErrorManager), (pulled.Length, pullEngine.ErrorManager) })
        {
            await Assert.That(count).IsEqualTo(1);
            await Assert.That(manager.ErrorCount).IsEqualTo(1);
            await Assert.That(manager.Errors[0].LineNumber).IsEqualTo(2);
            await Assert.That(manager.Errors[0].ExceptionInfo).IsTypeOf<BadUsageException>();
        }
    }

    // ---- E9: line numbers ------------------------------------------------------------------

    [Test]
    [DisplayName("E9: ReadFile(path, callback) reports physical line numbers for consecutive errors")]
    public async Task CallbackPath_ErrorLineNumbers_ArePhysical()
    {
        // Arrange: two consecutive bad lines
        var engine = new FileHelperEngine<RcRecord> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: streaming callback read
        await WithTempFile("1,a\nbad\nworse\n4,d", path => Task.FromResult(engine.ReadFile(path, _ => { })));

        // Assert: errors at lines 2 and 3
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.Errors[1].LineNumber).IsEqualTo(3);
    }

    [Test]
    [DisplayName("E9: a failing multiline record is reported at its first physical line on every path")]
    public async Task MultilineError_ReportsStartLine()
    {
        // Arrange: record starting at line 3 spans lines 3-4 and has a bad Qty
        const string input = "1,\"a\",1\n2,\"b\",2\n3,\"c\nd\",oops\n4,\"e\",4";
        var batch = new FileHelperEngine<RcMultiline> { ErrorMode = ErrorMode.SaveAndContinue };
        var pull = new FileHelperAsyncEngine<RcMultiline> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: batch and pull-model reads
        batch.ReadString(input);
        pull.BeginReadString(input);
        pull.ReadRemaining();
        pull.Close();

        // Assert: line 3 on both
        await Assert.That(batch.ErrorManager.Errors[0].LineNumber).IsEqualTo(3);
        await Assert.That(pull.ErrorManager.Errors[0].LineNumber).IsEqualTo(3);
    }

    // ---- E10: per-read state reset ---------------------------------------------------------

    [Test]
    [DisplayName("E10: ReadSync resets errors and captures HeaderText on each read")]
    public async Task ReadSync_ResetsStatePerRead()
    {
        // Arrange: SaveAndContinue engine and two inputs with one bad record each
        var engine = new FileHelperEngine<RcFramed> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: two consecutive streaming reads
        _ = engine.ReadSync("H1\n1,a\nbad\nF1\nF2").ToArray();
        _ = engine.ReadSync("H2\nbad\n2,b\nF1\nF2").ToArray();

        // Assert: only the second read's state remains
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
        await Assert.That(engine.HeaderText).IsEqualTo("H2");
        await Assert.That(engine.TotalRecords).IsEqualTo(1);
    }

    // ---- E11: RecycleRecords never affects array reads -------------------------------------

    [Test]
    [DisplayName("E11: RecycleRecords does not make array-returning reads share one instance")]
    public async Task RecycleRecords_ArrayRead_DistinctInstances()
    {
        // Arrange: recycling enabled
        var engine = new FileHelperEngine<RcRecyclable> { RecycleRecords = true };

        // Act: array read and callback read
        var records = engine.ReadString("1,A\n2,B\n3,C");
        var seen = new List<RcRecyclable>();
        engine.ReadString("1,A\n2,B", seen.Add);

        // Assert: array has three distinct records; callback reuses one instance
        await Assert.That(string.Join("|", records.Select(r => r.Name))).IsEqualTo("A|B|C");
        await Assert.That(ReferenceEquals(records[0], records[1])).IsFalse();
        await Assert.That(ReferenceEquals(seen[0], seen[1])).IsTrue();
    }
}
