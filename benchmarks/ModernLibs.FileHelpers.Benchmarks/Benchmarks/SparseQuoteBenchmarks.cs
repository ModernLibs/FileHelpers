// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

/// <summary>
/// Same layout as <see cref="QuotedFieldsBenchmarks"/> (header, every text column [FieldQuoted]), but only
/// 1 % of the rows contain a quote: measures what the quote handling costs on rows that have none.
/// </summary>
[MemoryDiagnoser]
public class SparseQuoteBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<QuotedRecord> _engine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(SparseQuoteBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsureSparseQuotes(dataDir, Rows);
        _engine = new FileHelperEngine<QuotedRecord>();

        var records = ReadFile();
        DataCheck.SparseQuotes("FileHelpers", _filePath, Rows, records.Length, i => (records[i].Id, records[i].Description, records[i].Notes));
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), ReadFile_ParseAndDiscard(), "parse-and-discard checksum");
    }

    [Benchmark(Baseline = true)]
    public QuotedRecord[] ReadFile() => _engine.ReadFile(_filePath);

    /// <summary>Every record parsed completely, none kept; the checksum is the sum of the Ids.</summary>
    [Benchmark]
    public long ReadFile_ParseAndDiscard()
    {
        long checksum = 0;
        _engine.ReadFile(_filePath, r => checksum += r.Id);
        return checksum;
    }
}
