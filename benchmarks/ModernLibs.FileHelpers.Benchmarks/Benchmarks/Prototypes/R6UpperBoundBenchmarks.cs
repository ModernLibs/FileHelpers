using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Prototypes;

/// <summary>
/// Gate R6 (design 3.2): quoted parse-and-discard, <c>ReadFile(path, callback)</c> on the seeded quoted set, with the
/// generated descriptor (<see cref="R6_Current"/>, the same work as
/// <c>QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard</c>) and with the upper-bound descriptor
/// <see cref="QuotedPrecomputedEndsDescriptor"/> in the same engine (<see cref="R6_PrecomputedEnds"/>).
/// </summary>
/// <remarks>
/// Both return the sum of the Ids. Each setup reads the file once through its own engine and callback, checks every
/// record against the dataset (<see cref="DataCheck.QuotedRandom"/> and the Name column) and checks the checksum.
/// <c>--check-prototypes</c> compares the two paths' records field by field.
/// </remarks>
[MemoryDiagnoser]
public class R6UpperBoundBenchmarks
{
    private static readonly string[] s_names = ["Alpha", "Beta", "Gamma", "Delta", "Epsilon"];

    private string _path = "";
    private FileHelperEngine<QuotedRecord> _engine = null!;
    private QuotedPrecomputedEndsDescriptor _descriptor = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    /// <summary>Every record of <paramref name="path"/>, read with <c>ReadFile(path, callback)</c>, the benchmarks' read.</summary>
    internal static List<QuotedRecord> Collect(FileHelperEngine<QuotedRecord> engine, string path)
    {
        var records = new List<QuotedRecord>();
        engine.ReadFile(path, records.Add);
        return records;
    }

    private void CheckRecords(string name, List<QuotedRecord> records)
    {
        DataCheck.QuotedRandom(name, Rows, records.Count, i => (records[i].Id, records[i].Description, records[i].Notes, records[i].Category));
        for (int i = 0; i < records.Count; i++)
            DataCheck.Equal(s_names[i % s_names.Length], records[i].Name, $"{name} row {i} Name");
    }

    [GlobalSetup(Target = nameof(R6_Current))]
    public void SetupCurrent()
    {
        _path = DatasetGenerator.EnsureQuotedFieldsRandom(ComponentData.DataDir(), Rows);
        _engine = new FileHelperEngine<QuotedRecord>();
        CheckRecords(nameof(R6_Current), Collect(_engine, _path));
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), R6_Current(), nameof(R6_Current) + " checksum");
    }

    [GlobalSetup(Target = nameof(R6_PrecomputedEnds))]
    public void SetupPrecomputedEnds()
    {
        _path = DatasetGenerator.EnsureQuotedFieldsRandom(ComponentData.DataDir(), Rows);
        _descriptor = QuotedPrecomputedEndsDescriptor.FromFile(_path);
        _engine = PrototypeEngine.Create(_descriptor);
        _descriptor.Rewind();
        CheckRecords(nameof(R6_PrecomputedEnds), Collect(_engine, _path));
        DataCheck.Equal(Rows, _descriptor.RowsParsed, nameof(R6_PrecomputedEnds) + " rows parsed by the prototype");
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), R6_PrecomputedEnds(), nameof(R6_PrecomputedEnds) + " checksum");
        DataCheck.Equal(Rows, _descriptor.RowsParsed, nameof(R6_PrecomputedEnds) + " rows parsed by the prototype");
    }

    [Benchmark(Baseline = true)]
    public long R6_Current()
    {
        long checksum = 0;
        _engine.ReadFile(_path, r => checksum += r.Id);
        return checksum;
    }

    [Benchmark]
    public long R6_PrecomputedEnds()
    {
        _descriptor.Rewind();
        long checksum = 0;
        _engine.ReadFile(_path, r => checksum += r.Id);
        return checksum;
    }
}
