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

    private void Prepare()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(SepComparisonBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsurePackageAssets(dataDir, Rows);
        _engine = new FileHelperEngine<PackageAsset>();
    }

    /// <summary>
    /// Loads the records the writers write. The callback read collects them without the pooled record buffer
    /// of the array read, so no such buffer stays in the process being measured.
    /// </summary>
    private void PrepareRecords()
    {
        Prepare();
        var records = new List<PackageAsset>(Rows);
        _engine.ReadFile(_filePath, records.Add);
        _records = records.ToArray();
        DataCheck.PackageAssets("FileHelpers records", DataCheck.DataLines(_filePath, hasHeader: true), _records.Length, i => PackageAssetColumns.Of(_records[i]));
    }

    // Correctness guard: both readers must parse the file, and both writers must write it back exactly as the
    // generator wrote it (header line, same date format), so the two sides do the same work. Each setup runs
    // only the guard of its own benchmark, so no other reader or writer runs in the measured process (see
    // FixedWidthParseBenchmarks).
    [GlobalSetup(Target = nameof(ReadAllRows_FileHelpers))]
    public void SetupReadFileHelpers()
    {
        Prepare();
        var fh = ReadAllRows_FileHelpers();
        DataCheck.PackageAssets("FileHelpers", DataCheck.DataLines(_filePath, hasHeader: true), fh.Length, i => PackageAssetColumns.Of(fh[i]));
    }

    [GlobalSetup(Target = nameof(ReadAllRows_Sep))]
    public void SetupReadSep()
    {
        Prepare();
        var sep = ReadAllRows_Sep();
        DataCheck.PackageAssets("Sep", DataCheck.DataLines(_filePath, hasHeader: true), sep.Count, i => PackageAssetColumns.Of(sep[i]));
    }

    [GlobalSetup(Target = nameof(WriteString_FileHelpers))]
    public void SetupWriteFileHelpers()
    {
        PrepareRecords();
        DataCheck.WrittenPackageAssets("FileHelpers", DataCheck.DataLines(_filePath, hasHeader: false), WriteString_FileHelpers());
    }

    [GlobalSetup(Target = nameof(WriteString_Sep))]
    public void SetupWriteSep()
    {
        PrepareRecords();
        DataCheck.WrittenPackageAssets("Sep", DataCheck.DataLines(_filePath, hasHeader: false), WriteString_Sep());
    }

    [GlobalSetup(Target = nameof(FileHelpers_ParseAndDiscard))]
    public void SetupDiscardFileHelpers()
    {
        Prepare();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), FileHelpers_ParseAndDiscard(), "FileHelpers parse-and-discard checksum");
    }

    [GlobalSetup(Target = nameof(Sep_ParseAndDiscard))]
    public void SetupDiscardSep()
    {
        Prepare();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), Sep_ParseAndDiscard(), "Sep parse-and-discard checksum");
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

    // Parse-and-discard (design B2): every record parsed completely, none kept (see DiscardChecksum).

    [Benchmark]
    public long FileHelpers_ParseAndDiscard()
    {
        long checksum = 0;
        _engine.ReadFile(_filePath, r => checksum += r.Id);
        return checksum;
    }

    [Benchmark]
    public long Sep_ParseAndDiscard()
    {
        long checksum = 0;
        using var reader = Sep.New(',').Reader().FromFile(_filePath);
        foreach (var row in reader)
            checksum += PackageAssetColumns.FromSep(row).Id;
        return checksum;
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
