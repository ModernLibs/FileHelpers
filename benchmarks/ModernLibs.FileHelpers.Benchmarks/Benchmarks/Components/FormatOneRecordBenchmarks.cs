using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Benchmarks.Prototypes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;

/// <summary>
/// One-record <c>Format</c> (design B3): 1,000 PackageAssets records, each formatted on its own into the engine's
/// <see cref="RentedBufferWriter"/> (reset before each record, as the engine does), with no <c>TextWriter</c> and no
/// result string. <see cref="FormatRecord_Current"/> calls the legacy <c>Format</c> of the generated descriptor and
/// <see cref="FormatRecord_SingleSpan"/> the G2 prototype <see cref="PackageAssetSpanFormat"/>, both through
/// <see cref="IRecordDescriptor{T}"/>; <c>FormatRecord_Writer</c> (4.10 builds only) calls the record writer of the
/// generated descriptor as the engine does, through <c>RecordWriteBuffer.Format</c> (T23). It separates the cost of
/// <c>Format</c> from the rest of <c>WriteString</c> (<see cref="G2PrototypeBenchmarks"/>).
/// </summary>
/// <remarks>
/// Each returns the number of characters written. Each setup formats every record with the same method and compares
/// it with its line in the file. From T23 on the legacy <c>Format</c> of a generated descriptor is the adapter
/// <c>RecordFormatting.Format</c>, which formats into the engine's buffer through the record writer as well: the
/// difference between <see cref="FormatRecord_Current"/> and <c>FormatRecord_Writer</c> is the adapter call.
/// </remarks>
[MemoryDiagnoser]
public class FormatOneRecordBenchmarks : IDisposable
{
    private const int Records = 1_000;

    private PackageAsset[] _records = [];
    private string[] _lines = [];
    private IRecordDescriptor<PackageAsset> _descriptor = null!;
    private RentedBufferWriter _buffer = null!;
#if !FILEHELPERS_BASELINE
    private IRecordWriterDescriptor<PackageAsset> _writerDescriptor = null!;
#endif

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

#if !FILEHELPERS_BASELINE
    // IRecordWriterDescriptor<T> and RecordWriteBuffer.Format are new in 4.10 (T22, T23): a build against the 4.9.1
    // package or source (FILEHELPERS_BASELINE) has no FormatRecord_Writer.
    [GlobalSetup(Target = nameof(FormatRecord_Writer))]
    public void SetupWriter()
    {
        Prepare(FileHelperRegistry<PackageAsset>.Descriptor);
        _writerDescriptor = (IRecordWriterDescriptor<PackageAsset>)_descriptor;
        DataCheck.Equal(_lines.Length, _records.Length, nameof(FormatRecord_Writer) + " record count");
        long expected = 0;
        for (int i = 0; i < _records.Length; i++)
        {
            _buffer.Reset();
            _buffer.Format(_writerDescriptor, _records[i]);
            DataCheck.Equal(_lines[i], _buffer.WrittenSpan.ToString(), $"{nameof(FormatRecord_Writer)} record {i}");
            expected += _lines[i].Length;
        }
        DataCheck.Equal(expected, FormatRecord_Writer(), nameof(FormatRecord_Writer) + " characters written");
    }
#endif

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

#if !FILEHELPERS_BASELINE
    [Benchmark]
    public long FormatRecord_Writer()
    {
        long written = 0;
        var descriptor = _writerDescriptor;
        var buffer = _buffer;
        foreach (var record in _records)
        {
            buffer.Reset();
            buffer.Format(descriptor, record);
            written += buffer.WrittenSpan.Length;
        }
        return written;
    }
#endif

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
