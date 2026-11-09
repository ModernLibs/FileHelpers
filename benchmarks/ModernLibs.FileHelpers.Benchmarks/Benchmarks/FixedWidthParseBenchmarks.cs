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

/// <summary>The fixed-width row as CsvHelper 34 reads it: the same columns as <see cref="FixedRecord"/>.</summary>
[CsvHelper.CsvRecord]
public sealed partial class FixedCsvHelperRow
{
    [CsvHelper.CsvColumn(Index = 0, Width = 8)] public string Id { get; set; } = "";
    [CsvHelper.CsvColumn(Index = 1, Width = 12)] public string Name { get; set; } = "";
    [CsvHelper.CsvColumn(Index = 2, Width = 10)] public string NumericField { get; set; } = "";
    [CsvHelper.CsvColumn(Index = 3, Width = 15)] public string Description { get; set; } = "";
    [CsvHelper.CsvColumn(Index = 4, Width = 20)] public string DecimalField { get; set; } = "";
    [CsvHelper.CsvColumn(Index = 5, Width = 5)] public string Code { get; set; } = "";
    [CsvHelper.CsvColumn(Index = 6, Width = 10)] public string Suffix { get; set; } = "";
}

/// <summary>Concatenates the fixed-width fields back into the record's line, for <see cref="DataCheck.FixedWidth"/>.</summary>
internal static class FixedRecordText
{
    public static string Of(FixedRecord r) => string.Concat(r.Id, r.Name, r.NumericField, r.Description, r.DecimalField, r.Code, r.Suffix);

    public static string Of(FixedRecordStruct r) => string.Concat(r.Id, r.Name, r.NumericField, r.Description, r.DecimalField, r.Code, r.Suffix);

    public static string Of(FixedCsvHelperRow r) => string.Concat(r.Id, r.Name, r.NumericField, r.Description, r.DecimalField, r.Code, r.Suffix);
}

[MemoryDiagnoser]
public class FixedWidthParseBenchmarks
{
    // No header; the fields keep their padding, as FixedRecord has no [FieldTrim] (CsvHelper trims
    // fixed-width fields by default).
    private static readonly CsvHelper.CsvReaderOptions CsvHelperOptions = CsvHelper.CsvReaderOptions.Default with
    {
        Mode = CsvHelper.CsvMode.FixedWidth,
        HasHeader = false,
        FieldWidths = [8, 12, 10, 15, 20, 5, 10],
        TrimWhitespace = false,
    };

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
        var csvHelper = ReadAllRows_CsvHelper();
        DataCheck.FixedWidth("CsvHelper", lines, csvHelper.Count, i => FixedRecordText.Of(csvHelper[i]));
        DataCheck.Equal(Rows, SliceAllFields_SpanSlicer(), "slicer line count");
    }

    [Benchmark(Baseline = true)]
    public FixedRecord[] ReadAllRows_Class() => _classEngine.ReadFile(_filePath);

    [Benchmark]
    public FixedRecordStruct[] ReadAllRows_Struct() => _structEngine.ReadFile(_filePath);

    [Benchmark]
    public IReadOnlyList<FixedCsvHelperRow> ReadAllRows_CsvHelper()
    {
        using var csv = CsvHelper.CsvReader.FromFile(_filePath, CsvHelperOptions);
        return csv.GetRecords<FixedCsvHelperRow>().ToList();
    }

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
