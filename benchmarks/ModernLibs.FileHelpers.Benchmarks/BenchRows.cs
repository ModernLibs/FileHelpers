using System.Globalization;

namespace ModernLibs.FileHelpers.Benchmarks;

/// <summary>
/// The <c>Rows</c> values of the three classes that run 10,000, 100,000 and 1,000,000 rows
/// (DelimitedParseBenchmarks, SepComparisonBenchmarks, WriteBenchmarks). <c>FH_BENCH_MAX_ROWS</c> cuts the list to the
/// values at or below it: <c>benchmarks/tools/round.sh --max-rows N</c> sets it for every measured process, so a round
/// never runs the 1,000,000-row case (design D10). Unset or empty: all three values.
/// </summary>
internal static class BenchRows
{
    public const string MaxRowsVariable = "FH_BENCH_MAX_ROWS";

    private static readonly int[] All = [10_000, 100_000, 1_000_000];

    public static IEnumerable<int> Values
    {
        get
        {
            string? text = Environment.GetEnvironmentVariable(MaxRowsVariable);
            if (string.IsNullOrEmpty(text))
                return All;
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int max) || max < All[0])
                throw new InvalidOperationException(
                    $"{MaxRowsVariable}={text} is not an integer of at least {All[0]}; no Rows value would be left.");
            return All.Where(rows => rows <= max).ToArray();
        }
    }
}
