// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

/// <summary>Writes the records of the fixed-width dataset (<see cref="FixedRecord"/>, 80 characters per record).</summary>
[MemoryDiagnoser]
public class FixedWidthWriteBenchmarks
{
    private FixedRecord[] _records = null!;
    private FileHelperEngine<FixedRecord> _engine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(FixedWidthWriteBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        var file = DatasetGenerator.EnsureFixedWidth(dataDir, Rows);

        _engine = new FileHelperEngine<FixedRecord>();
        var records = new List<FixedRecord>(Rows);
        _engine.ReadFile(file, records.Add);
        _records = records.ToArray();

        // The written text must be the dataset file, line for line.
        var fileLines = DataCheck.DataLines(file, hasHeader: false);
        DataCheck.Equal(fileLines.Length, _records.Length, "FileHelpers read row count");
        var lines = DataCheck.TextLines(WriteString());
        DataCheck.Equal(fileLines.Length, lines.Length, "FileHelpers written line count");
        for (int row = 0; row < lines.Length; row++)
        {
            if (!string.Equals(fileLines[row], lines[row], StringComparison.Ordinal))
                DataCheck.Equal(fileLines[row], lines[row], $"FileHelpers written line {row}");
        }
        WriteStream_Null();
    }

    [Benchmark(Baseline = true)]
    public string WriteString() => _engine.WriteString(_records);

    [Benchmark]
    public void WriteStream_Null() => _engine.WriteStream(TextWriter.Null, _records);
}
