// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using FlatFiles;
using FlatFiles.TypeMapping;
using ModernLibs.FileHelpers.Benchmarks.Data;
using RecordParser.Extensions;
using RecordParser.Parsers;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

/// <summary>
/// The tier B fixed-width libraries (T11), each reading the 80-character fixed-width file into <see cref="FixedRecord"/>
/// with the padding kept. Sylvan.Data is not here: it has no fixed-width reader (see BENCHMARKS.md).
/// </summary>
[MemoryDiagnoser]
public class TierBFixedWidthBenchmarks
{
    private string _filePath = "";
    private IFixedLengthReader<FixedRecord> _recordParser = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    private void Prepare()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(TierBFixedWidthBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsureFixedWidth(dataDir, Rows);
        _recordParser = RecordParserReaders.Fixed();
    }

    private void Check(string name, IReadOnlyList<FixedRecord> rows) =>
        DataCheck.FixedWidth(name, DataCheck.DataLines(_filePath, hasHeader: false), rows.Count, i => FixedRecordText.Of(rows[i]));

    [GlobalSetup(Target = nameof(FlatFiles_ReadAll))]
    public void SetupFlatFiles() { Prepare(); Check("FlatFiles", FlatFiles_ReadAll()); }

    [GlobalSetup(Target = nameof(RecordParser_ReadAll))]
    public void SetupRecordParser() { Prepare(); Check("RecordParser", RecordParser_ReadAll()); }

    [Benchmark]
    public IReadOnlyList<FixedRecord> FlatFiles_ReadAll()
    {
        var mapper = FixedLengthTypeMapper.Define(() => new FixedRecord());
        mapper.Property(x => x.Id, new Window(8)).Trim(false);
        mapper.Property(x => x.Name, new Window(12)).Trim(false);
        mapper.Property(x => x.NumericField, new Window(10)).Trim(false);
        mapper.Property(x => x.Description, new Window(15)).Trim(false);
        mapper.Property(x => x.DecimalField, new Window(20)).Trim(false);
        mapper.Property(x => x.Code, new Window(5)).Trim(false);
        mapper.Property(x => x.Suffix, new Window(10)).Trim(false);
        using var reader = new StreamReader(_filePath);
        return mapper.Read(reader, new FixedLengthOptions()).ToList();
    }

    [Benchmark]
    public IReadOnlyList<FixedRecord> RecordParser_ReadAll()
    {
        var results = new List<FixedRecord>(Rows);
        using var reader = new StreamReader(_filePath);
        foreach (var r in reader.ReadRecords(new FixedLengthReaderOptions<FixedRecord> { Parser = _recordParser.Parse }))
            results.Add(r);
        return results;
    }
}
