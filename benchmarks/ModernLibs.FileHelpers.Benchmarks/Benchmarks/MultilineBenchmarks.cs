// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

/// <summary>No header; Text may be a quoted value with an embedded line break (every tenth row).</summary>
[DelimitedRecord(",")]
public sealed class MultilineRecord
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = "";
    [FieldOrder(3), FieldQuoted] public string Text { get; set; } = "";
    [FieldOrder(4)] public string Category { get; set; } = "";
}

/// <summary>Quoted values that continue on the next line: the reader must join lines for 10 % of the rows.</summary>
[MemoryDiagnoser]
public class MultilineBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<MultilineRecord> _engine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    private void Prepare()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(MultilineBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsureMultilineValues(dataDir, Rows);
        _engine = new FileHelperEngine<MultilineRecord>();
    }

    // Correctness guard: each setup runs only the guard of its own benchmark (see FixedWidthParseBenchmarks).
    [GlobalSetup(Target = nameof(ReadFile))]
    public void SetupReadFile()
    {
        Prepare();
        var records = ReadFile();
        DataCheck.Multiline("FileHelpers", _filePath, Rows, records.Length, i => (records[i].Id, records[i].Text));
    }

    [GlobalSetup(Target = nameof(ReadFile_ParseAndDiscard))]
    public void SetupDiscard()
    {
        Prepare();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), ReadFile_ParseAndDiscard(), "parse-and-discard checksum");
    }

    [Benchmark(Baseline = true)]
    public MultilineRecord[] ReadFile() => _engine.ReadFile(_filePath);

    /// <summary>Every record parsed completely, none kept; the checksum is the sum of the Ids.</summary>
    [Benchmark]
    public long ReadFile_ParseAndDiscard()
    {
        long checksum = 0;
        _engine.ReadFile(_filePath, r => checksum += r.Id);
        return checksum;
    }
}
