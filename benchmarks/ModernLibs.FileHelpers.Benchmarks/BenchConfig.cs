using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace ModernLibs.FileHelpers.Benchmarks;

/// <summary>
/// The configuration every run of the suite uses (T08, design B1, B4 and D6).
/// </summary>
/// <remarks>
/// <para>
/// Columns: the memory diagnoser adds Gen0, Gen1, Gen2 (collections per 1,000 operations) and Allocated to
/// every table (B1). Exporters: BenchmarkDotNet's defaults plus the full JSON report
/// (<c>results/*-report-full.json</c>), which <c>benchmarks/tools/ab.py</c> reads.
/// </para>
/// <para>
/// Toolchain: in-process. The suite is built in three configurations (<c>net10</c>, <c>net11-ra-on</c>,
/// <c>net11-ra-off</c>, see <c>benchmarks/tools/build-variants.sh</c>) that differ only in how the library was
/// compiled. BenchmarkDotNet's default toolchain does not run the assembly it is started from: it looks for the
/// project file under the current directory and builds it again, with default properties, so a
/// <c>net11-ra-off</c> build would be measured as a fresh runtime-async build (and BenchmarkDotNet 0.15.8 does not
/// recognise .NET 11 there at all). In-process, the measured library is the DLL that verify-variant checked.
/// The cost is that all benchmarks selected by one <c>--filter</c> share one process; ab.py therefore starts one
/// process per benchmark method, and results that are published come from such runs.
/// </para>
/// <para>
/// Jobs: <c>--job &lt;name&gt;</c> is handled here instead of by BenchmarkDotNet, because BenchmarkDotNet's own
/// <c>--job</c> replaces the job, and with it the toolchain. The other job options (<c>--iterationCount</c>,
/// <c>--warmupCount</c>, ...) apply on top as usual.
/// </para>
/// </remarks>
internal sealed class BenchConfig : ManualConfig
{
    /// <summary>
    /// BenchmarkDotNet stops an in-process benchmark case after 5 minutes by default; the 1M-row cases of the
    /// default job can come close to that on a slow machine.
    /// </summary>
    private static readonly IToolchain s_toolchain = new InProcessEmitToolchain(TimeSpan.FromHours(2), logOutput: true);

    private BenchConfig(Job job)
    {
        Add(DefaultConfig.Instance);
        AddJob(job.WithToolchain(s_toolchain).WithId(job.Id).AsDefault());
        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(new GcCountColumn(0), new GcCountColumn(1), new GcCountColumn(2));
        AddExporter(JsonExporter.Full);
    }

    /// <summary>
    /// Builds the configuration from the command line: takes out <c>--job</c>/<c>-j</c> (and <c>--inProcess</c>,
    /// which is always on) and returns the remaining arguments for BenchmarkDotNet.
    /// </summary>
    /// <returns><see langword="null"/> and an error message when the arguments cannot be used.</returns>
    public static (BenchConfig? Config, string[] Arguments, string? Error) FromArguments(string[] args)
    {
        var rest = new List<string>(args.Length);
        string jobName = "default";
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "--job" or "-j")
            {
                if (i + 1 >= args.Length)
                    return (null, args, "--job needs a value");
                jobName = args[++i];
            }
            else if (arg.StartsWith("--job=", StringComparison.Ordinal))
            {
                jobName = arg["--job=".Length..];
            }
            else if (arg is "--inProcess" or "-i")
            {
                // Always in-process (see the remarks of BenchConfig).
            }
            else if (arg is "--runtimes" or "-r" || arg.StartsWith("--runtimes=", StringComparison.Ordinal))
            {
                return (null, args, "--runtimes is not supported: benchmarks run in-process on the runtime of the build. "
                    + "Build one folder per configuration with benchmarks/tools/build-variants.sh.");
            }
            else
            {
                rest.Add(arg);
            }
        }

        Job? job = jobName.ToUpperInvariant() switch
        {
            "DEFAULT" => Job.Default,
            "DRY" => Job.Dry,
            "SHORT" => Job.ShortRun,
            "MEDIUM" => Job.MediumRun,
            "LONG" => Job.LongRun,
            "VERYLONG" => Job.VeryLongRun,
            _ => null,
        };
        if (job is null)
            return (null, args, $"unknown job '{jobName}' (Default, Dry, Short, Medium, Long, VeryLong)");
        return (new BenchConfig(job), rest.ToArray(), null);
    }

    /// <summary>
    /// GenN collections per 1,000 operations, shown in every table (B1). The memory diagnoser's own GenN column
    /// disappears when no benchmark of a table has a collection of that generation, which leaves the reader to
    /// guess whether the count is zero or was not measured. This column has the memory diagnoser's column id
    /// and prints its value in the same format; BenchmarkDotNet keeps one column per id, so a table shows
    /// either one with the same text.
    /// </summary>
    private sealed class GcCountColumn(int generation) : IColumn
    {
        private readonly string _metricId = $"Gen{generation}Collects";

        public string Id => _metricId;

        public string ColumnName { get; } = $"Gen{generation}";

        public bool AlwaysShow => true;

        public ColumnCategory Category => ColumnCategory.Metric;

        public int PriorityInCategory => generation;

        public bool IsNumeric => true;

        public UnitType UnitType => UnitType.Dimensionless;

        public string Legend => $"GC Generation {generation} collects per 1000 operations";

        public bool IsAvailable(Summary summary) => true;

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase) =>
            GetValue(summary, benchmarkCase, summary.Style);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            var report = summary[benchmarkCase];
            if (report is null || !report.Metrics.TryGetValue(_metricId, out var metric))
                return "NA";
            if (metric.Value == 0 && !style.PrintZeroValuesInContent)
                return "-";
            return metric.Value.ToString("#0.0000", summary.GetCultureInfo());
        }

        public override string ToString() => ColumnName;
    }
}
