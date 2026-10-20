// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

[FixedLengthRecord(FixedMode.ExactLength)]
public sealed class FixedRecord
{
    [FieldOrder(1), FieldFixedLength(8)] public string Id { get; set; } = "";
    [FieldOrder(2), FieldFixedLength(12)] public string Name { get; set; } = "";
    [FieldOrder(3), FieldFixedLength(10)] public string NumericField { get; set; } = "";
    [FieldOrder(4), FieldFixedLength(15)] public string Description { get; set; } = "";
    [FieldOrder(5), FieldFixedLength(20)] public string DecimalField { get; set; } = "";
    [FieldOrder(6), FieldFixedLength(5)] public string Code { get; set; } = "";
    [FieldOrder(7), FieldFixedLength(10)] public string Suffix { get; set; } = "";
}

[FixedLengthRecord(FixedMode.ExactLength)]
public record struct FixedRecordStruct(
    [property: FieldOrder(1), FieldFixedLength(8)] string Id,
    [property: FieldOrder(2), FieldFixedLength(12)] string Name,
    [property: FieldOrder(3), FieldFixedLength(10)] string NumericField,
    [property: FieldOrder(4), FieldFixedLength(15)] string Description,
    [property: FieldOrder(5), FieldFixedLength(20)] string DecimalField,
    [property: FieldOrder(6), FieldFixedLength(5)] string Code,
    [property: FieldOrder(7), FieldFixedLength(10)] string Suffix
);

/// <summary>Concatenates the fixed-width fields back into the record's line, for <see cref="DataCheck.FixedWidth"/>.</summary>
internal static class FixedRecordText
{
    public static string Of(FixedRecord r) => string.Concat(r.Id, r.Name, r.NumericField, r.Description, r.DecimalField, r.Code, r.Suffix);

    public static string Of(FixedRecordStruct r) => string.Concat(r.Id, r.Name, r.NumericField, r.Description, r.DecimalField, r.Code, r.Suffix);
}

[MemoryDiagnoser]
public class FixedWidthParseBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<FixedRecord> _classEngine = null!;
    private FileHelperEngine<FixedRecordStruct> _structEngine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(FixedWidthParseBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsureFixedWidth(dataDir, Rows);
        _classEngine = new FileHelperEngine<FixedRecord>();
        _structEngine = new FileHelperEngine<FixedRecordStruct>();

        // Correctness guard: every benchmark must parse the file it measures.
        var lines = DataCheck.DataLines(_filePath, hasHeader: false);
        var cls = ReadAllRows_Class();
        DataCheck.FixedWidth("class", lines, cls.Length, i => FixedRecordText.Of(cls[i]));
        var str = ReadAllRows_Struct();
        DataCheck.FixedWidth("struct", lines, str.Length, i => FixedRecordText.Of(str[i]));
        DataCheck.Equal(Rows, SliceAllFields_SpanSlicer(), "slicer line count");
    }

    [Benchmark(Baseline = true)]
    public FixedRecord[] ReadAllRows_Class() => _classEngine.ReadFile(_filePath);

    [Benchmark]
    public FixedRecordStruct[] ReadAllRows_Struct() => _structEngine.ReadFile(_filePath);

    [Benchmark]
    public int SliceAllFields_SpanSlicer()
    {
        var content = File.ReadAllText(_filePath);
        var lines = content.Split('\n');
        int count = 0;
        foreach (var line in lines)
        {
            if (line.Length < 80) continue;
            var slicer = new FixedLengthSlicer(line.AsSpan());
            _ = slicer.Slice(0, 8);
            _ = slicer.Slice(8, 12);
            _ = slicer.Slice(20, 10);
            _ = slicer.Slice(30, 15);
            _ = slicer.Slice(45, 20);
            _ = slicer.Slice(65, 5);
            _ = slicer.Slice(70, 10);
            count++;
        }
        return count;
    }
}
