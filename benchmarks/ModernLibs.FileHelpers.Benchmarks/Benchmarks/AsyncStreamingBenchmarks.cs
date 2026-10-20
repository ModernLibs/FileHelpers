// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using System.Globalization;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

[DelimitedRecord("|")]
public record struct AsyncHeaderRecord(
    [property: FieldOrder(1)] string Kind,
    [property: FieldOrder(2)] string Batch
);

[DelimitedRecord("|")]
public record struct AsyncDetailRecord(
    [property: FieldOrder(1)] string Kind,
    [property: FieldOrder(2)] int Id,
    [property: FieldOrder(3)] decimal Amount
);

[MemoryDiagnoser]
public class AsyncStreamingBenchmarks : IAsyncDisposable
{
    private string _multiRecordPath = "";
    private MultiRecordAsyncEngine _multiEngine = null!;
    private MultiRecordEngine _multiSyncEngine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(AsyncStreamingBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);

        _multiEngine = new MultiRecordAsyncEngine(
            MultiRecordType.For<AsyncHeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<AsyncDetailRecord>(line => line.StartsWith("D|", StringComparison.Ordinal)));

        _multiSyncEngine = new MultiRecordEngine(
            MultiRecordType.For<AsyncHeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<AsyncDetailRecord>(line => line.StartsWith("D|", StringComparison.Ordinal)));

        // Generate multi-record test file
        _multiRecordPath = Path.Combine(dataDir, $"multi_record_{Rows}.txt");
        if (!File.Exists(_multiRecordPath))
        {
            using var writer = new StreamWriter(_multiRecordPath);
            for (int i = 0; i < Rows; i++)
            {
                writer.WriteLine($"H|BATCH{i}");
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"D|{i}|{i * 1.5m}"));
            }
        }

        // Correctness guard: every benchmark must read every record of the file it measures.
        var records = ReadSync_MultiRecord();
        DataCheck.Equal(2 * Rows, records.Length, "sync record count");
        DataCheck.That(records[0] is AsyncHeaderRecord { Kind: "H", Batch: "BATCH0" }, "first record must be header H|BATCH0");
        var last = Rows - 1;
        DataCheck.That(records[^1] is AsyncDetailRecord { Kind: "D" } d && d.Id == last && d.Amount == last * 1.5m,
            $"last record must be detail D|{last}|{last * 1.5m}, got {records[^1]}");
        DataCheck.Equal(2 * Rows, ReadAsync_MultiRecord().GetAwaiter().GetResult(), "async record count");
        DataCheck.Equal(2 * Rows, ReadAsync_MultiRecord_WithCancellation().GetAwaiter().GetResult(), "async (cancellable) record count");
    }

    [GlobalCleanup]
    public Task CleanupAsync() => DisposeAsync().AsTask();

    public async ValueTask DisposeAsync()
    {
        if (_multiEngine is not null)
            await _multiEngine.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public async Task<int> ReadAsync_MultiRecord()
    {
        int count = 0;
        await foreach (var _ in _multiEngine.ReadAsync(_multiRecordPath))
            count++;
        return count;
    }

    [Benchmark]
    public object[] ReadSync_MultiRecord() => _multiSyncEngine.ReadFile(_multiRecordPath);

    [Benchmark]
    public async Task<int> ReadAsync_MultiRecord_WithCancellation()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        int count = 0;
        await foreach (var _ in _multiEngine.ReadAsync(_multiRecordPath, cts.Token))
            count++;
        return count;
    }
}
