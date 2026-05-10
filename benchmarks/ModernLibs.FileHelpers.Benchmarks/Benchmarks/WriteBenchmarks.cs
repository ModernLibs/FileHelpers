// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class WriteBenchmarks
{
    private PackageAsset[] _records = null!;
    private FileHelperEngine<PackageAsset> _engine = null!;
    private string _outputPath = "";

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(WriteBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        var sourceFile = DatasetGenerator.EnsurePackageAssets(dataDir, Rows);

        _engine = new FileHelperEngine<PackageAsset>();
        _records = _engine.ReadFile(sourceFile);
        _outputPath = Path.Combine(dataDir, "output_write.csv");
    }

    [Benchmark(Baseline = true)]
    public string WriteString_FileHelpers() => _engine.WriteString(_records);

    [Benchmark]
    public void WriteFile_FileHelpers() => _engine.WriteFile(_outputPath, _records);

    [Benchmark]
    public string WriteString_CsvHelper()
    {
        using var writer = new System.IO.StringWriter();
        using var csv = new CsvHelper.CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
        csv.WriteRecords(_records);
        return writer.ToString();
    }
}
