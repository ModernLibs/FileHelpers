// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using System.Globalization;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

/// <summary>Eight columns, no header, 505 characters per line (see <see cref="DatasetGenerator.EnsureLongLines"/>).</summary>
[DelimitedRecord(",")]
public sealed class LongLineRecord
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Text1 { get; set; } = "";
    [FieldOrder(3)] public string Text2 { get; set; } = "";
    [FieldOrder(4)] public string Text3 { get; set; } = "";
    [FieldOrder(5)] public string Text4 { get; set; } = "";
    [FieldOrder(6)] public string Text5 { get; set; } = "";
    [FieldOrder(7)] public string Text6 { get; set; } = "";
    [FieldOrder(8)] public string Text7 { get; set; } = "";

    public string ToLine() => string.Join(',', Id.ToString("D8", CultureInfo.InvariantCulture), Text1, Text2, Text3, Text4, Text5, Text6, Text7);
}

/// <summary>Long lines: per-line work (line scan, buffer growth) dominates over per-field work.</summary>
[MemoryDiagnoser]
public class LongLineBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<LongLineRecord> _engine = null!;

    [Params(5_000, 20_000)]
    public int Rows { get; set; }

    private void Prepare()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(LongLineBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsureLongLines(dataDir, Rows);
        _engine = new FileHelperEngine<LongLineRecord>();
    }

    // Correctness guard: each setup runs only the guard of its own benchmark, so no other read runs in the
    // measured process (see FixedWidthParseBenchmarks).
    [GlobalSetup(Target = nameof(ReadFile))]
    public void SetupReadFile()
    {
        Prepare();
        var records = ReadFile();
        DataCheck.LongLines("FileHelpers", _filePath, Rows, records.Length, i => records[i].ToLine());
    }

    [GlobalSetup(Target = nameof(ReadFile_ParseAndDiscard))]
    public void SetupDiscard()
    {
        Prepare();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), ReadFile_ParseAndDiscard(), "parse-and-discard checksum");
    }

    [Benchmark(Baseline = true)]
    public LongLineRecord[] ReadFile() => _engine.ReadFile(_filePath);

    /// <summary>Every record parsed completely, none kept; the checksum is the sum of the Ids.</summary>
    [Benchmark]
    public long ReadFile_ParseAndDiscard()
    {
        long checksum = 0;
        _engine.ReadFile(_filePath, r => checksum += r.Id);
        return checksum;
    }
}
