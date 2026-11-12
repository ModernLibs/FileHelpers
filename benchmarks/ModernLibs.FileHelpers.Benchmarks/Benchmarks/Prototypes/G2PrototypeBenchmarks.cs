using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Prototypes;

/// <summary>
/// Gate G2 (design 3.2): <c>engine.WriteString(records)</c> of 100,000 PackageAssets records, with the generated
/// <c>Format</c> (<see cref="G2_Current"/>) and with the single-span prototype <see cref="PackageAssetSpanFormat"/> in
/// the same engine (<see cref="G2_SingleSpan"/>).
/// </summary>
/// <remarks>
/// The two benchmark bodies are the same call; only the engine's descriptor differs (see <see cref="PrototypeEngine"/>),
/// so the engine's write pipeline (header, the rented record buffer, the newline, <c>PooledStringWriter</c>, the result
/// string) is measured in both. Each setup loads the records with a separate engine through the callback read (no
/// pooled record buffer stays in the process), copies the header it read, writes once and compares the text with the
/// file line by line (<see cref="DataCheck.WrittenPackageAssets"/>); the prototype's setup also checks that the
/// prototype formatted every record (<see cref="PackageAssetSpanFormat.FormatCalls"/>). <c>--check-prototypes</c>
/// compares the two texts. The record is still formatted into the engine's record buffer and then copied into the
/// <c>PooledStringWriter</c>, so the gate excludes P5 (formatting straight into the chunk, design 5.1).
/// </remarks>
[MemoryDiagnoser]
public class G2PrototypeBenchmarks
{
    private PackageAsset[] _records = [];
    private FileHelperEngine<PackageAsset> _engine = null!;
    private string _sourceFile = "";

    [Params(100_000)]
    public int Rows { get; set; }

    /// <summary>The records and header of <paramref name="path"/>, read with a separate engine.</summary>
    internal static (PackageAsset[] Records, string? Header) Load(string path)
    {
        var reader = new FileHelperEngine<PackageAsset>();
        var records = new List<PackageAsset>();
        reader.ReadFile(path, records.Add);
        return (records.ToArray(), reader.HeaderText);
    }

    private string? Prepare()
    {
        _sourceFile = DatasetGenerator.EnsurePackageAssets(ComponentData.DataDir(), Rows);
        (_records, string? header) = Load(_sourceFile);
        DataCheck.PackageAssets("G2 records", DataCheck.DataLines(_sourceFile, hasHeader: true), _records.Length, i => PackageAssetColumns.Of(_records[i]));
        return header;
    }

    [GlobalSetup(Target = nameof(G2_Current))]
    public void SetupCurrent()
    {
        string? header = Prepare();
        _engine = new FileHelperEngine<PackageAsset> { HeaderText = header, NewLineForWrite = "\r\n" };
        DataCheck.WrittenPackageAssets(nameof(G2_Current), DataCheck.DataLines(_sourceFile, hasHeader: false), G2_Current());
    }

    [GlobalSetup(Target = nameof(G2_SingleSpan))]
    public void SetupSingleSpan()
    {
        string? header = Prepare();
        var prototype = new PackageAssetSpanFormat();
        _engine = PrototypeEngine.Create<PackageAsset>(prototype);
        _engine.HeaderText = header;
        _engine.NewLineForWrite = "\r\n";
        DataCheck.WrittenPackageAssets(nameof(G2_SingleSpan), DataCheck.DataLines(_sourceFile, hasHeader: false), G2_SingleSpan());
        // Proof that the engine wrote through the prototype, not through the generated Format.
        DataCheck.Equal((long)Rows, prototype.FormatCalls, nameof(G2_SingleSpan) + " records formatted by the prototype");
    }

    [Benchmark(Baseline = true)]
    public string G2_Current() => _engine.WriteString(_records);

    [Benchmark]
    public string G2_SingleSpan() => _engine.WriteString(_records);
}
