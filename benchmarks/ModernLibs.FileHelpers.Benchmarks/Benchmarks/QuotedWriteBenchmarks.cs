// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

/// <summary>Writes the records of the seeded quoted-fields dataset: values that need quotes are quoted on output.</summary>
[MemoryDiagnoser]
public class QuotedWriteBenchmarks
{
    private QuotedRecord[] _records = null!;
    private FileHelperEngine<QuotedRecord> _engine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(QuotedWriteBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        var file = DatasetGenerator.EnsureQuotedFieldsRandom(dataDir, Rows);

        _engine = new FileHelperEngine<QuotedRecord>();
        var records = new List<QuotedRecord>(Rows);
        _engine.ReadFile(file, records.Add);
        _records = records.ToArray();
        DataCheck.QuotedRandom("FileHelpers read", Rows, _records.Length, i => (_records[i].Id, _records[i].Description, _records[i].Notes, _records[i].Category));

        // The written text has the header line (FileHelpers writes back the header it skipped) and, read again,
        // gives the same records: quoting on output is correct whatever its style.
        var written = WriteString();
        DataCheck.Equal(Rows + 1, DataCheck.TextLines(written).Length, "written line count");
        var again = _engine.ReadString(written);
        DataCheck.QuotedRandom("FileHelpers written", Rows, again.Length, i => (again[i].Id, again[i].Description, again[i].Notes, again[i].Category));
        WriteStream_Null();
    }

    [Benchmark(Baseline = true)]
    public string WriteString() => _engine.WriteString(_records);

    [Benchmark]
    public void WriteStream_Null() => _engine.WriteStream(TextWriter.Null, _records);
}
