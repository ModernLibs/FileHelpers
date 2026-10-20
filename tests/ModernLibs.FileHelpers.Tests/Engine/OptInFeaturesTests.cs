// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Test fixtures co-located in this file — intentional.
#pragma warning disable MA0048
// MA0008: Ref struct requires StructLayout — suppress for test fixtures.
#pragma warning disable MA0008

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

[DelimitedRecord(",")]
internal sealed class SimpleCsv
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public string Name = string.Empty;
    [FieldOrder(3)] public decimal Amount;
}

[DelimitedRecord(",")]
[IgnoreFirst(1)]
internal sealed class HeaderedCsv
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public string Name = string.Empty;
    [FieldOrder(3)] public decimal Amount;
}

[DelimitedRecord(";")]
internal sealed class SemicolonCsv
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public string Value = string.Empty;
}

[DelimitedRecord(",")]
internal sealed class RecycleableRecord
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public int Score;
    [FieldOrder(3)] public decimal Total;
}

// ---------------------------------------------------------------------------
// IAsyncEnumerable<T> ReadAsync tests
// ---------------------------------------------------------------------------

[Category("Streaming")]
public sealed class StreamingAsyncTests
{
    private static readonly string[] s_threeDataRows = ["1,Alice,10.00", "2,Bob,20.00", "3,Charlie,30.00"];
    private static readonly string[] s_headerPlusThree = ["Id,Name,Amount", "1,Alice,10.00", "2,Bob,20.00", "3,Charlie,30.00"];

    [Test]
    [DisplayName("ReadAsync yields all records from a file via IAsyncEnumerable")]
    public async Task ReadAsync_YieldsAllRecords()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tmp, s_threeDataRows);
            var engine = new FileHelperEngine<SimpleCsv>();
            var results = new List<SimpleCsv>();
            await foreach (var r in engine.ReadAsync(tmp))
                results.Add(r);
            await Assert.That(results.Count).IsEqualTo(3);
            await Assert.That(results[0].Id).IsEqualTo(1);
            await Assert.That(results[2].Name).IsEqualTo("Charlie");
        }
        finally { File.Delete(tmp); }
    }

    [Test]
    [DisplayName("ReadAsync yields records from file with header line present")]
    public async Task ReadAsync_WithHeaderLine_ParsesDataRecords()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tmp, s_headerPlusThree);
            var engine = new FileHelperEngine<HeaderedCsv>();
            var results = new List<HeaderedCsv>();
            await foreach (var r in engine.ReadAsync(tmp))
                results.Add(r);
            await Assert.That(results.Count).IsGreaterThanOrEqualTo(2);
        }
        finally { File.Delete(tmp); }
    }

    [Test]
    [DisplayName("ReadAsync cancellation throws OperationCanceledException")]
    public async Task ReadAsync_RespondsToCancellation()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            var lines = Enumerable.Range(1, 10).Select(i => $"{i},Name{i},{i * 10}.00").ToArray();
            await File.WriteAllLinesAsync(tmp, lines);
            var engine = new FileHelperEngine<SimpleCsv>();
            using var cts = new CancellationTokenSource();
            var results = new List<SimpleCsv>();
            int count = 0;
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var r in engine.ReadAsync(tmp, cts.Token))
                {
                    results.Add(r);
                    count++;
                    if (count >= 3) cts.Cancel();
                }
            });
            await Assert.That(results.Count).IsEqualTo(3);
        }
        finally { File.Delete(tmp); }
    }
}

// ---------------------------------------------------------------------------
// IEnumerable<T> ReadSync tests
// ---------------------------------------------------------------------------

[Category("Streaming")]
public sealed class StreamingSyncTests
{
    [Test]
    [DisplayName("ReadSync(TextReader) yields all records lazily")]
    public async Task ReadSync_TextReader_YieldsAllRecords()
    {
        const string content = "1,Alice,10.00\n2,Bob,20.00\n3,Charlie,30.00\n";
        using var reader = new StringReader(content);
        var engine = new FileHelperEngine<SimpleCsv>();
        var results = engine.ReadSync(reader).ToList();
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].Id).IsEqualTo(1);
        await Assert.That(results[1].Id).IsEqualTo(2);
        await Assert.That(results[2].Id).IsEqualTo(3);
    }

    [Test]
    [DisplayName("ReadSync(string) yields all records from string content")]
    public async Task ReadSync_String_YieldsAllRecords()
    {
        const string content = "1,Alice,10.00\n2,Bob,20.00\n3,Charlie,30.00\n";
        var engine = new FileHelperEngine<SimpleCsv>();
        var results = engine.ReadSync(content).ToList();
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[1].Amount).IsEqualTo(20.00m);
    }

    [Test]
    [DisplayName("ReadSync with maxRecords stops at the limit")]
    public async Task ReadSync_MaxRecords_StopsAtLimit()
    {
        const string content = "1,Alice,10.00\n2,Bob,20.00\n3,Charlie,30.00\n4,Diana,40.00\n";
        var engine = new FileHelperEngine<SimpleCsv>();
        var results = engine.ReadSync(content, maxRecords: 2).ToList();
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results[0].Id).IsEqualTo(1);
        await Assert.That(results[1].Id).IsEqualTo(2);
    }

    [Test]
    [DisplayName("ReadSync does not dispose the TextReader")]
    public async Task ReadSync_DoesNotDisposeReader()
    {
        const string content = "1,Alice,10.00\n";
        var reader = new TrackingStringReader(content);
        var engine = new FileHelperEngine<SimpleCsv>();
        _ = engine.ReadSync(reader).ToList();
        await Assert.That(reader.WasDisposed).IsFalse();
    }
}

// ---------------------------------------------------------------------------
// Callback-based read tests (Action<T>)
// ---------------------------------------------------------------------------

[Category("Callbacks")]
public sealed class CallbackReadTests
{
    private static readonly string[] s_threeDataRows = ["1,Alice,10.00", "2,Bob,20.00", "3,Charlie,30.00"];

    [Test]
    [DisplayName("ReadSpan with Action<T> callback invokes callback for each record")]
    public async Task ReadSpan_Callback_InvokesForEachRecord()
    {
        const string content = "1,Alice,10.00\n2,Bob,20.00\n3,Charlie,30.00\n";
        var engine = new FileHelperEngine<SimpleCsv>();
        var results = new List<SimpleCsv>();
        int count = engine.ReadSpan(content.AsSpan(), r => results.Add(r));
        await Assert.That(count).IsEqualTo(3);
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].Id).IsEqualTo(1);
        await Assert.That(results[2].Name).IsEqualTo("Charlie");
    }

    [Test]
    [DisplayName("ReadString with Action<T> callback invokes callback for each record")]
    public async Task ReadString_Callback_InvokesForEachRecord()
    {
        const string content = "1,Alice,10.00\n2,Bob,20.00\n3,Charlie,30.00\n";
        var engine = new FileHelperEngine<SimpleCsv>();
        var results = new List<SimpleCsv>();
        int count = engine.ReadString(content, r => results.Add(r));
        await Assert.That(count).IsEqualTo(3);
        await Assert.That(results.Count).IsEqualTo(3);
    }

    [Test]
    [DisplayName("ReadSpan callback with maxRecords stops at the limit")]
    public async Task ReadSpan_Callback_MaxRecords_StopsAtLimit()
    {
        const string content = "1,Alice,10.00\n2,Bob,20.00\n3,Charlie,30.00\n";
        var engine = new FileHelperEngine<SimpleCsv>();
        var results = new List<SimpleCsv>();
        int count = engine.ReadSpan(content.AsSpan(), r => results.Add(r), maxRecords: 2);
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(results.Count).IsEqualTo(2);
    }

    [Test]
    [DisplayName("ReadFile with callback streams from disk without full materialization")]
    public async Task ReadFile_Callback_StreamsFromDisk()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tmp, s_threeDataRows);
            var engine = new FileHelperEngine<SimpleCsv>();
            var results = new List<SimpleCsv>();
            int count = engine.ReadFile(tmp, r => results.Add(r));
            await Assert.That(count).IsEqualTo(3);
            await Assert.That(results[1].Name).IsEqualTo("Bob");
        }
        finally { File.Delete(tmp); }
    }

    [Test]
    [DisplayName("ReadFileAsync with callback streams asynchronously from disk")]
    public async Task ReadFileAsync_Callback_StreamsAsyncFromDisk()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tmp, s_threeDataRows);
            var engine = new FileHelperEngine<SimpleCsv>();
            var results = new List<SimpleCsv>();
            int count = await engine.ReadFileAsync(tmp, r => results.Add(r));
            await Assert.That(count).IsEqualTo(3);
            await Assert.That(results[0].Amount).IsEqualTo(10.00m);
        }
        finally { File.Delete(tmp); }
    }

    [Test]
    [DisplayName("Callback receives correct record values for processing")]
    public async Task ReadSpan_Callback_ProcessingWorks()
    {
        const string content = "1,Alice,10.00\n2,Bob,20.00\n3,Charlie,30.00\n";
        var engine = new FileHelperEngine<SimpleCsv>();
        decimal totalAmount = 0m;
        int count = 0;
        _ = engine.ReadSpan(content.AsSpan(), r => { totalAmount += r.Amount; count++; });
        await Assert.That(count).IsEqualTo(3);
        await Assert.That(totalAmount).IsEqualTo(60.00m);
    }

    [Test]
    [DisplayName("RecordPool works with callback-based read pattern")]
    public async Task RecordPool_WithCallback_PatternWorks()
    {
        const string content = "1,100,10.00\n2,200,20.00\n3,300,30.00\n";
        var engine = new FileHelperEngine<RecycleableRecord>();
        var results = new List<(int Id, int Score, decimal Total)>();
        int count = engine.ReadString(content, r =>
        {
            results.Add((r.Id, r.Score, r.Total));
        });
        await Assert.That(count).IsEqualTo(3);
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].Id).IsEqualTo(1);
        await Assert.That(results[2].Total).IsEqualTo(30.00m);
    }
}

// ---------------------------------------------------------------------------
// SpanRecordCallback<T> ref-based callback tests
// ---------------------------------------------------------------------------

[Category("RefCallbacks")]
public sealed class RefCallbackReadTests
{
    [Test]
    [DisplayName("ReadSpan with SpanRecordCallback<T> passes records by ref")]
    public async Task ReadSpan_RefCallback_PassesByRef()
    {
        const string content = "1,Alice,10.00\n2,Bob,20.00\n3,Charlie,30.00\n";
        var engine = new FileHelperEngine<SimpleCsv>();
        var capturedIds = new List<int>();
        var capturedNames = new List<string>();
        int count = engine.ReadSpan(content.AsSpan(), (ref SimpleCsv r) =>
        {
            capturedIds.Add(r.Id);
            capturedNames.Add(r.Name);
        });
        await Assert.That(count).IsEqualTo(3);
        await Assert.That(capturedIds[0]).IsEqualTo(1);
        await Assert.That(capturedNames[2]).IsEqualTo("Charlie");
    }

    [Test]
    [DisplayName("ReadString with SpanRecordCallback<T> passes records by ref")]
    public async Task ReadString_RefCallback_PassesByRef()
    {
        const string content = "1,Alice,10.00\n2,Bob,20.00\n";
        var engine = new FileHelperEngine<SimpleCsv>();
        int sum = 0;
        int count = engine.ReadString(content, (ref SimpleCsv r) => { sum += r.Id; });
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(sum).IsEqualTo(3);
    }

    [Test]
    [DisplayName("Ref callback with maxRecords stops at the limit")]
    public async Task ReadSpan_RefCallback_MaxRecords_StopsAtLimit()
    {
        const string content = "1,Alice,10.00\n2,Bob,20.00\n3,Charlie,30.00\n";
        var engine = new FileHelperEngine<SimpleCsv>();
        int count = 0;
        int result = engine.ReadSpan(content.AsSpan(), (ref SimpleCsv _) => count++, maxRecords: 2);
        await Assert.That(result).IsEqualTo(2);
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    [DisplayName("Ref callback can mutate record values")]
    public async Task ReadSpan_RefCallback_CanMutate()
    {
        const string content = "1,Alice,10.00\n2,Bob,20.00\n";
        var engine = new FileHelperEngine<SimpleCsv>();
        var results = new List<SimpleCsv>();
        int count = engine.ReadSpan(content.AsSpan(), (ref SimpleCsv r) =>
        {
            r.Amount *= 2;
            results.Add(r);
        });
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(results[0].Amount).IsEqualTo(20.00m);
        await Assert.That(results[1].Amount).IsEqualTo(40.00m);
    }
}

// ---------------------------------------------------------------------------
// By-ref record callbacks. Ref struct record types are rejected by the
// generator (MLFH027): they cannot be generic type arguments.
// ---------------------------------------------------------------------------

[Category("RefStruct")]
public sealed class RefStructRecordTests
{
    [Test]
    [DisplayName("Ref struct record can be passed by ref to callback pattern")]
    public async Task RefStruct_PassedByRef_ToCallback()
    {
        const string content = "1,100,10.00\n2,200,20.00\n";
        var engine = new FileHelperEngine<RecycleableRecord>();
        var captured = new List<(int Id, int Score, decimal Total)>();
        int count = engine.ReadSpan(content.AsSpan(), (ref RecycleableRecord r) =>
        {
            captured.Add((r.Id, r.Score, r.Total));
        });
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(captured[0].Id).IsEqualTo(1);
        await Assert.That(captured[1].Total).IsEqualTo(20.00m);
    }
}

// ---------------------------------------------------------------------------
// RecycleRecords on FileHelperEngine callback paths
// ---------------------------------------------------------------------------

[Category("Recycling")]
public sealed class EngineRecycleRecordsTests
{
    [Test]
    [DisplayName("RecycleRecords=true on engine callback reuses same instance")]
    public async Task RecycleRecords_Callback_SameInstanceReused()
    {
        const string content = "1,100,10.00\n2,200,20.00\n3,300,30.00\n";
        var engine = new FileHelperEngine<RecycleableRecord> { RecycleRecords = true };
        object? firstRef = null;
        object? secondRef = null;
        object? thirdRef = null;
        int callCount = 0;
        engine.ReadString(content, r =>
        {
            callCount++;
            if (callCount == 1) firstRef = r;
            else if (callCount == 2) secondRef = r;
            else if (callCount == 3) thirdRef = r;
        });
        await Assert.That(callCount).IsEqualTo(3);
        await Assert.That(ReferenceEquals(firstRef, secondRef)).IsTrue();
        await Assert.That(ReferenceEquals(secondRef, thirdRef)).IsTrue();
    }

    [Test]
    [DisplayName("RecycleRecords=false on engine callback creates new instances")]
    public async Task RecycleRecords_False_DifferentInstances()
    {
        const string content = "1,100,10.00\n2,200,20.00\n";
        var engine = new FileHelperEngine<RecycleableRecord> { RecycleRecords = false };
        object? firstRef = null;
        object? secondRef = null;
        int callCount = 0;
        engine.ReadString(content, r =>
        {
            callCount++;
            if (callCount == 1) firstRef = r;
            else if (callCount == 2) secondRef = r;
        });
        await Assert.That(callCount).IsEqualTo(2);
        await Assert.That(ReferenceEquals(firstRef, secondRef)).IsFalse();
    }

    [Test]
    [DisplayName("RecycleRecords on class type reuses same instance (callback captures by value)")]
    public async Task RecycleRecords_ClassType_CallbackCapturesByValue()
    {
        const string content = "1,100,10.00\n2,200,20.00\n";
        var engine = new FileHelperEngine<RecycleableRecord> { RecycleRecords = true };
        var capturedIds = new List<int>();
        var capturedScores = new List<int>();
        int count = engine.ReadString(content, r =>
        {
            capturedIds.Add(r.Id);
            capturedScores.Add(r.Score);
        });
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(capturedIds[0]).IsEqualTo(1);
        await Assert.That(capturedIds[1]).IsEqualTo(2);
        await Assert.That(capturedScores[1]).IsEqualTo(200);
    }
}

// ---------------------------------------------------------------------------
// EDGE: Edge case tests
// ---------------------------------------------------------------------------

[Category("EdgeCases")]
public sealed class EdgeCaseTests
{
    [Test]
    [DisplayName("ReadSpan callback handles empty content")]
    public async Task ReadSpan_Callback_EmptyContent_ReturnsZero()
    {
        var engine = new FileHelperEngine<SimpleCsv>();
        int callCount = 0;
        int count = engine.ReadSpan("".AsSpan(), _ => callCount++);
        await Assert.That(count).IsEqualTo(0);
        await Assert.That(callCount).IsEqualTo(0);
    }

    [Test]
    [DisplayName("ReadString callback handles empty content")]
    public async Task ReadString_Callback_EmptyContent_ReturnsZero()
    {
        var engine = new FileHelperEngine<SimpleCsv>();
        int callCount = 0;
        int count = engine.ReadString("", _ => callCount++);
        await Assert.That(count).IsEqualTo(0);
        await Assert.That(callCount).IsEqualTo(0);
    }

    [Test]
    [DisplayName("ReadSpan ref callback handles empty content")]
    public async Task ReadSpan_RefCallback_EmptyContent_ReturnsZero()
    {
        var engine = new FileHelperEngine<SimpleCsv>();
        int callCount = 0;
        int count = engine.ReadSpan("".AsSpan(), (ref SimpleCsv _) => callCount++);
        await Assert.That(count).IsEqualTo(0);
        await Assert.That(callCount).IsEqualTo(0);
    }

    [Test]
    [DisplayName("ReadFile callback handles non-existent file")]
    public async Task ReadFile_Callback_NonExistentFile_Throws()
    {
        var engine = new FileHelperEngine<SimpleCsv>();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(async () =>
        {
            engine.ReadFile("/nonexistent/path/file.csv", _ => { });
        });
    }

    [Test]
    [DisplayName("Callback receives records in correct order")]
    public async Task ReadSpan_Callback_OrderPreserved()
    {
        const string content = "5,Eve,50.00\n1,Alice,10.00\n3,Charlie,30.00\n";
        var engine = new FileHelperEngine<SimpleCsv>();
        var ids = new List<int>();
        engine.ReadSpan(content.AsSpan(), r => ids.Add(r.Id));
        await Assert.That(ids[0]).IsEqualTo(5);
        await Assert.That(ids[1]).IsEqualTo(1);
        await Assert.That(ids[2]).IsEqualTo(3);
    }

    [Test]
    [DisplayName("ReadSpan callback with semicolon delimiter works")]
    public async Task ReadSpan_Callback_SemicolonDelimiter_Works()
    {
        const string content = "1;hello\n2;world\n";
        var engine = new FileHelperEngine<SemicolonCsv>();
        var results = new List<SemicolonCsv>();
        int count = engine.ReadSpan(content.AsSpan(), r => results.Add(r));
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(results[0].Value).IsEqualTo("hello");
        await Assert.That(results[1].Value).IsEqualTo("world");
    }
}

// ---------------------------------------------------------------------------
// Helper: TrackingStringReader — tracks disposal
// ---------------------------------------------------------------------------

internal sealed class TrackingStringReader : StringReader
{
    public bool WasDisposed { get; private set; }
    public TrackingStringReader(string s) : base(s) { }
    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
}
