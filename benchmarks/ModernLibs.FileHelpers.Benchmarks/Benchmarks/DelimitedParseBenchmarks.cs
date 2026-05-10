// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
using Sylvan.Data.Csv;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

[DelimitedRecord(",")]
public sealed class PackageAsset
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
public class DelimitedParseBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<PackageAsset> _engine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(DelimitedParseBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsurePackageAssets(dataDir, Rows);
        _engine = new FileHelperEngine<PackageAsset>();
    }

    [Benchmark(Baseline = true)]
    public PackageAsset[] ReadAllRows_FileHelpers() => _engine.ReadFile(_filePath);

    [Benchmark]
    public List<PackageAssetRow> ReadAllRows_Sylvan()
    {
        var results = new List<PackageAssetRow>();
        using var reader = CsvDataReader.Create(_filePath);
        while (reader.Read())
        {
            results.Add(new PackageAssetRow
            {
                Id = reader.GetInt32(0),
                Version = reader.GetString(1),
                Authors = reader.GetString(2),
                Description = reader.GetString(3),
                Tags = reader.GetString(4),
                DownloadCount = reader.GetInt32(5),
            });
        }
        return results;
    }

    [Benchmark]
    public List<PackageAssetRow> ReadAllRows_CsvHelper()
    {
        using var reader = new System.IO.StreamReader(_filePath);
        using var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);
        return csv.GetRecords<PackageAssetRow>().ToList();
    }

    [Benchmark]
    public int[] ReadFirstColumn_FileHelpers()
    {
        var records = _engine.ReadFile(_filePath);
        var ids = new int[records.Length];
        for (int i = 0; i < records.Length; i++)
            ids[i] = records[i].Id;
        return ids;
    }

    [Benchmark]
    public int[] ReadFirstColumn_Sylvan()
    {
        var ids = new List<int>();
        using var reader = CsvDataReader.Create(_filePath);
        while (reader.Read())
            ids.Add(reader.GetInt32(0));
        return ids.ToArray();
    }
}

// Lightweight row type for competitor benchmarks (only first 6 fields)
public sealed class PackageAssetRow
{
    public int Id { get; set; }
    public string Version { get; set; } = "";
    public string Authors { get; set; } = "";
    public string Description { get; set; } = "";
    public string Tags { get; set; } = "";
    public int DownloadCount { get; set; }
}
