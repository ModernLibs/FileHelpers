// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004

using BenchmarkDotNet.Attributes;
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
/// </list>
/// <c>Engine_WriteStreamAsync_Null</c> (design B8, A1) is added when <c>WriteStreamAsync</c> exists (T43).
/// </remarks>
[MemoryDiagnoser]
public class EngineAsyncBenchmarks
{
    private string _path = "";
    private FileHelperEngine<PackageAsset> _engine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    private static string DataDir()
    {
        var dataDir = Path.Combine(Path.GetDirectoryName(typeof(EngineAsyncBenchmarks).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dataDir);
        return dataDir;
    }

    [GlobalSetup(Target = nameof(Engine_ReadAsync_Path))]
    public void SetupReadAsync()
    {
        _path = DatasetGenerator.EnsurePackageAssets(DataDir(), Rows);
        _engine = new FileHelperEngine<PackageAsset>();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), Engine_ReadAsync_Path().GetAwaiter().GetResult(), nameof(Engine_ReadAsync_Path) + " checksum");
    }

    [GlobalSetup(Target = nameof(Engine_ReadFileAsync_Callback))]
    public void SetupReadFileAsync()
    {
        _path = DatasetGenerator.EnsurePackageAssets(DataDir(), Rows);
        _engine = new FileHelperEngine<PackageAsset>();
        DataCheck.Equal(DiscardChecksum.IdSum(Rows), Engine_ReadFileAsync_Callback().GetAwaiter().GetResult(), nameof(Engine_ReadFileAsync_Callback) + " checksum");
    }

    [GlobalSetup(Target = nameof(Engine_ReadAsync_Path_ExceptionEvery100))]
    public void SetupExceptionEvery100()
    {
        _path = DatasetGenerator.EnsurePackageAssetsBadEvery100(DataDir(), Rows);
        _engine = new FileHelperEngine<PackageAsset> { ErrorMode = ErrorMode.SaveAndContinue };
        DataCheck.Equal(Rows / DatasetGenerator.BadRowEvery, Engine_ReadAsync_Path_ExceptionEvery100().GetAwaiter().GetResult(), nameof(Engine_ReadAsync_Path_ExceptionEvery100) + " ErrorCount");
        DataCheck.Equal(Rows - (Rows / DatasetGenerator.BadRowEvery), _engine.TotalRecords, nameof(Engine_ReadAsync_Path_ExceptionEvery100) + " records read");
        var first = _engine.ErrorManager.Errors[0];
        DataCheck.Equal(DatasetGenerator.BadRowEvery + 1, first.LineNumber, nameof(Engine_ReadAsync_Path_ExceptionEvery100) + " line of the first error (after the header)");
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
}
