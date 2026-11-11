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
/// </list>
/// </remarks>
[MemoryDiagnoser]
public class EngineAsyncBenchmarks
{
    private string _path = "";
    private FileHelperEngine<PackageAsset> _engine = null!;
    private PackageAsset[] _records = [];

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
}
