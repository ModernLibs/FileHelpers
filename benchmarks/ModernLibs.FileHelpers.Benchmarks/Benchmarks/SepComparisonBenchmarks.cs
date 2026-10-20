// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using System.Globalization;
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

        // Correctness guard: both readers must parse the file, and both writers must write it back exactly
        // as the generator wrote it (header line, same date format), so the two sides do the same work.
        var lines = DataCheck.DataLines(_filePath, hasHeader: true);
        DataCheck.PackageAssets("FileHelpers", lines, _records.Length, i => PackageAssetColumns.Of(_records[i]));
        var sep = ReadAllRows_Sep();
        DataCheck.PackageAssets("Sep", lines, sep.Count, i => PackageAssetColumns.Of(sep[i]));
        var fileLines = DataCheck.DataLines(_filePath, hasHeader: false);
        DataCheck.WrittenPackageAssets("FileHelpers", fileLines, WriteString_FileHelpers());
        DataCheck.WrittenPackageAssets("Sep", fileLines, WriteString_Sep());
    }

    [Benchmark(Baseline = true)]
    public PackageAsset[] ReadAllRows_FileHelpers() => _engine.ReadFile(_filePath);

    [Benchmark]
    public IReadOnlyList<PackageAssetRow> ReadAllRows_Sep()
    {
        var results = new List<PackageAssetRow>(Rows);
        using var reader = Sep.New(',').Reader().FromFile(_filePath);
        // Columns are addressed by index, as in the Sylvan benchmarks (no per-row name lookup).
        foreach (var row in reader)
        {
            results.Add(new PackageAssetRow
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
            });
        }
        return results;
    }

    [Benchmark]
    public string WriteString_FileHelpers() => _engine.WriteString(_records);

    [Benchmark]
    public string WriteString_Sep()
    {
        // Sep writes the header from the column names (FileHelpers writes back the header it read);
        // dates are formatted in the same "s" format FileHelpers writes.
        using var writer = Sep.New(',').Writer().ToText();
        Span<char> date = stackalloc char[32];
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
            r.Published.TryFormat(date, out var n, DatasetGenerator.DateFormat, CultureInfo.InvariantCulture);
            row["Published"].Set(date[..n]);
            r.LastUpdated.TryFormat(date, out n, DatasetGenerator.DateFormat, CultureInfo.InvariantCulture);
            row["LastUpdated"].Set(date[..n]);
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
