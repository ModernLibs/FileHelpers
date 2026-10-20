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

// FileHelpers 3.x fixed-width record type (the 3.x default FixedMode is ExactLength, like FixedRecord).
[Legacy.FixedLengthRecord]
public sealed class OldFixedRecord
{
    [Legacy.FieldFixedLength(8)] public string Id = "";
    [Legacy.FieldFixedLength(12)] public string Name = "";
    [Legacy.FieldFixedLength(10)] public string NumericField = "";
    [Legacy.FieldFixedLength(15)] public string Description = "";
    [Legacy.FieldFixedLength(20)] public string DecimalField = "";
    [Legacy.FieldFixedLength(5)] public string Code = "";
    [Legacy.FieldFixedLength(10)] public string Suffix = "";
}

[MemoryDiagnoser]
public class FixedWidthComparisonBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<FixedRecord> _modernClassEngine = null!;
    private FileHelperEngine<FixedRecordStruct> _modernStructEngine = null!;
    private Legacy.FileHelperEngine<OldFixedRecord> _oldEngine = null!;

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
        _oldEngine = new Legacy.FileHelperEngine<OldFixedRecord>();

        // Correctness guard: every benchmark must parse the file it measures.
        var lines = DataCheck.DataLines(_filePath, hasHeader: false);
        var cls = ReadAllRows_ModernClass();
        DataCheck.FixedWidth("ModernLibs class", lines, cls.Length, i => FixedRecordText.Of(cls[i]));
        var str = ReadAllRows_ModernStruct();
        DataCheck.FixedWidth("ModernLibs struct", lines, str.Length, i => FixedRecordText.Of(str[i]));
        var legacy = ReadAllRows_Legacy();
        DataCheck.FixedWidth("FileHelpers 3.x", lines, legacy.Length, i =>
        {
            var r = legacy[i];
            return string.Concat(r.Id, r.Name, r.NumericField, r.Description, r.DecimalField, r.Code, r.Suffix);
        });
        DataCheck.Equal("FileHelpers", _oldEngine.GetType().Assembly.GetName().Name, "3.x engine assembly");
    }

    [Benchmark(Baseline = true)]
    public FixedRecord[] ReadAllRows_ModernClass() => _modernClassEngine.ReadFile(_filePath);

    [Benchmark]
    public FixedRecordStruct[] ReadAllRows_ModernStruct() => _modernStructEngine.ReadFile(_filePath);

    [Benchmark]
    public OldFixedRecord[] ReadAllRows_Legacy() => _oldEngine.ReadFile(_filePath);
}
