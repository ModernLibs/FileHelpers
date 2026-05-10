// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

// Old FileHelpers fixed-width record type (uses legacy attributes)
[FileHelpers.FixedLengthRecord]
public sealed class OldFixedRecord
{
    [FileHelpers.FieldFixedLength(8)] public string Id = "";
    [FileHelpers.FieldFixedLength(12)] public string Name = "";
    [FileHelpers.FieldFixedLength(10)] public string NumericField = "";
    [FileHelpers.FieldFixedLength(15)] public string Description = "";
    [FileHelpers.FieldFixedLength(20)] public string DecimalField = "";
    [FileHelpers.FieldFixedLength(5)] public string Code = "";
    [FileHelpers.FieldFixedLength(10)] public string Suffix = "";
}

[MemoryDiagnoser]
public class FixedWidthComparisonBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<FixedRecord> _modernClassEngine = null!;
    private FileHelperEngine<FixedRecordStruct> _modernStructEngine = null!;
    private FileHelpers.FileHelperEngine<OldFixedRecord> _oldEngine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(FixedWidthComparisonBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsureFixedWidth(dataDir, Rows);
        _modernClassEngine = new FileHelperEngine<FixedRecord>();
        _modernStructEngine = new FileHelperEngine<FixedRecordStruct>();
        _oldEngine = new FileHelpers.FileHelperEngine<OldFixedRecord>();
    }

    [Benchmark(Baseline = true)]
    public FixedRecord[] ReadAllRows_ModernClass() => _modernClassEngine.ReadFile(_filePath);

    [Benchmark]
    public FixedRecordStruct[] ReadAllRows_ModernStruct() => _modernStructEngine.ReadFile(_filePath);

    [Benchmark]
    public OldFixedRecord[] ReadAllRows_Legacy() => _oldEngine.ReadFile(_filePath);
}
