using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Benchmarks.Prototypes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;

/// <summary>
/// One-record <c>Format</c> (design B3): 1,000 PackageAssets records, each formatted on its own into the engine's
/// <see cref="RentedBufferWriter"/> (reset before each record, as the engine does), with no <c>TextWriter</c> and no
/// result string. <see cref="FormatRecord_Current"/> calls the generated descriptor, <see cref="FormatRecord_SingleSpan"/>
/// the G2 prototype <see cref="PackageAssetSpanFormat"/>; both through <see cref="IRecordDescriptor{T}"/>, as the
/// engine calls them. It separates the cost of <c>Format</c> from the rest of <c>WriteString</c>
/// (<see cref="G2PrototypeBenchmarks"/>).
/// </summary>
/// <remarks>
/// Both return the number of characters written. Each setup formats every record with the same method and compares
/// it with its line in the file.
/// </remarks>
[MemoryDiagnoser]
public class FormatOneRecordBenchmarks : IDisposable
{
    private const int Records = 1_000;

    private PackageAsset[] _records = [];
    private string[] _lines = [];
    private IRecordDescriptor<PackageAsset> _descriptor = null!;
    private RentedBufferWriter _buffer = null!;

    private void Prepare(IRecordDescriptor<PackageAsset> descriptor)
    {
        string path = DatasetGenerator.EnsurePackageAssets(ComponentData.DataDir(), Records);
        var records = new List<PackageAsset>(Records);
        new FileHelperEngine<PackageAsset>().ReadFile(path, records.Add);
        _records = records.ToArray();
        _lines = DataCheck.DataLines(path, hasHeader: true);
        _descriptor = descriptor;
        _buffer?.Dispose();
        _buffer = new RentedBufferWriter(4096);
    }

    private void Check(string name, long checksum)
    {
        DataCheck.Equal(_lines.Length, _records.Length, name + " record count");
        long expected = 0;
        for (int i = 0; i < _records.Length; i++)
        {
            _buffer.Reset();
            _descriptor.Format(_records[i], _buffer);
            DataCheck.Equal(_lines[i], _buffer.WrittenSpan.ToString(), $"{name} record {i}");
            expected += _lines[i].Length;
        }
        DataCheck.Equal(expected, checksum, name + " characters written");
    }

    [GlobalSetup(Target = nameof(FormatRecord_Current))]
    public void SetupCurrent()
    {
        Prepare(FileHelperRegistry<PackageAsset>.Descriptor);
        Check(nameof(FormatRecord_Current), FormatRecord_Current());
    }

    [GlobalSetup(Target = nameof(FormatRecord_SingleSpan))]
    public void SetupSingleSpan()
    {
        Prepare(new PackageAssetSpanFormat());
        Check(nameof(FormatRecord_SingleSpan), FormatRecord_SingleSpan());
    }

    [GlobalCleanup]
    public void Cleanup() => Dispose();

    public void Dispose()
    {
        _buffer?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public long FormatRecord_Current() => FormatAll();

    [Benchmark]
    public long FormatRecord_SingleSpan() => FormatAll();

    private long FormatAll()
    {
        long written = 0;
        var descriptor = _descriptor;
        var buffer = _buffer;
        foreach (var record in _records)
        {
            buffer.Reset();
            descriptor.Format(record, buffer);
            written += buffer.WrittenSpan.Length;
        }
        return written;
    }
}
