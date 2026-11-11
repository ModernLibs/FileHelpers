// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
using nietras.SeparatedValues;
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
// CsvHelper 34 maps a [CsvRecord] type with source-generated code (columns by header name).
[CsvHelper.CsvRecord]
public sealed partial class PackageAssetRow
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

    /// <summary>
    /// One PackageAssets row from Sylvan, for the parse-and-discard benchmarks. The ReadAllRows benchmarks keep
    /// their own inline initializer, so their code is the same as in earlier published runs.
    /// </summary>
    public static PackageAssetRow FromSylvan(CsvDataReader reader) => new()
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
    };

    /// <summary>One PackageAssets row from Sep (see <see cref="FromSylvan"/>).</summary>
    public static PackageAssetRow FromSep(SepReader.Row row) => new()
    {
        Id = row[0].Parse<int>(),
        Version = row[1].ToString(),
        Authors = row[2].ToString(),
        Description = row[3].ToString(),
        Tags = row[4].ToString(),
        DownloadCount = row[5].Parse<int>(),
        ProjectUrl = row[6].ToString(),
        LicenseUrl = row[7].ToString(),
        IconUrl = row[8].ToString(),
        Language = row[9].ToString(),
        Framework = row[10].ToString(),
        MinClientVersion = row[11].ToString(),
        Type = row[12].Parse<int>(),
        IsPrerelease = row[13].Parse<bool>(),
        Published = row[14].Parse<DateTime>(),
        LastUpdated = row[15].Parse<DateTime>(),
        PackageSize = row[16].Parse<long>(),
        DependencyCount = row[17].Parse<int>(),
        HasReadme = row[18].Parse<bool>(),
        HasLicense = row[19].Parse<bool>(),
        HasIcon = row[20].Parse<bool>(),
        IsSigned = row[21].Parse<bool>(),
        RepositoryType = row[22].ToString(),
        RepositoryUrl = row[23].ToString(),
        RequireLicenseAcceptance = row[24].Parse<bool>(),
    };
}

/// <summary>
/// Checksums of the parse-and-discard benchmarks (design B2). Each library parses every record completely and
/// keeps none: ModernLibs.FileHelpers through <c>ReadFile(path, callback)</c>, every other library through its
/// own read loop. Each record adds one field to the checksum, the benchmark returns it, and its setup compares
/// it with the value computed here from the dataset.
/// </summary>
internal static class DiscardChecksum
{
    /// <summary>The PackageAssets and quoted-fields datasets number their rows 0 to rows - 1 in the Id column.</summary>
    public static long IdSum(int rows) => (long)rows * (rows - 1) / 2;

    /// <summary>Fixed-width records add the last character of their 8-character Id field.</summary>
    public static long FixedIdLastChars(string[] dataLines)
    {
        long sum = 0;
        foreach (var line in dataLines)
            sum += line[7];
        return sum;
    }
}

[MemoryDiagnoser]
public class DelimitedParseBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<PackageAsset> _engine = null!;
    private RecordParser.Parsers.IVariableLengthReader<PackageAssetRow> _recordParser = null!;

    [Params(10_000, 100_000, 1_000_000)]
    public int Rows { get; set; }

    private void Prepare()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(DelimitedParseBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsurePackageAssets(dataDir, Rows);
        _engine = new FileHelperEngine<PackageAsset>();
        _recordParser = RecordParserReaders.PackageAssets();
    }

    private void CheckFirstColumn(string name, int[] ids)
    {
        DataCheck.Equal(Rows, ids.Length, name + " first-column count");
        DataCheck.Equal(Rows - 1, ids[^1], name + " last Id");
    }

    // Correctness guard: every benchmark must parse the file it measures. Each setup runs only the guard of
    // its own benchmark, so no other reader runs in the measured process (see FixedWidthParseBenchmarks).
    [GlobalSetup(Target = nameof(ReadAllRows_FileHelpers))]
    public void SetupAllRowsFileHelpers()
    {
        Prepare();
        var fh = ReadAllRows_FileHelpers();
        DataCheck.PackageAssets("FileHelpers", DataCheck.DataLines(_filePath, hasHeader: true), fh.Length, i => PackageAssetColumns.Of(fh[i]));
    }

    [GlobalSetup(Target = nameof(ReadAllRows_Sylvan))]
    public void SetupAllRowsSylvan()
    {
        Prepare();
        var sylvan = ReadAllRows_Sylvan();
        DataCheck.PackageAssets("Sylvan", DataCheck.DataLines(_filePath, hasHeader: true), sylvan.Count, i => PackageAssetColumns.Of(sylvan[i]));
    }

    [GlobalSetup(Target = nameof(ReadAllRows_CsvHelper))]
    public void SetupAllRowsCsvHelper()
    {
        Prepare();
        var csvHelper = ReadAllRows_CsvHelper();
        DataCheck.PackageAssets("CsvHelper", DataCheck.DataLines(_filePath, hasHeader: true), csvHelper.Count, i => PackageAssetColumns.Of(csvHelper[i]));
    }

    [GlobalSetup(Target = nameof(ReadFirstColumn_FileHelpers))]
    public void SetupFirstColumnFileHelpers()
    {
        Prepare();
        CheckFirstColumn("FileHelpers", ReadFirstColumn_FileHelpers());
    }

    [GlobalSetup(Target = nameof(ReadFirstColumn_Sylvan))]
    public void SetupFirstColumnSylvan()
    {
        Prepare();
        CheckFirstColumn("Sylvan", ReadFirstColumn_Sylvan());
    }

    [GlobalSetup(Target = nameof(ReadFirstColumn_CsvHelper))]
    public void SetupFirstColumnCsvHelper()
    {
        Prepare();
        CheckFirstColumn("CsvHelper", ReadFirstColumn_CsvHelper());
    }

    [GlobalSetup(Target = nameof(FileHelpers_ParseAndDiscard))]
    public void SetupDiscardFileHelpers()
    {
        Prepare();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), FileHelpers_ParseAndDiscard(), "FileHelpers parse-and-discard checksum");
    }

    [GlobalSetup(Target = nameof(Sylvan_ParseAndDiscard))]
    public void SetupDiscardSylvan()
    {
        Prepare();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), Sylvan_ParseAndDiscard(), "Sylvan parse-and-discard checksum");
    }

    [GlobalSetup(Target = nameof(CsvHelper_ParseAndDiscard))]
    public void SetupDiscardCsvHelper()
    {
        Prepare();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), CsvHelper_ParseAndDiscard(), "CsvHelper parse-and-discard checksum");
    }

    [GlobalSetup(Target = nameof(ReadAllRows_FlameCsv))]
    public void SetupAllRowsFlameCsv()
    {
        Prepare();
        var rows = ReadAllRows_FlameCsv();
        DataCheck.PackageAssets("FlameCsv", DataCheck.DataLines(_filePath, hasHeader: true), rows.Count, i => PackageAssetColumns.Of(rows[i]));
    }

    [GlobalSetup(Target = nameof(ReadAllRows_RecordParser))]
    public void SetupAllRowsRecordParser()
    {
        Prepare();
        RecordParserOptions.AssertSingleThreaded(_filePath, fixedWidth: false);
        var rows = ReadAllRows_RecordParser();
        DataCheck.PackageAssets("RecordParser", DataCheck.DataLines(_filePath, hasHeader: true), rows.Count, i => PackageAssetColumns.Of(rows[i]));
    }

    [GlobalSetup(Target = nameof(FlameCsv_ParseAndDiscard))]
    public void SetupDiscardFlameCsv()
    {
        Prepare();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), FlameCsv_ParseAndDiscard(), "FlameCsv parse-and-discard checksum");
    }

    [GlobalSetup(Target = nameof(RecordParser_ParseAndDiscard))]
    public void SetupDiscardRecordParser()
    {
        Prepare();
        RecordParserOptions.AssertSingleThreaded(_filePath, fixedWidth: false);
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), RecordParser_ParseAndDiscard(), "RecordParser parse-and-discard checksum");
    }

    // Tier A competitors (T11): FlameCsv reads UTF-8 bytes, RecordParser runs on one thread (see BENCHMARKS.md).

    [Benchmark]
    public IReadOnlyList<PackageAssetRow> ReadAllRows_FlameCsv() => CompetitorRuns.FlamePackageAssets(_filePath, Rows);

    [Benchmark]
    public IReadOnlyList<PackageAssetRow> ReadAllRows_RecordParser() => CompetitorRuns.RecordParserPackageAssets(_recordParser, _filePath, Rows);

    [Benchmark]
    public long FlameCsv_ParseAndDiscard() => CompetitorRuns.FlamePackageAssetsDiscard(_filePath);

    [Benchmark]
    public long RecordParser_ParseAndDiscard() => CompetitorRuns.RecordParserPackageAssetsDiscard(_recordParser, _filePath);

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
        using var csv = CsvHelper.CsvReader.FromFile(_filePath, CsvHelper.CsvReaderOptions.Default);
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
        using var csv = CsvHelper.CsvReader.FromFile(_filePath, CsvHelper.CsvReaderOptions.Default);
        csv.ReadHeader();
        int id = csv.GetFieldIndex("Id");
        var ids = new List<int>(Rows);
        while (csv.Read())
            ids.Add(csv.Parse<int>(id));
        return ids.ToArray();
    }

    // Parse-and-discard (design B2): every record parsed completely, none kept.

    [Benchmark]
    public long FileHelpers_ParseAndDiscard()
    {
        long checksum = 0;
        _engine.ReadFile(_filePath, r => checksum += r.Id);
        return checksum;
    }

    [Benchmark]
    public long Sylvan_ParseAndDiscard()
    {
        long checksum = 0;
        using var reader = CsvDataReader.Create(_filePath);
        while (reader.Read())
            checksum += PackageAssetColumns.FromSylvan(reader).Id;
        return checksum;
    }

    [Benchmark]
    public long CsvHelper_ParseAndDiscard()
    {
        long checksum = 0;
        using var csv = CsvHelper.CsvReader.FromFile(_filePath, CsvHelper.CsvReaderOptions.Default);
        foreach (var r in csv.GetRecords<PackageAssetRow>())
            checksum += r.Id;
        return checksum;
    }
}
