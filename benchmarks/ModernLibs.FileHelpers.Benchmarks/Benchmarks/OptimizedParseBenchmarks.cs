// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

[DelimitedRecord(",")]
public sealed class BenchmarkAsset
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Version { get; set; } = "";
    [FieldOrder(3)] public string Authors { get; set; } = "";
    [FieldOrder(4)] public string Description { get; set; } = "";
    [FieldOrder(5)] public string Tags { get; set; } = "";
    [FieldOrder(6)] public int DownloadCount { get; set; }
    [FieldOrder(7)] public string ProjectUrl { get; set; } = "";
    [FieldOrder(8)] public string LicenseUrl { get; set; } = "";
    [FieldOrder(9)] public string IconUrl { get; set; } = "";
    [FieldOrder(10)] public string Language { get; set; } = "";
    [FieldOrder(11)] public string Framework { get; set; } = "";
    [FieldOrder(12)] public string MinClientVersion { get; set; } = "";
    [FieldOrder(13)] public int Type { get; set; }
    [FieldOrder(14)] public bool IsPrerelease { get; set; }
    [FieldOrder(15)] public DateTime Published { get; set; }
    [FieldOrder(16)] public DateTime LastUpdated { get; set; }
    [FieldOrder(17)] public long PackageSize { get; set; }
    [FieldOrder(18)] public int DependencyCount { get; set; }
    [FieldOrder(19)] public bool HasReadme { get; set; }
    [FieldOrder(20)] public bool HasLicense { get; set; }
    [FieldOrder(21)] public bool HasIcon { get; set; }
    [FieldOrder(22)] public bool IsSigned { get; set; }
    [FieldOrder(23)] public string RepositoryType { get; set; } = "";
    [FieldOrder(24)] public string RepositoryUrl { get; set; } = "";
    [FieldOrder(25)] public bool RequireLicenseAcceptance { get; set; }
}

[MemoryDiagnoser]
public class OptimizedParseBenchmarks
{
    private string _filePath = "";
    private string _content = "";
    private char[] _contentBuffer = [];
    private FileHelperEngine<BenchmarkAsset> _engine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(OptimizedParseBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsurePackageAssets(dataDir, Rows);
        _engine = new FileHelperEngine<BenchmarkAsset>();
        _content = File.ReadAllText(_filePath);
        _contentBuffer = _content.ToCharArray();
    }

    // Baseline: ReadFile (string path, existing API)
    [Benchmark(Baseline = true)]
    public BenchmarkAsset[] ReadFile() => _engine.ReadFile(_filePath);

    // ReadString (string content, existing API)
    [Benchmark]
    public BenchmarkAsset[] ReadString() => _engine.ReadString(_content);

    // ReadSpan with ReadOnlySpan<char> (OPT-06/07/08: span-based, pre-allocated int[], SIMD line scanning)
    [Benchmark]
    public BenchmarkAsset[] ReadSpan() => _engine.ReadSpan(_content.AsSpan());

    // ReadSpan with char[] (OPT-06: caller already has char[] buffer)
    [Benchmark]
    public BenchmarkAsset[] ReadSpan_CharArray() => _engine.ReadSpan(_contentBuffer.AsSpan());

    // OPT-11: Callback-based read — zero result array allocation
    [Benchmark]
    public int ReadString_Callback()
    {
        int count = 0;
        _engine.ReadString(_content, _ => count++);
        return count;
    }

    // OPT-11: Callback-based span read — zero result array allocation
    [Benchmark]
    public int ReadSpan_Callback()
    {
        int count = 0;
        _engine.ReadSpan(_content.AsSpan(), _ => count++);
        return count;
    }

    // OPT-11: Callback-based with processing (simulates real-world use)
    [Benchmark]
    public int ReadSpan_Callback_ProcessAndDiscard()
    {
        int count = 0;
        _engine.ReadSpan(_content.AsSpan(), r =>
        {
            _ = r.Id + r.DownloadCount;
            count++;
        });
        return count;
    }

    // OPT-12: ReadFile with callback — zero string + zero T[] allocation, streaming from disk
    [Benchmark]
    public int ReadFile_Callback()
    {
        int count = 0;
        _engine.ReadFile(_filePath, _ => count++);
        return count;
    }

    // OPT-12: ReadFile callback with processing (simulates real-world ETL)
    [Benchmark]
    public int ReadFile_Callback_ProcessAndDiscard()
    {
        int count = 0;
        _engine.ReadFile(_filePath, r =>
        {
            _ = r.Id + r.DownloadCount;
            count++;
        });
        return count;
    }

    // OPT-12: ReadFileAsync callback — async streaming from disk
    [Benchmark]
    public int ReadFileAsync_Callback()
    {
        int count = 0;
        _engine.ReadFileAsync(_filePath, _ => count++).GetAwaiter().GetResult();
        return count;
    }

    // OPT-12: ReadFileAsync callback with processing
    [Benchmark]
    public int ReadFileAsync_Callback_ProcessAndDiscard()
    {
        int count = 0;
        _engine.ReadFileAsync(_filePath, r =>
        {
            _ = r.Id + r.DownloadCount;
            count++;
        }).GetAwaiter().GetResult();
        return count;
    }
}
