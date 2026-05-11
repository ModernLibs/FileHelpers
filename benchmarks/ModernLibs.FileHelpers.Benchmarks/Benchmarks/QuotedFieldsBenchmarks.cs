// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
using nietras.SeparatedValues;
using Sylvan.Data.Csv;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

[DelimitedRecord(",")]
public sealed class QuotedRecord
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = "";
    [FieldOrder(3)] public string Description { get; set; } = "";
    [FieldOrder(4)] public string Notes { get; set; } = "";
    [FieldOrder(5)] public string Category { get; set; } = "";
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
    }

    [Benchmark(Baseline = true)]
    public QuotedRecord[] ReadAllRows_FileHelpers() => _engine.ReadFile(_filePath);

    [Benchmark]
    public List<QuotedCompetitorRow> ReadAllRows_Sylvan()
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
    public List<QuotedCompetitorRow> ReadAllRows_CsvHelper()
    {
        using var reader = new System.IO.StreamReader(_filePath);
        using var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);
        return csv.GetRecords<QuotedCompetitorRow>().ToList();
    }

    [Benchmark]
    public List<QuotedCompetitorRow> ReadAllRows_Sep()
    {
        var results = new List<QuotedCompetitorRow>(Rows);
        using var reader = Sep.New(',').Reader().FromFile(_filePath);
        foreach (var row in reader)
        {
            results.Add(new QuotedCompetitorRow
            {
                Id = row["Id"].Parse<int>(),
                Name = row["Name"].ToString(),
                Description = row["Description"].ToString(),
                Notes = row["Notes"].ToString(),
                Category = row["Category"].ToString(),
            });
        }
        return results;
    }
}
