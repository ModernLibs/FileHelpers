// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004

using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

/// <summary>
/// The async reads of <see cref="FileHelperEngine{T}"/> on the 25-column file, for the runtime-async A/B of design D6
/// (<c>net11-ra-on</c> against <c>net11-ra-off</c>). <see cref="AsyncStreamingBenchmarks"/> measures
/// <c>MultiRecordAsyncEngine</c> only.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="Engine_ReadAsync_Path"/>: <c>await foreach</c> over <c>ReadAsync(path)</c>, the sum of the Ids.</item>
/// <item><see cref="Engine_ReadFileAsync_Callback"/>: <c>ReadFileAsync(path, callback)</c>, the sum of the Ids.</item>
/// <item><see cref="Engine_ReadAsync_Path_ExceptionEvery100"/>: <c>await foreach</c> over <c>ReadAsync(path)</c> of
/// the file where every 100th row has a bad integer
/// (<see cref="DatasetGenerator.EnsurePackageAssetsBadEvery100"/>), with <see cref="ErrorMode.SaveAndContinue"/>: one
/// conversion exception is thrown and caught per bad row, inside the async enumerator. Returns
/// <c>ErrorManager.ErrorCount</c>, which the setup checks is <c>Rows / 100</c>.</item>
/// <item><see cref="Engine_WriteStreamAsync_Null"/>: <c>WriteStreamAsync(Stream.Null, records)</c> of the dataset's
/// records, read once in the setup. The setup checks one call's output on a <see cref="MemoryStream"/>: its length is
/// the encoded <c>WriteString</c> text plus the preamble. Returns <c>TotalRecords</c>.</item>
/// <item><see cref="Engine_ReadAsync_Stream"/>, <see cref="Engine_ReadAsync_TextReader"/>: <c>await foreach</c> over
/// <c>ReadAsync(Stream)</c> / <c>ReadAsync(TextReader)</c> of the same file held in memory (a new
/// <see cref="MemoryStream"/> per call), the sum of the Ids.</item>
/// <item><see cref="Engine_WriteStreamAsync_Null_SourceThrows"/>: <c>WriteStreamAsync(Stream.Null, records)</c> where
/// the source yields 3 records and then throws; <c>Rows / 100</c> such calls per invocation, each exception caught.
/// The write path has no per-record error mode, so the fault is one exception per call, and the benchmark is
/// dominated by the fault path (catch, flush of the pending records, rethrow). The setup checks one call's output on
/// a <see cref="MemoryStream"/> (preamble plus the 3 records), <c>TotalRecords == 0</c>, and the count caught,
/// <c>Rows / 100</c>, which is what the method returns.</item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
public class EngineAsyncBenchmarks
{
    private string _path = "";
    private FileHelperEngine<PackageAsset> _engine = null!;
    private PackageAsset[] _records = [];
    private byte[] _bytes = [];

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup(Target = nameof(Engine_ReadAsync_Path))]
    public void SetupReadAsync()
    {
        _path = DatasetGenerator.EnsurePackageAssets(ComponentData.DataDir(), Rows);
        _engine = new FileHelperEngine<PackageAsset>();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), Engine_ReadAsync_Path().GetAwaiter().GetResult(), nameof(Engine_ReadAsync_Path) + " checksum");
    }

    [GlobalSetup(Target = nameof(Engine_ReadFileAsync_Callback))]
    public void SetupReadFileAsync()
    {
        _path = DatasetGenerator.EnsurePackageAssets(ComponentData.DataDir(), Rows);
        _engine = new FileHelperEngine<PackageAsset>();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), Engine_ReadFileAsync_Callback().GetAwaiter().GetResult(), nameof(Engine_ReadFileAsync_Callback) + " checksum");
    }

    [GlobalSetup(Target = nameof(Engine_ReadAsync_Path_ExceptionEvery100))]
    public void SetupExceptionEvery100()
    {
        _path = DatasetGenerator.EnsurePackageAssetsBadEvery100(ComponentData.DataDir(), Rows);
        _engine = new FileHelperEngine<PackageAsset> { ErrorMode = ErrorMode.SaveAndContinue };
        DataCheck.Equal(Rows / DatasetGenerator.BadRowEvery, Engine_ReadAsync_Path_ExceptionEvery100().GetAwaiter().GetResult(), nameof(Engine_ReadAsync_Path_ExceptionEvery100) + " ErrorCount");
        DataCheck.Equal(Rows - (Rows / DatasetGenerator.BadRowEvery), _engine.TotalRecords, nameof(Engine_ReadAsync_Path_ExceptionEvery100) + " records read");
        var first = _engine.ErrorManager.Errors[0];
        DataCheck.Equal(DatasetGenerator.BadRowEvery + 1, first.LineNumber, nameof(Engine_ReadAsync_Path_ExceptionEvery100) + " line of the first error (after the header)");
    }

    [GlobalSetup(Target = nameof(Engine_WriteStreamAsync_Null))]
    public async Task SetupWriteStreamAsync()
    {
        _engine = new FileHelperEngine<PackageAsset>();
        _records = _engine.ReadFile(DatasetGenerator.EnsurePackageAssets(ComponentData.DataDir(), Rows));
        DataCheck.Equal(Rows, _records.Length, nameof(Engine_WriteStreamAsync_Null) + " records");

        using var ms = new MemoryStream();
        await _engine.WriteStreamAsync(ms, _records);
        long expected = _engine.Encoding.GetPreamble().Length + _engine.Encoding.GetByteCount(_engine.WriteString(_records));
        DataCheck.Equal(expected, ms.Length, nameof(Engine_WriteStreamAsync_Null) + " output length");
        DataCheck.Equal(Rows, await Engine_WriteStreamAsync_Null(), nameof(Engine_WriteStreamAsync_Null) + " TotalRecords");
    }

    [GlobalSetup(Target = nameof(Engine_ReadAsync_Stream))]
    public void SetupReadAsyncStream()
    {
        _bytes = File.ReadAllBytes(DatasetGenerator.EnsurePackageAssets(ComponentData.DataDir(), Rows));
        _engine = new FileHelperEngine<PackageAsset>();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), Engine_ReadAsync_Stream().GetAwaiter().GetResult(), nameof(Engine_ReadAsync_Stream) + " checksum");
    }

    [GlobalSetup(Target = nameof(Engine_ReadAsync_TextReader))]
    public void SetupReadAsyncTextReader()
    {
        _bytes = File.ReadAllBytes(DatasetGenerator.EnsurePackageAssets(ComponentData.DataDir(), Rows));
        _engine = new FileHelperEngine<PackageAsset>();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), Engine_ReadAsync_TextReader().GetAwaiter().GetResult(), nameof(Engine_ReadAsync_TextReader) + " checksum");
    }

    [GlobalSetup(Target = nameof(Engine_WriteStreamAsync_Null_SourceThrows))]
    public async Task SetupWriteStreamAsyncSourceThrows()
    {
        _engine = new FileHelperEngine<PackageAsset>();
        _records = _engine.ReadFile(DatasetGenerator.EnsurePackageAssets(ComponentData.DataDir(), Rows));
        DataCheck.Equal(Rows, _records.Length, nameof(Engine_WriteStreamAsync_Null_SourceThrows) + " records");

        // One failing call: the records before the fault reach the stream (preamble included); TotalRecords stays 0.
        using var ms = new MemoryStream();
        bool threw = false;
        try
        {
            await _engine.WriteStreamAsync(ms, ThrowAfterFewRecords(_records));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        DataCheck.That(threw, nameof(Engine_WriteStreamAsync_Null_SourceThrows) + " one call throws");
        DataCheck.Equal(0, _engine.TotalRecords, nameof(Engine_WriteStreamAsync_Null_SourceThrows) + " TotalRecords after the fault");
        long expected = _engine.Encoding.GetPreamble().Length
            + _engine.Encoding.GetByteCount(_engine.WriteString(_records.Take(RecordsBeforeFault)));
        DataCheck.Equal(expected, ms.Length, nameof(Engine_WriteStreamAsync_Null_SourceThrows) + " output length of one call");
        DataCheck.Equal(Rows / FaultEvery, await Engine_WriteStreamAsync_Null_SourceThrows(), nameof(Engine_WriteStreamAsync_Null_SourceThrows) + " exceptions caught");
    }

    [Benchmark]
    public async Task<long> Engine_ReadAsync_Stream()
    {
        long checksum = 0;
        using var stream = new MemoryStream(_bytes, writable: false);
        await foreach (var record in _engine.ReadAsync(stream))
            checksum += record.Id;
        return checksum;
    }

    [Benchmark]
    public async Task<long> Engine_ReadAsync_TextReader()
    {
        long checksum = 0;
        using var reader = new StreamReader(new MemoryStream(_bytes, writable: false), _engine.Encoding);
        await foreach (var record in _engine.ReadAsync(reader))
            checksum += record.Id;
        return checksum;
    }

    private const int RecordsBeforeFault = 3;

    /// <summary>One faulting write per this many rows, as in the read twin.</summary>
    private const int FaultEvery = 100;

    /// <summary>The first <see cref="RecordsBeforeFault"/> records, then an <see cref="InvalidOperationException"/>.</summary>
    private static IEnumerable<PackageAsset> ThrowAfterFewRecords(PackageAsset[] records)
    {
        for (int i = 0; i < RecordsBeforeFault; i++)
            yield return records[i];
        throw new InvalidOperationException("Benchmark source fault.");
    }

    [Benchmark]
    public async Task<long> Engine_ReadAsync_Path()
    {
        long checksum = 0;
        await foreach (var record in _engine.ReadAsync(_path))
            checksum += record.Id;
        return checksum;
    }

    [Benchmark]
    public async Task<long> Engine_ReadFileAsync_Callback()
    {
        long checksum = 0;
        await _engine.ReadFileAsync(_path, r => checksum += r.Id);
        return checksum;
    }

    [Benchmark]
    public async Task<int> Engine_ReadAsync_Path_ExceptionEvery100()
    {
        await foreach (var _ in _engine.ReadAsync(_path))
        {
        }
        return _engine.ErrorManager.ErrorCount;
    }

    [Benchmark]
    public async Task<int> Engine_WriteStreamAsync_Null()
    {
        await _engine.WriteStreamAsync(Stream.Null, _records);
        return _engine.TotalRecords;
    }

    [Benchmark]
    public async Task<int> Engine_WriteStreamAsync_Null_SourceThrows()
    {
        int caught = 0;
        for (int i = 0; i < Rows / FaultEvery; i++)
        {
            try
            {
                await _engine.WriteStreamAsync(Stream.Null, ThrowAfterFewRecords(_records));
            }
            catch (InvalidOperationException)
            {
                caught++;
            }
        }
        return caught;
    }
}
