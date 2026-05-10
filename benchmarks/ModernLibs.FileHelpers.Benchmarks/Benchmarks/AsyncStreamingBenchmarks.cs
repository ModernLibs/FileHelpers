// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

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
public class AsyncStreamingBenchmarks
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
                writer.WriteLine($"D|{i}|{i * 1.5m}");
            }
        }
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
