using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Prototypes;

/// <summary>
/// Gate R6 (design 3.2): quoted parse-and-discard, <c>ReadFile(path, callback)</c> on the seeded quoted set, with the
/// generated descriptor (<see cref="R6_Current"/>, the same work as
/// <c>QuotedFieldsBenchmarks.FileHelpers_ParseAndDiscard</c>) and, in the same engine, with the two upper-bound
/// descriptors that take precomputed field ends.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="R6_PrecomputedEnds"/> is the gate (lead ruling R32): precomputed ends with today's emitted strip pass
/// (<see cref="QuotedPrecomputedEndsEmittedUnquote"/>), R6 alone. <see cref="R6_PrecomputedEnds_FastUnquote"/> is
/// informative: precomputed ends with the fastest unquote (<see cref="QuotedPrecomputedEndsFastUnquote"/>), R6 plus R3.
/// The gate ratio is <c>R6_PrecomputedEnds / R6_Current</c> within one 4.10 build; the Step 0 report gives both
/// prototype ratios.
/// </para>
/// <para>
/// All three return the sum of the Ids. Each setup reads the file once through its own engine and callback, checks
/// every record against the dataset (<see cref="DataCheck.QuotedRandom"/> and the Name column), that the prototype
/// parsed every row, and the checksum. <c>--check-prototypes</c> compares both prototypes' records with
/// <c>R6_Current</c> field by field.
/// </para>
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

    private void SetupPrototype(string name, Func<string, QuotedPrecomputedEndsDescriptor> create, Func<long> benchmark)
    {
        _path = DatasetGenerator.EnsureQuotedFieldsRandom(ComponentData.DataDir(), Rows);
        _descriptor = create(_path);
        _engine = PrototypeEngine.Create<QuotedRecord>(_descriptor);
        _descriptor.Rewind();
        CheckRecords(name, Collect(_engine, _path));
        DataCheck.Equal(Rows, _descriptor.RowsParsed, name + " rows parsed by the prototype");
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), benchmark(), name + " checksum");
        DataCheck.Equal(Rows, _descriptor.RowsParsed, name + " rows parsed by the prototype");
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
    public void SetupPrecomputedEnds() =>
        SetupPrototype(nameof(R6_PrecomputedEnds), path => new QuotedPrecomputedEndsEmittedUnquote(path), R6_PrecomputedEnds);

    [GlobalSetup(Target = nameof(R6_PrecomputedEnds_FastUnquote))]
    public void SetupPrecomputedEndsFastUnquote() =>
        SetupPrototype(nameof(R6_PrecomputedEnds_FastUnquote), path => new QuotedPrecomputedEndsFastUnquote(path), R6_PrecomputedEnds_FastUnquote);

    [Benchmark(Baseline = true)]
    public long R6_Current()
    {
        long checksum = 0;
        _engine.ReadFile(_path, r => checksum += r.Id);
        return checksum;
    }

    /// <summary>The gate: precomputed ends, today's strip pass (R6 alone).</summary>
    [Benchmark]
    public long R6_PrecomputedEnds()
    {
        _descriptor.Rewind();
        long checksum = 0;
        _engine.ReadFile(_path, r => checksum += r.Id);
        return checksum;
    }

    /// <summary>Informative: precomputed ends, fastest unquote (R6 plus R3).</summary>
    [Benchmark]
    public long R6_PrecomputedEnds_FastUnquote()
    {
        _descriptor.Rewind();
        long checksum = 0;
        _engine.ReadFile(_path, r => checksum += r.Id);
        return checksum;
    }
}
