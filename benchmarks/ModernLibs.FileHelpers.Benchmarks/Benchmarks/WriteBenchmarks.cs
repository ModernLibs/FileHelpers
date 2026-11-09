// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using System.Globalization;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

/// <summary>
/// The CsvHelper 34 record writer for <see cref="PackageAsset"/> writes its dates in the "s" format that
/// FileHelpers writes. The class only carries the attributes; it is never created.
/// </summary>
[CsvHelper.CsvRecordMap(typeof(PackageAsset))]
internal sealed partial class PackageAssetCsvMap
{
    [CsvHelper.CsvFormat(DatasetGenerator.DateFormat)] public DateTime Published { get; set; }
    [CsvHelper.CsvFormat(DatasetGenerator.DateFormat)] public DateTime LastUpdated { get; set; }
}

[MemoryDiagnoser]
public class WriteBenchmarks
{
    private PackageAsset[] _records = null!;
    private FileHelperEngine<PackageAsset> _engine = null!;
    private string _outputPath = "";
    private string _sourceFile = "";

    [Params(10_000, 100_000, 1_000_000)]
    public int Rows { get; set; }

    /// <summary>
    /// Loads the records the writers write. The callback read collects them without the pooled record buffer
    /// of the array read, so no such buffer stays in the process being measured.
    /// </summary>
    private void Prepare()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(WriteBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _sourceFile = DatasetGenerator.EnsurePackageAssets(dataDir, Rows);

        _engine = new FileHelperEngine<PackageAsset>();
        var records = new List<PackageAsset>(Rows);
        _engine.ReadFile(_sourceFile, records.Add);
        _records = records.ToArray();
        _outputPath = Path.Combine(dataDir, "output_write.csv");
        DataCheck.PackageAssets("FileHelpers read", DataCheck.DataLines(_sourceFile, hasHeader: true), _records.Length, i => PackageAssetColumns.Of(_records[i]));
    }

    // Correctness guard: every writer must write the records back exactly as the generator wrote the file,
    // header line included (FileHelpers writes back the header it skipped on read). Each setup runs only the
    // guard of its own benchmark, so no other writer runs in the measured process (see FixedWidthParseBenchmarks).
    [GlobalSetup(Target = nameof(WriteString_FileHelpers))]
    public void SetupWriteString()
    {
        Prepare();
        DataCheck.WrittenPackageAssets("FileHelpers WriteString", DataCheck.DataLines(_sourceFile, hasHeader: false), WriteString_FileHelpers());
    }

    [GlobalSetup(Target = nameof(WriteFile_FileHelpers))]
    public void SetupWriteFile()
    {
        Prepare();
        WriteFile_FileHelpers();
        DataCheck.WrittenPackageAssets("FileHelpers WriteFile", DataCheck.DataLines(_sourceFile, hasHeader: false), File.ReadAllText(_outputPath));
    }

    [GlobalSetup(Target = nameof(WriteString_CsvHelper))]
    public void SetupCsvHelper()
    {
        Prepare();
        DataCheck.WrittenPackageAssets("CsvHelper", DataCheck.DataLines(_sourceFile, hasHeader: false), WriteString_CsvHelper());
    }

    [Benchmark(Baseline = true)]
    public string WriteString_FileHelpers() => _engine.WriteString(_records);

    [Benchmark]
    public void WriteFile_FileHelpers() => _engine.WriteFile(_outputPath, _records);

    [Benchmark]
    public string WriteString_CsvHelper()
    {
        using var writer = new System.IO.StringWriter(CultureInfo.InvariantCulture);
        // Dates in the "s" format FileHelpers writes: see PackageAssetCsvMap.
        using (var csv = CsvHelper.CsvWriter.Create(writer, CsvHelper.CsvWriterOptions.Default, leaveOpen: true))
            csv.WriteRecords(_records);
        return writer.ToString();
    }
}
