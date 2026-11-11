// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
using RecordParser.Extensions;
using RecordParser.Parsers;
// The FileHelpers 3.x NuGet package. It must be named through this alias: inside the
// ModernLibs.FileHelpers.* namespaces a bare "FileHelpers." resolves to ModernLibs.FileHelpers, which
// made the "Legacy" benchmarks of earlier milestones measure this library instead of 3.x.
using Legacy = global::FileHelpers;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

// FileHelpers 3.x fixed-width record type (the 3.x default FixedMode is ExactLength, like FixedRecord).
[Legacy.FixedLengthRecord]
public sealed class OldFixedRecord
{
    [Legacy.FieldFixedLength(8)] public string Id = "";
    [Legacy.FieldFixedLength(12)] public string Name = "";
    [Legacy.FieldFixedLength(10)] public string NumericField = "";
    [Legacy.FieldFixedLength(15)] public string Description = "";
    [Legacy.FieldFixedLength(20)] public string DecimalField = "";
    [Legacy.FieldFixedLength(5)] public string Code = "";
    [Legacy.FieldFixedLength(10)] public string Suffix = "";
}

[MemoryDiagnoser]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "The 3.x async engine is closed after every operation and disposed by the GlobalCleanup of its benchmark.")]
public class FixedWidthComparisonBenchmarks
{
    private string _filePath = "";
    private FileHelperEngine<FixedRecord> _modernClassEngine = null!;
    private FileHelperEngine<FixedRecordStruct> _modernStructEngine = null!;
    private Legacy.FileHelperEngine<OldFixedRecord> _oldEngine = null!;
    private Legacy.FileHelperAsyncEngine<OldFixedRecord> _oldAsyncEngine = null!;
    private IFixedLengthReader<FixedRecord> _recordParser = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    private void Prepare()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(FixedWidthComparisonBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = DatasetGenerator.EnsureFixedWidth(dataDir, Rows);
        _modernClassEngine = new FileHelperEngine<FixedRecord>();
        _modernStructEngine = new FileHelperEngine<FixedRecordStruct>();
        _oldEngine = new Legacy.FileHelperEngine<OldFixedRecord>();
        _recordParser = RecordParserReaders.Fixed();
    }

    // Correctness guard: every benchmark must parse the file it measures. Each setup runs only the guard of
    // its own benchmark, so no other reader runs in the measured process (see FixedWidthParseBenchmarks).
    [GlobalSetup(Target = nameof(ReadAllRows_ModernClass))]
    public void SetupModernClass()
    {
        Prepare();
        var cls = ReadAllRows_ModernClass();
        DataCheck.FixedWidth("ModernLibs class", DataCheck.DataLines(_filePath, hasHeader: false), cls.Length, i => FixedRecordText.Of(cls[i]));
    }

    [GlobalSetup(Target = nameof(ReadAllRows_ModernStruct))]
    public void SetupModernStruct()
    {
        Prepare();
        var str = ReadAllRows_ModernStruct();
        DataCheck.FixedWidth("ModernLibs struct", DataCheck.DataLines(_filePath, hasHeader: false), str.Length, i => FixedRecordText.Of(str[i]));
    }

    [GlobalSetup(Target = nameof(ReadAllRows_Legacy))]
    public void SetupLegacy()
    {
        Prepare();
        var legacy = ReadAllRows_Legacy();
        DataCheck.FixedWidth("FileHelpers 3.x", DataCheck.DataLines(_filePath, hasHeader: false), legacy.Length, i =>
        {
            var r = legacy[i];
            return string.Concat(r.Id, r.Name, r.NumericField, r.Description, r.DecimalField, r.Code, r.Suffix);
        });
        DataCheck.Equal("FileHelpers", _oldEngine.GetType().Assembly.GetName().Name, "3.x engine assembly");
    }

    [GlobalSetup(Target = nameof(Modern_ParseAndDiscard))]
    public void SetupDiscardModern()
    {
        Prepare();
        DataCheck.Equal(DiscardChecksum.FixedIdLastChars(DataCheck.DataLines(_filePath, hasHeader: false)),
            Modern_ParseAndDiscard(), "ModernLibs.FileHelpers parse-and-discard checksum");
    }

    [GlobalSetup(Target = nameof(Legacy_ParseAndDiscard))]
    public void SetupDiscardLegacy()
    {
        Prepare();
        _oldAsyncEngine = new Legacy.FileHelperAsyncEngine<OldFixedRecord>();
        DataCheck.Equal(DiscardChecksum.FixedIdLastChars(DataCheck.DataLines(_filePath, hasHeader: false)),
            Legacy_ParseAndDiscard(), "FileHelpers 3.x parse-and-discard checksum");
        DataCheck.Equal("FileHelpers", _oldAsyncEngine.GetType().Assembly.GetName().Name, "3.x async engine assembly");
    }

    [GlobalSetup(Target = nameof(ReadAllRows_RecordParser))]
    public void SetupRecordParser()
    {
        Prepare();
        var rows = ReadAllRows_RecordParser();
        DataCheck.FixedWidth("RecordParser", DataCheck.DataLines(_filePath, hasHeader: false), rows.Count, i => FixedRecordText.Of(rows[i]));
    }

    [GlobalSetup(Target = nameof(RecordParser_ParseAndDiscard))]
    public void SetupDiscardRecordParser()
    {
        Prepare();
        DataCheck.Equal(DiscardChecksum.FixedIdLastChars(DataCheck.DataLines(_filePath, hasHeader: false)),
            RecordParser_ParseAndDiscard(), "RecordParser parse-and-discard checksum");
    }

    [GlobalCleanup(Target = nameof(Legacy_ParseAndDiscard))]
    public void CleanupDiscardLegacy() => ((IDisposable)_oldAsyncEngine).Dispose();

    [Benchmark(Baseline = true)]
    public FixedRecord[] ReadAllRows_ModernClass() => _modernClassEngine.ReadFile(_filePath);

    [Benchmark]
    public FixedRecordStruct[] ReadAllRows_ModernStruct() => _modernStructEngine.ReadFile(_filePath);

    [Benchmark]
    public OldFixedRecord[] ReadAllRows_Legacy() => _oldEngine.ReadFile(_filePath);

    // Parse-and-discard (design B2): every record (class record) parsed completely, none kept (see
    // DiscardChecksum). FileHelpers 3.x has no callback read; its record-by-record read loop is
    // FileHelperAsyncEngine.ReadNext.

    [Benchmark]
    public long Modern_ParseAndDiscard()
    {
        long checksum = 0;
        _modernClassEngine.ReadFile(_filePath, r => checksum += r.Id[^1]);
        return checksum;
    }

    [Benchmark]
    public long Legacy_ParseAndDiscard()
    {
        long checksum = 0;
        _oldAsyncEngine.BeginReadFile(_filePath);
        try
        {
            while (_oldAsyncEngine.ReadNext() is { } r)
                checksum += r.Id[^1];
        }
        finally
        {
            _oldAsyncEngine.Close();
        }
        return checksum;
    }

    // Tier A competitor (T11). RecordParser maps each field by offset and length into the same FixedRecord
    // (padding kept, as the other readers). FlameCsv has no fixed-width mode, so it is not in this class.

    [Benchmark]
    public IReadOnlyList<FixedRecord> ReadAllRows_RecordParser()
    {
        var results = new List<FixedRecord>(Rows);
        using var reader = new StreamReader(_filePath);
        foreach (var r in reader.ReadRecords(new FixedLengthReaderOptions<FixedRecord> { Parser = _recordParser.Parse }))
            results.Add(r);
        return results;
    }

    [Benchmark]
    public long RecordParser_ParseAndDiscard()
    {
        long checksum = 0;
        using var reader = new StreamReader(_filePath);
        foreach (var r in reader.ReadRecords(new FixedLengthReaderOptions<FixedRecord> { Parser = _recordParser.Parse }))
            checksum += r.Id[^1];
        return checksum;
    }
}
