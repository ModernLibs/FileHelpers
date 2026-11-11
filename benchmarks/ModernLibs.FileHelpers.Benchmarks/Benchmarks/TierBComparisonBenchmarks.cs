// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods and field mappings are intentionally long.
#pragma warning disable MA0051

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using Cursively;
using FlatFiles;
using FlatFiles.TypeMapping;
using ModernLibs.FileHelpers.Benchmarks.Data;
using TinyCsvParser;
using TinyCsvParser.Models;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

// TinyCsvParser maps columns by index. Defaults of its converters parse int, long, bool and DateTime with the
// current culture; the data is culture-neutral (digits, "True"/"False", ISO 8601 "s" dates).
internal sealed class PackageAssetTinyMapping : CsvMapping<PackageAssetRow>
{
    public PackageAssetTinyMapping()
    {
        MapProperty(0, x => x.Id);
        MapProperty(1, x => x.Version);
        MapProperty(2, x => x.Authors);
        MapProperty(3, x => x.Description);
        MapProperty(4, x => x.Tags);
        MapProperty(5, x => x.DownloadCount);
        MapProperty(6, x => x.ProjectUrl);
        MapProperty(7, x => x.LicenseUrl);
        MapProperty(8, x => x.IconUrl);
        MapProperty(9, x => x.Language);
        MapProperty(10, x => x.Framework);
        MapProperty(11, x => x.MinClientVersion);
        MapProperty(12, x => x.Type);
        MapProperty(13, x => x.IsPrerelease);
        MapProperty(14, x => x.Published);
        MapProperty(15, x => x.LastUpdated);
        MapProperty(16, x => x.PackageSize);
        MapProperty(17, x => x.DependencyCount);
        MapProperty(18, x => x.HasReadme);
        MapProperty(19, x => x.HasLicense);
        MapProperty(20, x => x.HasIcon);
        MapProperty(21, x => x.IsSigned);
        MapProperty(22, x => x.RepositoryType);
        MapProperty(23, x => x.RepositoryUrl);
        MapProperty(24, x => x.RequireLicenseAcceptance);
    }
}

/// <summary>Row assembly for the readers that hand out text fields one at a time (NReco, LumenWorks, Cursively).</summary>
internal static class PackageAssetFields
{
    private static readonly CultureInfo s_culture = CultureInfo.InvariantCulture;

    public static void Set(PackageAssetRow r, int column, ReadOnlySpan<char> text)
    {
        switch (column)
        {
            case 0: r.Id = int.Parse(text, provider: s_culture); break;
            case 1: r.Version = new string(text); break;
            case 2: r.Authors = new string(text); break;
            case 3: r.Description = new string(text); break;
            case 4: r.Tags = new string(text); break;
            case 5: r.DownloadCount = int.Parse(text, provider: s_culture); break;
            case 6: r.ProjectUrl = new string(text); break;
            case 7: r.LicenseUrl = new string(text); break;
            case 8: r.IconUrl = new string(text); break;
            case 9: r.Language = new string(text); break;
            case 10: r.Framework = new string(text); break;
            case 11: r.MinClientVersion = new string(text); break;
            case 12: r.Type = int.Parse(text, provider: s_culture); break;
            case 13: r.IsPrerelease = bool.Parse(text); break;
            case 14: r.Published = DateTime.Parse(text, s_culture); break;
            case 15: r.LastUpdated = DateTime.Parse(text, s_culture); break;
            case 16: r.PackageSize = long.Parse(text, provider: s_culture); break;
            case 17: r.DependencyCount = int.Parse(text, provider: s_culture); break;
            case 18: r.HasReadme = bool.Parse(text); break;
            case 19: r.HasLicense = bool.Parse(text); break;
            case 20: r.HasIcon = bool.Parse(text); break;
            case 21: r.IsSigned = bool.Parse(text); break;
            case 22: r.RepositoryType = new string(text); break;
            case 23: r.RepositoryUrl = new string(text); break;
            case 24: r.RequireLicenseAcceptance = bool.Parse(text); break;
        }
    }
}

/// <summary>
/// Cursively hands out UTF-8 bytes. Each field is decoded into a char buffer and then set exactly like the
/// text-based readers do, so the work per field is the same.
/// </summary>
internal sealed class PackageAssetCursivelyVisitor(List<PackageAssetRow> rows) : CsvReaderVisitorBase
{
    private char[] _chars = new char[256];
    private int _length;
    private int _column;
    private bool _header = true;
    private PackageAssetRow _current = new();

    public override void VisitPartialFieldContents(ReadOnlySpan<byte> chunk) => Append(chunk);

    public override void VisitEndOfField(ReadOnlySpan<byte> chunk)
    {
        Append(chunk);
        if (!_header)
            PackageAssetFields.Set(_current, _column, _chars.AsSpan(0, _length));
        _length = 0;
        _column++;
    }

    public override void VisitEndOfRecord()
    {
        if (_header)
            _header = false;
        else
        {
            rows.Add(_current);
            _current = new PackageAssetRow();
        }
        _column = 0;
    }

    private void Append(ReadOnlySpan<byte> chunk)
    {
        int max = Encoding.UTF8.GetMaxCharCount(chunk.Length);
        if (_length + max > _chars.Length)
            Array.Resize(ref _chars, Math.Max(_chars.Length * 2, _length + max));
        _length += Encoding.UTF8.GetChars(chunk, _chars.AsSpan(_length));
    }
}

/// <summary>
/// The tier B delimited libraries (T11), each reading the 25-column PackageAssets file into
/// <see cref="PackageAssetRow"/> with every field converted. One method per library, one correctness setup per method.
/// </summary>
[MemoryDiagnoser]
public class TierBComparisonBenchmarks
{
    private string _filePath = "";

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    private void Prepare()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(TierBComparisonBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsurePackageAssets(dataDir, Rows);
    }

    private void Check(string name, IReadOnlyList<PackageAssetRow> rows) =>
        DataCheck.PackageAssets(name, DataCheck.DataLines(_filePath, hasHeader: true), rows.Count, i => PackageAssetColumns.Of(rows[i]));

    [GlobalSetup(Target = nameof(NReco_ReadAll))]
    public void SetupNReco() { Prepare(); Check("NReco", NReco_ReadAll()); }

    [GlobalSetup(Target = nameof(TinyCsvParser_ReadAll))]
    public void SetupTinyCsvParser() { Prepare(); Check("TinyCsvParser", TinyCsvParser_ReadAll()); }

    [GlobalSetup(Target = nameof(Cursively_ReadAll))]
    public void SetupCursively() { Prepare(); Check("Cursively", Cursively_ReadAll()); }

    [GlobalSetup(Target = nameof(LumenWorks_ReadAll))]
    public void SetupLumenWorks() { Prepare(); Check("LumenWorks", LumenWorks_ReadAll()); }

    [GlobalSetup(Target = nameof(FlatFiles_ReadAll))]
    public void SetupFlatFiles() { Prepare(); Check("FlatFiles", FlatFiles_ReadAll()); }

    [Benchmark]
    public IReadOnlyList<PackageAssetRow> NReco_ReadAll()
    {
        var results = new List<PackageAssetRow>(Rows);
        using var stream = new StreamReader(_filePath);
        var reader = new NReco.Csv.CsvReader(stream, ",");
        bool header = true;
        while (reader.Read())
        {
            if (header) { header = false; continue; }
            var row = new PackageAssetRow();
            for (int c = 0; c < 25; c++)
                PackageAssetFields.Set(row, c, reader[c]);
            results.Add(row);
        }
        return results;
    }

    [Benchmark]
    public IReadOnlyList<PackageAssetRow> TinyCsvParser_ReadAll()
    {
        var results = new List<PackageAssetRow>(Rows);
        // Sequential (the parser is stateless and has no parallel mode in 3.x), header skipped.
        var parser = new CsvParser<PackageAssetRow>(new CsvOptions(',', '"', '"', Encoding.UTF8, SkipHeader: true), new PackageAssetTinyMapping());
        foreach (var result in parser.ReadFromFile(_filePath))
        {
            if (!result.IsSuccess)
                throw new InvalidOperationException("TinyCsvParser could not map a row.");
            results.Add(result.Result);
        }
        return results;
    }

    [Benchmark]
    public IReadOnlyList<PackageAssetRow> Cursively_ReadAll()
    {
        var results = new List<PackageAssetRow>(Rows);
        CsvSyncInput.ForMemoryMappedFile(_filePath).Process(new PackageAssetCursivelyVisitor(results));
        return results;
    }

    [Benchmark]
    public IReadOnlyList<PackageAssetRow> LumenWorks_ReadAll()
    {
        var results = new List<PackageAssetRow>(Rows);
        using var stream = new StreamReader(_filePath);
        using var reader = new LumenWorks.Framework.IO.Csv.CsvReader(stream, hasHeaders: true);
        while (reader.ReadNextRecord())
        {
            var row = new PackageAssetRow();
            for (int c = 0; c < 25; c++)
                PackageAssetFields.Set(row, c, reader[c]);
            results.Add(row);
        }
        return results;
    }

    [Benchmark]
    public IReadOnlyList<PackageAssetRow> FlatFiles_ReadAll()
    {
        var mapper = DelimitedTypeMapper.Define(() => new PackageAssetRow());
        mapper.Property(x => x.Id);
        mapper.Property(x => x.Version);
        mapper.Property(x => x.Authors);
        mapper.Property(x => x.Description);
        mapper.Property(x => x.Tags);
        mapper.Property(x => x.DownloadCount);
        mapper.Property(x => x.ProjectUrl);
        mapper.Property(x => x.LicenseUrl);
        mapper.Property(x => x.IconUrl);
        mapper.Property(x => x.Language);
        mapper.Property(x => x.Framework);
        mapper.Property(x => x.MinClientVersion);
        mapper.Property(x => x.Type);
        mapper.Property(x => x.IsPrerelease);
        mapper.Property(x => x.Published);
        mapper.Property(x => x.LastUpdated);
        mapper.Property(x => x.PackageSize);
        mapper.Property(x => x.DependencyCount);
        mapper.Property(x => x.HasReadme);
        mapper.Property(x => x.HasLicense);
        mapper.Property(x => x.HasIcon);
        mapper.Property(x => x.IsSigned);
        mapper.Property(x => x.RepositoryType);
        mapper.Property(x => x.RepositoryUrl);
        mapper.Property(x => x.RequireLicenseAcceptance);
        var options = new DelimitedOptions { Separator = ",", IsFirstRecordSchema = true, FormatProvider = CultureInfo.InvariantCulture };
        using var stream = new StreamReader(_filePath);
        return mapper.Read(stream, options).ToList();
    }
}
