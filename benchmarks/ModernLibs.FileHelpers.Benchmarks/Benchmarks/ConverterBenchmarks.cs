// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using System.Globalization;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Converters;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

/// <summary>
/// The default converters on their own, with the values of the 25-column dataset: <c>True</c>/<c>False</c>
/// for bools and the sortable <c>s</c> pattern for dates. Each benchmark parses 1,024 values, so a change of
/// a few nanoseconds per value shows above the noise of the full-file benchmarks.
/// </summary>
[MemoryDiagnoser]
public class ConverterBenchmarks
{
    private const int Count = 1024;

    private readonly DateTimeConverter _date = new("s");
    private string[] _bools = null!;
    private string[] _dates = null!;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _bools = new string[Count];
        _dates = new string[Count];
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        for (int i = 0; i < Count; i++)
        {
            _bools[i] = random.Next(2) == 1 ? "True" : "False";
            _dates[i] = baseDate.AddDays(-random.Next(0, 3650)).AddSeconds(random.Next(0, 86_400)).ToString("s", CultureInfo.InvariantCulture);
        }

        if (Bool() != _bools.Count(b => string.Equals(b, "True", StringComparison.Ordinal)) || Date() == 0)
            throw new InvalidOperationException("converter benchmark guard");
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public int Bool()
    {
        int trues = 0;
        foreach (string text in _bools)
        {
            if (!BoolConverter.Instance.TryParse(text, out bool value))
                throw new InvalidOperationException(text);
            if (value)
                trues++;
        }

        return trues;
    }

    [Benchmark(OperationsPerInvoke = Count)]
    public long Date()
    {
        long ticks = 0;
        foreach (string text in _dates)
        {
            if (!_date.TryParse(text, out var value))
                throw new InvalidOperationException(text);
            ticks ^= value.Ticks;
        }

        return ticks;
    }
}
