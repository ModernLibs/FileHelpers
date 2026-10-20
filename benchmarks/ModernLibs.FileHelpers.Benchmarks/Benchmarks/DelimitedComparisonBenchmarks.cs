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
public class DelimitedComparisonBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<PackageAsset> _modernEngine = null!;
    private Legacy.FileHelperEngine<OldPackageAsset> _oldEngine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(DelimitedComparisonBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsurePackageAssets(dataDir, Rows);
        _modernEngine = new FileHelperEngine<PackageAsset>();
        _oldEngine = new Legacy.FileHelperEngine<OldPackageAsset>();

        // Correctness guard: both libraries must parse the file they are measured on.
        var lines = DataCheck.DataLines(_filePath, hasHeader: true);
        var modern = ReadAllRows_Modern();
        DataCheck.PackageAssets("ModernLibs.FileHelpers", lines, modern.Length, i => PackageAssetColumns.Of(modern[i]));
        var legacy = ReadAllRows_Legacy();
        DataCheck.PackageAssets("FileHelpers 3.x", lines, legacy.Length, i =>
        {
            var r = legacy[i];
            return DataCheck.PackageColumns(r.Id, r.Version, r.DownloadCount, r.MinClientVersion, r.IsPrerelease,
                r.Published, r.LastUpdated, r.PackageSize, r.RepositoryType, r.RequireLicenseAcceptance);
        });
        DataCheck.Equal("FileHelpers", _oldEngine.GetType().Assembly.GetName().Name, "3.x engine assembly");
    }

    [Benchmark(Baseline = true)]
    public PackageAsset[] ReadAllRows_Modern() => _modernEngine.ReadFile(_filePath);

    [Benchmark]
    public OldPackageAsset[] ReadAllRows_Legacy() => _oldEngine.ReadFile(_filePath);
}
