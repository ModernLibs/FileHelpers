// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// <see cref="FileHelperAsyncEngine{T}"/> session rules: string writes keep the footer, and operations that
/// would reset the state of an open session are rejected.
/// </summary>
[Category("AsyncEngine")]
public sealed class AsyncEngineSessionTests
{
    private static string TempFileWith(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), "fh-session-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, content);
        return path;
    }

    [Test]
    [DisplayName("EndWriteString returns the footer written on close")]
    public async Task EndWriteString_IncludesFooter()
    {
        // Arrange: header and footer on a string write session
        using var engine = new FileHelperAsyncEngine<RcRecord> { HeaderText = "HH", FooterText = "FF" };
        engine.BeginWriteString();
        engine.WriteNext(new RcRecord { Id = 1, Name = "x" });

        // Act: end the session
        string result = engine.EndWriteString();

        // Assert: header, record and footer are all present
        await Assert.That(result).IsEqualTo("HH\r\n1,x\r\nFF\r\n");
    }

    [Test]
    [DisplayName("ReadAsync while a BeginRead session is open throws and leaves the session intact")]
    public async Task ReadAsync_WhileBeginReadOpen_Throws()
    {
        // Arrange: open pull session with one record consumed
        string path = TempFileWith("9,z\r\n");
        try
        {
            using var engine = new FileHelperAsyncEngine<RcRecord>();
            engine.BeginReadString("1,a\r\n2,b\r\n");
            _ = engine.ReadNext();

            // Act: start a streaming read on the same engine
            async Task Act()
            {
                await foreach (var _ in engine.ReadAsync(path))
                {
                }
            }

            // Assert: rejected; the open session continues where it was
            await Assert.That(Act).Throws<InvalidOperationException>();
            await Assert.That(engine.LineNumber).IsEqualTo(1);
            await Assert.That(engine.ReadNext()!.Id).IsEqualTo(2);
            await Assert.That(engine.TotalRecords).IsEqualTo(2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [DisplayName("BeginRead while a ReadAsync enumeration is running throws")]
    public async Task BeginRead_WhileReadAsyncRunning_Throws()
    {
        // Arrange: streaming read paused after the first record
        string path = TempFileWith("1,a\r\n2,b\r\n");
        try
        {
            using var engine = new FileHelperAsyncEngine<RcRecord>();
            var ids = new List<int>();
            Exception? caught = null;
            await foreach (var r in engine.ReadAsync(path))
            {
                ids.Add(r.Id);
                if (ids.Count == 1)
                {
                    // Act: open a pull session mid-enumeration
                    try
                    {
                        engine.BeginReadString("9,z");
                    }
                    catch (InvalidOperationException ex)
                    {
                        caught = ex;
                    }
                }
            }

            // Assert: rejected; the enumeration completes normally
            await Assert.That(caught).IsNotNull();
            await Assert.That(string.Join(",", ids)).IsEqualTo("1,2");
            await Assert.That(engine.TotalRecords).IsEqualTo(2);

            // And after it completes the engine is usable again
            engine.BeginReadString("9,z");
            await Assert.That(engine.ReadNext()!.Id).IsEqualTo(9);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [DisplayName("Reading while a write session is open, and writing while reading, throw (3.x)")]
    public async Task ReadAndWriteSessions_AreExclusive()
    {
        // Arrange: one engine with a write session, one with a read session
        using var writing = new FileHelperAsyncEngine<RcRecord>();
        writing.BeginWriteString();
        using var reading = new FileHelperAsyncEngine<RcRecord>();
        reading.BeginReadString("1,a");

        // Act + Assert: the other kind of session cannot be opened
        await Assert.That(() => writing.BeginReadString("1,a")).Throws<InvalidOperationException>();
        await Assert.That(() => reading.BeginWriteString()).Throws<InvalidOperationException>();
        await Assert.That(reading.ReadNext()!.Id).IsEqualTo(1);
        writing.WriteNext(new RcRecord { Id = 2, Name = "b" });
        await Assert.That(writing.EndWriteString()).IsEqualTo("2,b\r\n");
    }

    [Test]
    [DisplayName("A failing record with [IgnoreLast] throws once; ReadNext then continues with the next record")]
    public async Task ReadNext_WithFooter_ErrorDoesNotRepeat()
    {
        // Arrange: footer of two lines, a bad record in the middle, default ThrowException mode
        using var engine = new FileHelperAsyncEngine<RcFramed>();
        engine.BeginReadString("H\n1,a\nx,b\n3,c\n4,d\nF1\nF2");
        var got = new List<string>();

        // Act: pull until the end, catching the failure
        for (int i = 0; i < 6; i++)
        {
            try
            {
                got.Add(engine.ReadNext()?.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "end");
            }
            catch (FileHelpersException)
            {
                got.Add("error");
            }
        }

        // Assert: the bad line fails once, later records and the footer are intact
        await Assert.That(string.Join(",", got)).IsEqualTo("1,error,3,4,end,end");
        await Assert.That(engine.FooterText).IsEqualTo("F1\r\nF2");
    }

    [Test]
    [DisplayName("Close() inside AfterReadRecord with [IgnoreLast] keeps the record and ends the session")]
    public async Task Close_InAfterReadRecord_WithFooter_KeepsRecord()
    {
        // Arrange: the handler closes the engine on the second record
        using var engine = new FileHelperAsyncEngine<RcFramed>();
        engine.AfterReadRecord += (_, e) =>
        {
            if (e.Record.Id == 2)
                engine.Close();
        };
        engine.BeginReadString("H\n1,a\n2,b\n3,c\nF1\nF2");

        // Act
        var first = engine.ReadNext();
        var second = engine.ReadNext();

        // Assert: both records are returned, then the session is closed
        await Assert.That(first!.Id).IsEqualTo(1);
        await Assert.That(second!.Id).IsEqualTo(2);
        await Assert.That(() => engine.ReadNext()).Throws<InvalidOperationException>();
    }

    [Test]
    [DisplayName("An abandoned ReadAsync enumerator does not block the engine after Close() or Dispose()")]
    public async Task AbandonedReadAsync_CloseFreesEngine()
    {
        // Arrange: a streaming read started and left without DisposeAsync
        string path = TempFileWith("1,a\r\n2,b\r\n");
        try
        {
            var engine = new FileHelperAsyncEngine<RcRecord>();
            var abandoned = engine.ReadAsync(path).GetAsyncEnumerator();
            await Assert.That(await abandoned.MoveNextAsync()).IsTrue();

            // Act: close, then dispose (reuse after disposal is supported)
            engine.Close();
            engine.BeginReadString("9,z");
            var afterClose = engine.ReadNext();
            engine.Dispose();
            engine.BeginReadString("8,y");
            var afterDispose = engine.ReadNext();

            // Assert: new sessions work and the abandoned enumeration has ended
            await Assert.That(afterClose!.Id).IsEqualTo(9);
            await Assert.That(afterDispose!.Id).IsEqualTo(8);
            engine.Close();
            await Assert.That(await abandoned.MoveNextAsync()).IsFalse();
            await abandoned.DisposeAsync();
            await Assert.That(engine.TotalRecords).IsEqualTo(1);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
