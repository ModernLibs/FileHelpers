// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// E2, E10, E11, E15 on <see cref="FileHelperAsyncEngine{T}"/>: end of input never inferred from a default record,
/// per-read reset, recycling only for streaming pulls, AfterReadRecord on ReadAsync.
/// </summary>
[Category("ReadCore")]
public sealed class ReadCoreAsyncEngineTests
{
    private static async Task<string> TempFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), "fh-readcore-" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    [Test]
    [DisplayName("E2: ReadNext(count) stops at end of input for struct records")]
    public async Task StructRecords_ReadNextCount_StopsAtEof()
    {
        // Arrange: two struct records
        using var engine = new FileHelperAsyncEngine<RcStruct>();
        engine.BeginReadString("1,10\n2,20\n");

        // Act: ask for more records than exist
        var records = engine.ReadNext(10);

        // Assert: exactly two, no default padding
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[1].Value).IsEqualTo(20);
    }

    [Test]
    [DisplayName("E2: foreach and ReadRemaining terminate for struct records, including a 0,0 record")]
    public async Task StructRecords_ForeachAndReadRemaining_Terminate()
    {
        // Arrange: a record equal to default(T) must not be mistaken for end of input
        using var engine = new FileHelperAsyncEngine<RcStruct>();

        // Act: foreach over one session, ReadRemaining over another
        engine.BeginReadString("0,0\n3,30");
        var viaForeach = engine.Take(10).ToList();
        engine.Close();
        engine.BeginReadString("0,0\n3,30");
        var viaRemaining = engine.ReadRemaining();

        // Assert: both see exactly the two records
        await Assert.That(viaForeach.Count).IsEqualTo(2);
        await Assert.That(viaRemaining.Length).IsEqualTo(2);
        await Assert.That(viaRemaining[0]).IsEqualTo(new RcStruct { Id = 0, Value = 0 });
    }

    [Test]
    [DisplayName("E15: FileHelperAsyncEngine.ReadAsync raises BeforeReadRecord and AfterReadRecord")]
    public async Task ReadAsync_RaisesAfterReadRecord()
    {
        // Arrange: count events, skip record 2 in AfterRead
        var path = await TempFile("1,a\n2,b\n3,c");
        using var engine = new FileHelperAsyncEngine<RcRecord>();
        int before = 0;
        int after = 0;
        engine.BeforeReadRecord += (_, _) => before++;
        engine.AfterReadRecord += (_, e) =>
        {
            after++;
            e.SkipThisRecord = e.Record.Id == 2;
        };

        // Act: enumerate
        var ids = new List<int>();
        try
        {
            await foreach (var r in engine.ReadAsync(path))
                ids.Add(r.Id);
        }
        finally
        {
            File.Delete(path);
        }

        // Assert: events fired for every record, skip honoured
        await Assert.That(before).IsEqualTo(3);
        await Assert.That(after).IsEqualTo(3);
        await Assert.That(string.Join(",", ids)).IsEqualTo("1,3");
    }

    [Test]
    [DisplayName("E10: FileHelperAsyncEngine.ReadAsync resets TotalRecords and errors per read")]
    public async Task ReadAsync_ResetsStatePerRead()
    {
        // Arrange: SaveAndContinue, one bad line
        var path = await TempFile("1,a\nbad\n3,c");
        using var engine = new FileHelperAsyncEngine<RcRecord> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: read the same file twice
        try
        {
            await foreach (var _ in engine.ReadAsync(path))
            {
            }

            await foreach (var _ in engine.ReadAsync(path))
            {
            }
        }
        finally
        {
            File.Delete(path);
        }

        // Assert: counters reflect only the last read
        await Assert.That(engine.TotalRecords).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(engine.LineNumber).IsEqualTo(3);
    }

    [Test]
    [DisplayName("E11: RecycleRecords on the async engine never affects ReadRemaining/ReadNext(count) arrays")]
    public async Task RecycleRecords_ArrayPulls_DistinctInstances()
    {
        // Arrange: recycling enabled
        using var engine = new FileHelperAsyncEngine<RcRecyclable> { RecycleRecords = true };

        // Act: array pulls
        engine.BeginReadString("1,A\n2,B\n3,C\n4,D");
        var firstTwo = engine.ReadNext(2);
        var rest = engine.ReadRemaining();

        // Assert: distinct instances with their own values
        await Assert.That(ReferenceEquals(firstTwo[0], firstTwo[1])).IsFalse();
        await Assert.That(ReferenceEquals(rest[0], rest[1])).IsFalse();
        await Assert.That(string.Join("|", firstTwo.Concat(rest).Select(r => r.Name))).IsEqualTo("A|B|C|D");
    }

    [Test]
    [DisplayName("BeginReadString captures HeaderText before the first ReadNext and FooterText at end of input")]
    public async Task HeaderAndFooter_PullModel()
    {
        // Arrange: framed input
        using var engine = new FileHelperAsyncEngine<RcFramed>();

        // Act: open, check header, read all
        engine.BeginReadString("HEAD\n1,a\n2,b\nT1\nT2");
        var header = engine.HeaderText;
        var records = engine.ReadRemaining();

        // Assert: header immediately, footer = raw last two lines
        await Assert.That(header).IsEqualTo("HEAD");
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(engine.FooterText).IsEqualTo("T1\r\nT2");
    }
}
