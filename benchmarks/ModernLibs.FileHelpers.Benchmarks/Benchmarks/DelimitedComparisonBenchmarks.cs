// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

// Old FileHelpers record type (uses legacy attributes)
[FileHelpers.DelimitedRecord(",")]
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
    public DateTime Published;
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
    private FileHelpers.FileHelperEngine<OldPackageAsset> _oldEngine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(DelimitedComparisonBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsurePackageAssets(dataDir, Rows);
        _modernEngine = new FileHelperEngine<PackageAsset>();
        _oldEngine = new FileHelpers.FileHelperEngine<OldPackageAsset>();
    }

    [Benchmark(Baseline = true)]
    public PackageAsset[] ReadAllRows_Modern() => _modernEngine.ReadFile(_filePath);

    [Benchmark]
    public OldPackageAsset[] ReadAllRows_Legacy() => _oldEngine.ReadFile(_filePath);
}
