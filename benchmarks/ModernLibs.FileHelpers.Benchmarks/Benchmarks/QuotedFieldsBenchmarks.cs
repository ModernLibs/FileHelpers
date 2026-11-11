// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using FlameCsv;
using ModernLibs.FileHelpers.Benchmarks.Data;
using nietras.SeparatedValues;
using RecordParser.Extensions;
using RecordParser.Parsers;
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

[CsvHelper.CsvRecord]
public sealed partial class QuotedCompetitorRow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Category { get; set; } = "";
}

/// <summary>
/// The quoted-fields comparison on the seeded dataset: the pattern of each row is random
/// (<see cref="DatasetGenerator.EnsureQuotedFieldsRandom"/>).
/// </summary>
public class QuotedFieldsBenchmarks : QuotedFieldsBenchmarksBase
{
    protected override string EnsureFile(string dataDir, int rows) => DatasetGenerator.EnsureQuotedFieldsRandom(dataDir, rows);

    protected override void CheckRows(string name, int count, Func<int, (int Id, string? Description, string? Notes, string? Category)> project) =>
        DataCheck.QuotedRandom(name, Rows, count, project);
}

/// <summary>
/// The quoted-fields comparison on the periodic dataset (row i uses pattern i % 10), kept for continuity with
/// the 4.9 reports (<see cref="DatasetGenerator.EnsureQuotedFields"/>).
/// </summary>
public class QuotedFieldsPeriodicBenchmarks : QuotedFieldsBenchmarksBase
{
    protected override string EnsureFile(string dataDir, int rows) => DatasetGenerator.EnsureQuotedFields(dataDir, rows);

    protected override void CheckRows(string name, int count, Func<int, (int Id, string? Description, string? Notes, string? Category)> project) =>
        DataCheck.QuotedFields(name, Rows, count, project);
}

[MemoryDiagnoser]
public abstract class QuotedFieldsBenchmarksBase
{
    private string _filePath = "";
    private FileHelperEngine<QuotedRecord> _engine = null!;
    private IVariableLengthReader<QuotedCompetitorRow> _recordParser = null!;

    // RecordParser must be told that fields are quoted (it then unquotes them, like the other readers).
    private static readonly VariableLengthReaderOptions s_recordParserOptions = new() { HasHeader = true, ContainsQuotedFields = true };

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    protected abstract string EnsureFile(string dataDir, int rows);

    protected abstract void CheckRows(string name, int count, Func<int, (int Id, string? Description, string? Notes, string? Category)> project);

    private void Prepare()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(QuotedFieldsBenchmarksBase).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = EnsureFile(dataDir, Rows);
        _engine = new FileHelperEngine<QuotedRecord>();
        _recordParser = RecordParserReaders.Quoted();
    }

    private void CheckCompetitor(string name, IReadOnlyList<QuotedCompetitorRow> rows) =>
        CheckRows(name, rows.Count, i => (rows[i].Id, rows[i].Description, rows[i].Notes, rows[i].Category));

    // Correctness guard: every benchmark must unquote the file it measures. Each setup runs only the guard of
    // its own benchmark, so no other reader runs in the measured process (see FixedWidthParseBenchmarks).
    [GlobalSetup(Target = nameof(ReadAllRows_FileHelpers))]
    public void SetupFileHelpers()
    {
        Prepare();
        var fh = ReadAllRows_FileHelpers();
        CheckRows("FileHelpers", fh.Length, i => (fh[i].Id, fh[i].Description, fh[i].Notes, fh[i].Category));
    }

    [GlobalSetup(Target = nameof(ReadAllRows_Sylvan))]
    public void SetupSylvan()
    {
        Prepare();
        CheckCompetitor("Sylvan", ReadAllRows_Sylvan());
    }

    [GlobalSetup(Target = nameof(ReadAllRows_CsvHelper))]
    public void SetupCsvHelper()
    {
        Prepare();
        CheckCompetitor("CsvHelper", ReadAllRows_CsvHelper());
    }

    [GlobalSetup(Target = nameof(ReadAllRows_Sep))]
    public void SetupSep()
    {
        Prepare();
        CheckCompetitor("Sep", ReadAllRows_Sep());
    }

    [GlobalSetup(Target = nameof(ReadAllRows_FlameCsv))]
    public void SetupFlameCsv()
    {
        Prepare();
        CheckCompetitor("FlameCsv", ReadAllRows_FlameCsv());
    }

    [GlobalSetup(Target = nameof(ReadAllRows_RecordParser))]
    public void SetupRecordParser()
    {
        Prepare();
        CheckCompetitor("RecordParser", ReadAllRows_RecordParser());
    }

    private void CheckDiscard(string name, long checksum) =>
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), checksum, name + " parse-and-discard checksum");

    [GlobalSetup(Target = nameof(FileHelpers_ParseAndDiscard))]
    public void SetupDiscardFileHelpers()
    {
        Prepare();
        CheckDiscard("FileHelpers", FileHelpers_ParseAndDiscard());
    }

    [GlobalSetup(Target = nameof(Sylvan_ParseAndDiscard))]
    public void SetupDiscardSylvan()
    {
        Prepare();
        CheckDiscard("Sylvan", Sylvan_ParseAndDiscard());
    }

    [GlobalSetup(Target = nameof(CsvHelper_ParseAndDiscard))]
    public void SetupDiscardCsvHelper()
    {
        Prepare();
        CheckDiscard("CsvHelper", CsvHelper_ParseAndDiscard());
    }

    [GlobalSetup(Target = nameof(Sep_ParseAndDiscard))]
    public void SetupDiscardSep()
    {
        Prepare();
        CheckDiscard("Sep", Sep_ParseAndDiscard());
    }

    [GlobalSetup(Target = nameof(FlameCsv_ParseAndDiscard))]
    public void SetupDiscardFlameCsv()
    {
        Prepare();
        CheckDiscard("FlameCsv", FlameCsv_ParseAndDiscard());
    }

    [GlobalSetup(Target = nameof(RecordParser_ParseAndDiscard))]
    public void SetupDiscardRecordParser()
    {
        Prepare();
        CheckDiscard("RecordParser", RecordParser_ParseAndDiscard());
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
        using var csv = CsvHelper.CsvReader.FromFile(_filePath, CsvHelper.CsvReaderOptions.Default);
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

    // Tier A competitors (T11): FlameCsv (UTF-8 bytes, source-generated binding, quotes removed by default) and
    // RecordParser (ContainsQuotedFields = true).

    [Benchmark]
    public IReadOnlyList<QuotedCompetitorRow> ReadAllRows_FlameCsv()
    {
        var results = new List<QuotedCompetitorRow>(Rows);
        using var stream = File.OpenRead(_filePath);
        foreach (var r in CsvReader.Read(stream, QuotedFlameMap.Default, CsvOptions<byte>.Default))
            results.Add(r);
        return results;
    }

    [Benchmark]
    public IReadOnlyList<QuotedCompetitorRow> ReadAllRows_RecordParser()
    {
        var results = new List<QuotedCompetitorRow>(Rows);
        using var reader = new StreamReader(_filePath);
        foreach (var r in reader.ReadRecords(_recordParser, s_recordParserOptions))
            results.Add(r);
        return results;
    }

    // Parse-and-discard (design B2): every record parsed and unquoted completely, none kept (see DiscardChecksum).

    [Benchmark]
    public long FlameCsv_ParseAndDiscard()
    {
        long checksum = 0;
        using var stream = File.OpenRead(_filePath);
        foreach (var r in CsvReader.Read(stream, QuotedFlameMap.Default, CsvOptions<byte>.Default))
            checksum += r.Id;
        return checksum;
    }

    [Benchmark]
    public long RecordParser_ParseAndDiscard()
    {
        long checksum = 0;
        using var reader = new StreamReader(_filePath);
        foreach (var r in reader.ReadRecords(_recordParser, s_recordParserOptions))
            checksum += r.Id;
        return checksum;
    }

    [Benchmark]
    public long FileHelpers_ParseAndDiscard()
    {
        long checksum = 0;
        _engine.ReadFile(_filePath, r => checksum += r.Id);
        return checksum;
    }

    [Benchmark]
    public long Sylvan_ParseAndDiscard()
    {
        long checksum = 0;
        using var reader = CsvDataReader.Create(_filePath);
        while (reader.Read())
        {
            var r = new QuotedCompetitorRow
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                Notes = reader.GetString(3),
                Category = reader.GetString(4),
            };
            checksum += r.Id;
        }
        return checksum;
    }

    [Benchmark]
    public long CsvHelper_ParseAndDiscard()
    {
        long checksum = 0;
        using var csv = CsvHelper.CsvReader.FromFile(_filePath, CsvHelper.CsvReaderOptions.Default);
        foreach (var r in csv.GetRecords<QuotedCompetitorRow>())
            checksum += r.Id;
        return checksum;
    }

    [Benchmark]
    public long Sep_ParseAndDiscard()
    {
        long checksum = 0;
        using var reader = Sep.New(',').Reader(o => o with { Unescape = true }).FromFile(_filePath);
        foreach (var row in reader)
        {
            var r = new QuotedCompetitorRow
            {
                Id = row[0].Parse<int>(),
                Name = row[1].ToString(),
                Description = row[2].ToString(),
                Notes = row[3].ToString(),
                Category = row[4].ToString(),
            };
            checksum += r.Id;
        }
        return checksum;
    }
}
