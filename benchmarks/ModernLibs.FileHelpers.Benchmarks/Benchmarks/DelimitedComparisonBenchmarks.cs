// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
// The FileHelpers 3.x NuGet package. It must be named through this alias: inside the
// ModernLibs.FileHelpers.* namespaces a bare "FileHelpers." resolves to ModernLibs.FileHelpers, which
// made the "Legacy" benchmarks of earlier milestones measure this library instead of 3.x.
using Legacy = global::FileHelpers;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

// FileHelpers 3.x record type, configured like PackageAsset: header line skipped, ISO 8601 "s" dates.
[Legacy.DelimitedRecord(",")]
[Legacy.IgnoreFirst(1)]
public sealed class OldPackageAsset
{
    public int Id;
    public string Version = "";
    public string Authors = "";
    public string Description = "";
    public string Tags = "";
    public int DownloadCount;
    public string ProjectUrl = "";
    public string LicenseUrl = "";
    public string IconUrl = "";
    public string Language = "";
    public string Framework = "";
    public string MinClientVersion = "";
    public int Type;
    public bool IsPrerelease;
    [Legacy.FieldConverter(Legacy.ConverterKind.Date, DatasetGenerator.DateFormat)]
    public DateTime Published;
    [Legacy.FieldConverter(Legacy.ConverterKind.Date, DatasetGenerator.DateFormat)]
    public DateTime LastUpdated;
    public long PackageSize;
    public int DependencyCount;
    public bool HasReadme;
    public bool HasLicense;
    public bool HasIcon;
    public bool IsSigned;
    public string RepositoryType = "";
    public string RepositoryUrl = "";
    public bool RequireLicenseAcceptance;
}

[MemoryDiagnoser]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "The 3.x async engine is closed after every operation and disposed by the GlobalCleanup of its benchmark.")]
public class DelimitedComparisonBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<PackageAsset> _modernEngine = null!;
    private Legacy.FileHelperEngine<OldPackageAsset> _oldEngine = null!;
    private Legacy.FileHelperAsyncEngine<OldPackageAsset> _oldAsyncEngine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    private void Prepare()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(DelimitedComparisonBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsurePackageAssets(dataDir, Rows);
        _modernEngine = new FileHelperEngine<PackageAsset>();
        _oldEngine = new Legacy.FileHelperEngine<OldPackageAsset>();
    }

    // Correctness guard: both libraries must parse the file they are measured on. Each setup runs only the
    // guard of its own benchmark, so no other reader runs in the measured process (see FixedWidthParseBenchmarks).
    [GlobalSetup(Target = nameof(ReadAllRows_Modern))]
    public void SetupModern()
    {
        Prepare();
        var modern = ReadAllRows_Modern();
        DataCheck.PackageAssets("ModernLibs.FileHelpers", DataCheck.DataLines(_filePath, hasHeader: true), modern.Length, i => PackageAssetColumns.Of(modern[i]));
    }

    [GlobalSetup(Target = nameof(ReadAllRows_Legacy))]
    public void SetupLegacy()
    {
        Prepare();
        var legacy = ReadAllRows_Legacy();
        DataCheck.PackageAssets("FileHelpers 3.x", DataCheck.DataLines(_filePath, hasHeader: true), legacy.Length, i =>
        {
            var r = legacy[i];
            return DataCheck.PackageColumns(r.Id, r.Version, r.DownloadCount, r.MinClientVersion, r.IsPrerelease,
                r.Published, r.LastUpdated, r.PackageSize, r.RepositoryType, r.RequireLicenseAcceptance);
        });
        DataCheck.Equal("FileHelpers", _oldEngine.GetType().Assembly.GetName().Name, "3.x engine assembly");
    }

    [GlobalSetup(Target = nameof(Modern_ParseAndDiscard))]
    public void SetupDiscardModern()
    {
        Prepare();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), Modern_ParseAndDiscard(), "ModernLibs.FileHelpers parse-and-discard checksum");
    }

    [GlobalSetup(Target = nameof(Legacy_ParseAndDiscard))]
    public void SetupDiscardLegacy()
    {
        Prepare();
        _oldAsyncEngine = new Legacy.FileHelperAsyncEngine<OldPackageAsset>();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), Legacy_ParseAndDiscard(), "FileHelpers 3.x parse-and-discard checksum");
        DataCheck.Equal("FileHelpers", _oldAsyncEngine.GetType().Assembly.GetName().Name, "3.x async engine assembly");
    }

    [GlobalCleanup(Target = nameof(Legacy_ParseAndDiscard))]
    public void CleanupDiscardLegacy() => ((IDisposable)_oldAsyncEngine).Dispose();

    [Benchmark(Baseline = true)]
    public PackageAsset[] ReadAllRows_Modern() => _modernEngine.ReadFile(_filePath);

    [Benchmark]
    public OldPackageAsset[] ReadAllRows_Legacy() => _oldEngine.ReadFile(_filePath);

    // Parse-and-discard (design B2): every record parsed completely, none kept (see DiscardChecksum). FileHelpers
    // 3.x has no callback read; its record-by-record read loop is FileHelperAsyncEngine.ReadNext.

    [Benchmark]
    public long Modern_ParseAndDiscard()
    {
        long checksum = 0;
        _modernEngine.ReadFile(_filePath, r => checksum += r.Id);
        return checksum;
    }

    [Benchmark]
    public long Legacy_ParseAndDiscard()
    {
        long checksum = 0;
        _oldAsyncEngine.BeginReadFile(_filePath);
        try
        {
            while (_oldAsyncEngine.ReadNext() is { } r)
                checksum += r.Id;
        }
        finally
        {
            _oldAsyncEngine.Close();
        }
        return checksum;
    }
}
