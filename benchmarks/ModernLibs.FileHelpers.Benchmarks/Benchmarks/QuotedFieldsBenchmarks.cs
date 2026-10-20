// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
using nietras.SeparatedValues;
using Sylvan.Data.Csv;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

// The file starts with a header line. Every text column may be RFC 4180 quoted (embedded delimiters,
// doubled quotes); the CSV readers unquote any field by default, so every text field is [FieldQuoted].
[DelimitedRecord(",")]
[IgnoreFirst]
public sealed class QuotedRecord
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted] public string Name { get; set; } = "";
    [FieldOrder(3), FieldQuoted] public string Description { get; set; } = "";
    [FieldOrder(4), FieldQuoted] public string Notes { get; set; } = "";
    [FieldOrder(5), FieldQuoted] public string Category { get; set; } = "";
}

public sealed class QuotedCompetitorRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Category { get; set; } = "";
}

[MemoryDiagnoser]
public class QuotedFieldsBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<QuotedRecord> _engine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(QuotedFieldsBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsureQuotedFields(dataDir, Rows);
        _engine = new FileHelperEngine<QuotedRecord>();

        // Correctness guard: every benchmark must unquote the file it measures.
        var fh = ReadAllRows_FileHelpers();
        DataCheck.QuotedFields("FileHelpers", Rows, fh.Length, i => (fh[i].Id, fh[i].Description, fh[i].Notes, fh[i].Category));
        foreach (var (name, rows) in new[] { ("Sylvan", ReadAllRows_Sylvan()), ("CsvHelper", ReadAllRows_CsvHelper()), ("Sep", ReadAllRows_Sep()) })
            DataCheck.QuotedFields(name, Rows, rows.Count, i => (rows[i].Id, rows[i].Description, rows[i].Notes, rows[i].Category));
    }

    [Benchmark(Baseline = true)]
    public QuotedRecord[] ReadAllRows_FileHelpers() => _engine.ReadFile(_filePath);

    [Benchmark]
    public IReadOnlyList<QuotedCompetitorRow> ReadAllRows_Sylvan()
    {
        var results = new List<QuotedCompetitorRow>(Rows);
        using var reader = CsvDataReader.Create(_filePath);
        while (reader.Read())
        {
            results.Add(new QuotedCompetitorRow
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                Notes = reader.GetString(3),
                Category = reader.GetString(4),
            });
        }
        return results;
    }

    [Benchmark]
    public IReadOnlyList<QuotedCompetitorRow> ReadAllRows_CsvHelper()
    {
        using var reader = new System.IO.StreamReader(_filePath);
        using var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);
        return csv.GetRecords<QuotedCompetitorRow>().ToList();
    }

    [Benchmark]
    public IReadOnlyList<QuotedCompetitorRow> ReadAllRows_Sep()
    {
        var results = new List<QuotedCompetitorRow>(Rows);
        // Sep only splits on the separator by default; Unescape = true removes the quotes and un-doubles
        // embedded quotes, as the other libraries do.
        using var reader = Sep.New(',').Reader(o => o with { Unescape = true }).FromFile(_filePath);
        foreach (var row in reader)
        {
            results.Add(new QuotedCompetitorRow
            {
                Id = row[0].Parse<int>(),
                Name = row[1].ToString(),
                Description = row[2].ToString(),
                Notes = row[3].ToString(),
                Category = row[4].ToString(),
            });
        }
        return results;
    }
}
