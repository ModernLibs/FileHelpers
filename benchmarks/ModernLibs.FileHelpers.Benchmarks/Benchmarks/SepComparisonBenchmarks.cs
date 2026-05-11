// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
using nietras.SeparatedValues;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class SepComparisonBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<PackageAsset> _engine = null!;
    private PackageAsset[] _records = null!;

    [Params(10_000, 100_000, 1_000_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(SepComparisonBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsurePackageAssets(dataDir, Rows);
        _engine = new FileHelperEngine<PackageAsset>();
        _records = _engine.ReadFile(_filePath);
    }

    [Benchmark(Baseline = true)]
    public PackageAsset[] ReadAllRows_FileHelpers() => _engine.ReadFile(_filePath);

    [Benchmark]
    public List<PackageAssetRow> ReadAllRows_Sep()
    {
        var results = new List<PackageAssetRow>(Rows);
        using var reader = Sep.New(',').Reader().FromFile(_filePath);
        foreach (var row in reader)
        {
            results.Add(new PackageAssetRow
            {
                Id = row["Id"].Parse<int>(),
                Version = row["Version"].ToString(),
                Authors = row["Authors"].ToString(),
                Description = row["Description"].ToString(),
                Tags = row["Tags"].ToString(),
                DownloadCount = row["DownloadCount"].Parse<int>(),
                ProjectUrl = row["ProjectUrl"].ToString(),
                LicenseUrl = row["LicenseUrl"].ToString(),
                IconUrl = row["IconUrl"].ToString(),
                Language = row["Language"].ToString(),
                Framework = row["Framework"].ToString(),
                MinClientVersion = row["MinClientVersion"].ToString(),
                Type = row["Type"].Parse<int>(),
                IsPrerelease = row["IsPrerelease"].Parse<bool>(),
                Published = row["Published"].Parse<DateTime>(),
                LastUpdated = row["LastUpdated"].Parse<DateTime>(),
                PackageSize = row["PackageSize"].Parse<long>(),
                DependencyCount = row["DependencyCount"].Parse<int>(),
                HasReadme = row["HasReadme"].Parse<bool>(),
                HasLicense = row["HasLicense"].Parse<bool>(),
                HasIcon = row["HasIcon"].Parse<bool>(),
                IsSigned = row["IsSigned"].Parse<bool>(),
                RepositoryType = row["RepositoryType"].ToString(),
                RepositoryUrl = row["RepositoryUrl"].ToString(),
                RequireLicenseAcceptance = row["RequireLicenseAcceptance"].Parse<bool>(),
            });
        }
        return results;
    }

    [Benchmark]
    public string WriteString_FileHelpers() => _engine.WriteString(_records);

    [Benchmark]
    public string WriteString_Sep()
    {
        using var writer = Sep.New(',').Writer().ToText();
        foreach (var r in _records)
        {
            using var row = writer.NewRow();
            row["Id"].Format(r.Id);
            row["Version"].Set(r.Version);
            row["Authors"].Set(r.Authors);
            row["Description"].Set(r.Description);
            row["Tags"].Set(r.Tags);
            row["DownloadCount"].Format(r.DownloadCount);
            row["ProjectUrl"].Set(r.ProjectUrl);
            row["LicenseUrl"].Set(r.LicenseUrl);
            row["IconUrl"].Set(r.IconUrl);
            row["Language"].Set(r.Language);
            row["Framework"].Set(r.Framework);
            row["MinClientVersion"].Set(r.MinClientVersion);
            row["Type"].Format(r.Type);
            row["IsPrerelease"].Set(r.IsPrerelease ? "True" : "False");
            row["Published"].Format(r.Published);
            row["LastUpdated"].Format(r.LastUpdated);
            row["PackageSize"].Format(r.PackageSize);
            row["DependencyCount"].Format(r.DependencyCount);
            row["HasReadme"].Set(r.HasReadme ? "True" : "False");
            row["HasLicense"].Set(r.HasLicense ? "True" : "False");
            row["HasIcon"].Set(r.HasIcon ? "True" : "False");
            row["IsSigned"].Set(r.IsSigned ? "True" : "False");
            row["RepositoryType"].Set(r.RepositoryType);
            row["RepositoryUrl"].Set(r.RepositoryUrl);
            row["RequireLicenseAcceptance"].Set(r.RequireLicenseAcceptance ? "True" : "False");
        }
        return writer.ToString();
    }
}
