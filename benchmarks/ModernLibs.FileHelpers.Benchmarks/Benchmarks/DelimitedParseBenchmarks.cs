// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
using Sylvan.Data.Csv;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

// The PackageAssets file starts with a header line; dates are ISO 8601 "s" (see DatasetGenerator.DateFormat).
[DelimitedRecord(",")]
[IgnoreFirst]
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
    [FieldOrder(15), FieldConverter(ConverterKind.Date, DatasetGenerator.DateFormat)] public DateTime Published { get; set; }
    [FieldOrder(16), FieldConverter(ConverterKind.Date, DatasetGenerator.DateFormat)] public DateTime LastUpdated { get; set; }
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

// Full 25-field row for fair competitor comparison
public sealed class PackageAssetRow
{
    public int Id { get; set; }
    public string Version { get; set; } = "";
    public string Authors { get; set; } = "";
    public string Description { get; set; } = "";
    public string Tags { get; set; } = "";
    public int DownloadCount { get; set; }
    public string ProjectUrl { get; set; } = "";
    public string LicenseUrl { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string Language { get; set; } = "";
    public string Framework { get; set; } = "";
    public string MinClientVersion { get; set; } = "";
    public int Type { get; set; }
    public bool IsPrerelease { get; set; }
    public DateTime Published { get; set; }
    public DateTime LastUpdated { get; set; }
    public long PackageSize { get; set; }
    public int DependencyCount { get; set; }
    public bool HasReadme { get; set; }
    public bool HasLicense { get; set; }
    public bool HasIcon { get; set; }
    public bool IsSigned { get; set; }
    public string RepositoryType { get; set; } = "";
    public string RepositoryUrl { get; set; } = "";
    public bool RequireLicenseAcceptance { get; set; }
}

/// <summary>Projections of the PackageAssets row types for <see cref="DataCheck.PackageAssets"/>.</summary>
internal static class PackageAssetColumns
{
    public static string?[] Of(PackageAsset r) => DataCheck.PackageColumns(
        r.Id, r.Version, r.DownloadCount, r.MinClientVersion, r.IsPrerelease,
        r.Published, r.LastUpdated, r.PackageSize, r.RepositoryType, r.RequireLicenseAcceptance);

    public static string?[] Of(PackageAssetRow r) => DataCheck.PackageColumns(
        r.Id, r.Version, r.DownloadCount, r.MinClientVersion, r.IsPrerelease,
        r.Published, r.LastUpdated, r.PackageSize, r.RepositoryType, r.RequireLicenseAcceptance);
}

[MemoryDiagnoser]
public class DelimitedParseBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<PackageAsset> _engine = null!;

    [Params(10_000, 100_000, 1_000_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(DelimitedParseBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsurePackageAssets(dataDir, Rows);
        _engine = new FileHelperEngine<PackageAsset>();

        // Correctness guard: every benchmark must parse the file it measures.
        var lines = DataCheck.DataLines(_filePath, hasHeader: true);
        var fh = ReadAllRows_FileHelpers();
        DataCheck.PackageAssets("FileHelpers", lines, fh.Length, i => PackageAssetColumns.Of(fh[i]));
        var sylvan = ReadAllRows_Sylvan();
        DataCheck.PackageAssets("Sylvan", lines, sylvan.Count, i => PackageAssetColumns.Of(sylvan[i]));
        var csvHelper = ReadAllRows_CsvHelper();
        DataCheck.PackageAssets("CsvHelper", lines, csvHelper.Count, i => PackageAssetColumns.Of(csvHelper[i]));
        foreach (var (name, ids) in new[] { ("FileHelpers", ReadFirstColumn_FileHelpers()), ("Sylvan", ReadFirstColumn_Sylvan()), ("CsvHelper", ReadFirstColumn_CsvHelper()) })
        {
            DataCheck.Equal(Rows, ids.Length, name + " first-column count");
            DataCheck.Equal(Rows - 1, ids[^1], name + " last Id");
        }
    }

    [Benchmark(Baseline = true)]
    public PackageAsset[] ReadAllRows_FileHelpers() => _engine.ReadFile(_filePath);

    [Benchmark]
    public IReadOnlyList<PackageAssetRow> ReadAllRows_Sylvan()
    {
        var results = new List<PackageAssetRow>(Rows);
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
                ProjectUrl = reader.GetString(6),
                LicenseUrl = reader.GetString(7),
                IconUrl = reader.GetString(8),
                Language = reader.GetString(9),
                Framework = reader.GetString(10),
                MinClientVersion = reader.GetString(11),
                Type = reader.GetInt32(12),
                IsPrerelease = reader.GetBoolean(13),
                Published = reader.GetDateTime(14),
                LastUpdated = reader.GetDateTime(15),
                PackageSize = reader.GetInt64(16),
                DependencyCount = reader.GetInt32(17),
                HasReadme = reader.GetBoolean(18),
                HasLicense = reader.GetBoolean(19),
                HasIcon = reader.GetBoolean(20),
                IsSigned = reader.GetBoolean(21),
                RepositoryType = reader.GetString(22),
                RepositoryUrl = reader.GetString(23),
                RequireLicenseAcceptance = reader.GetBoolean(24),
            });
        }
        return results;
    }

    [Benchmark]
    public IReadOnlyList<PackageAssetRow> ReadAllRows_CsvHelper()
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
        var ids = new int[Rows];
        int idx = 0;
        using var reader = CsvDataReader.Create(_filePath);
        while (reader.Read())
            ids[idx++] = reader.GetInt32(0);
        return ids;
    }

    [Benchmark]
    public int[] ReadFirstColumn_CsvHelper()
    {
        using var reader = new System.IO.StreamReader(_filePath);
        using var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        var ids = new List<int>(Rows);
        while (csv.Read())
            ids.Add(csv.GetField<int>("Id"));
        return ids.ToArray();
    }
}
