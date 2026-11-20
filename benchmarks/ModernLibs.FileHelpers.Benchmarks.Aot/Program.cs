// Native AOT timing harness (T47, design 3.1 row C2). See BENCHMARKS.md, "Native AOT timing harness".
//
//   ModernLibs.FileHelpers.Benchmarks.Aot [--rows N] [--warmup N] [--iterations N] [--data-dir DIR] [--scenario NAME]
//                                         [--expect-checksum HEX] [--checksum-only] [--info]
//
// Times one scenario in this process. PackageAssets_ParseAndDiscard (the default) is the work of
// DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard: one ReadFile of the PackageAssets file with a callback that
// adds each record's Id. PackageAssets_WriteString (T59) is the work of WriteBenchmarks.WriteString_FileHelpers: one
// WriteString of the records the library read from that file. PackageAssets_ParseAndDiscard_NoVector (T21) is the read
// scenario with the AppContext switch ModernLibs.FileHelpers.DisableVectorizedTokenizer set before the first engine
// (a 4.9.1 package has no such switch and reads as in the read scenario). All scenarios share the data check and its
// checksum (the records the library reads); the write scenario also parses its own output with the BCL parsers and
// refuses to time unless that gives the same checksum. One untimed data check, --warmup untimed runs,
// then --iterations timed reads, each preceded by a full garbage collection (as BenchmarkDotNet does between
// iterations). Prints the start-up lines, then one JSON line with every iteration time. Exit codes: 0 done;
// 2 unusable arguments; 3 refused: the records do not match the text of the file, or the data checksum is not the
// one --expect-checksum names (the checksum of the other build of an A/B pair), so nothing is timed.
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ModernLibs.FileHelpers.Benchmarks.Benchmarks;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Aot;

internal static class Program
{
    public const string ReadScenario = "PackageAssets_ParseAndDiscard";
    public const string ReadNoVectorScenario = "PackageAssets_ParseAndDiscard_NoVector";
    public const string WriteScenario = "PackageAssets_WriteString";

    private static int Main(string[] args)
    {
        PrintStartup();
        if (!HarnessOptions.TryParse(args, out var options, out string? error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(HarnessOptions.Usage);
            return 2;
        }
        if (options.Info)
            return 0;

        // The library reads its switches once, when the first engine is made: set it before that (T21, ruling F22).
        if (string.Equals(options.Scenario, ReadNoVectorScenario, StringComparison.Ordinal))
            AppContext.SetSwitch("ModernLibs.FileHelpers.DisableVectorizedTokenizer", true);

        string dataDir = options.DataDir ?? Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dataDir);
        string path = DatasetGenerator.EnsurePackageAssets(dataDir, options.Rows);
        var engine = new FileHelperEngine<PackageAsset>();

        if (!CheckData(engine, path, options, out ulong checksum))
            return 3;
        if (options.ChecksumOnly)
        {
            Console.WriteLine(ResultJson(options.Scenario, options.Rows, warmup: 0, checksum, []));
            return 0;
        }

        ScenarioRun? scenario = string.Equals(options.Scenario, WriteScenario, StringComparison.Ordinal)
            ? WriteRun.Create(path, dataDir, checksum, options.Rows)
            : new ReadRun(engine, path, options.Rows);
        if (scenario is null)
            return 3;
        for (int i = 0; i < options.Warmup; i++)
        {
            if (scenario.Run() is { } wrong)
                return Refuse($"warm-up run {i + 1}: {wrong}");
        }

        var times = new long[options.Iterations];
        for (int i = 0; i < times.Length; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long start = Stopwatch.GetTimestamp();
            string? wrong = scenario.Run();
            long end = Stopwatch.GetTimestamp();
            if (wrong is not null)
                return Refuse($"timed run {i + 1}: {wrong}");
            times[i] = (long)((end - start) * (1_000_000_000.0 / Stopwatch.Frequency));
        }

        Console.WriteLine(ResultJson(options.Scenario, options.Rows, options.Warmup, checksum, times));
        return 0;
    }

    /// <summary>One run of a scenario; <see cref="Run"/> returns null, or the reason the run gave a wrong result.</summary>
    private abstract class ScenarioRun
    {
        public abstract string? Run();
    }

    /// <summary>The measured work of DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard.</summary>
    private sealed class ReadRun(FileHelperEngine<PackageAsset> engine, string path, int rows) : ScenarioRun
    {
        private readonly long _idSum = (long)rows * (rows - 1) / 2;

        public override string? Run()
        {
            long sum = ParseAndDiscard(engine, path);
            return sum == _idSum ? null : $"the Id sum is {sum}, expected {_idSum}";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long ParseAndDiscard(FileHelperEngine<PackageAsset> engine, string path)
        {
            long checksum = 0;
            engine.ReadFile(path, r => checksum += r.Id);
            return checksum;
        }
    }

    /// <summary>
    /// The measured work of WriteBenchmarks.WriteString_FileHelpers: WriteString of the records read from the file,
    /// with the settings of that benchmark (CRLF, the header text without its line ending). Create writes once, parses
    /// the output with the BCL parsers (DataChecksum.OfFile) and refuses (null) unless rows and checksum are those of
    /// the data check; every run must then return a string of the same length.
    /// </summary>
    private sealed class WriteRun : ScenarioRun
    {
        private readonly FileHelperEngine<PackageAsset> _engine;
        private readonly PackageAsset[] _records;
        private readonly int _length;

        private WriteRun(FileHelperEngine<PackageAsset> engine, PackageAsset[] records, int length)
        {
            _engine = engine;
            _records = records;
            _length = length;
        }

        public static WriteRun? Create(string path, string dataDir, ulong textChecksum, int rows)
        {
            var writer = new FileHelperEngine<PackageAsset> { NewLineForWrite = "\r\n" };
            var records = new List<PackageAsset>(rows);
            writer.ReadFile(path, records.Add);
            // 4.10 captures the header with its line ending, 4.9.1 without (see WriteBenchmarks.Prepare).
            writer.HeaderText = writer.HeaderText?.TrimEnd('\r', '\n');
            var array = records.ToArray();
            string text = writer.WriteString(array);
            string outputPath = Path.Combine(dataDir, "output_write_check.csv");
            File.WriteAllText(outputPath, text);
            try
            {
                var (writtenChecksum, writtenRows) = DataChecksum.OfFile(outputPath);
                if (writtenRows != rows || writtenChecksum != textChecksum)
                {
                    Refuse($"data check: WriteString wrote {writtenRows} rows, checksum {Hex(writtenChecksum)}; "
                        + $"the data check read {rows} rows, checksum {Hex(textChecksum)}");
                    return null;
                }
            }
            catch (InvalidDataException ex)
            {
                Refuse("data check: the output of WriteString does not parse: " + ex.Message);
                return null;
            }
            finally
            {
                File.Delete(outputPath);
            }
            return new WriteRun(writer, array, text.Length);
        }

        public override string? Run()
        {
            string text = Write(_engine, _records);
            return text.Length == _length ? null : $"WriteString returned {text.Length} characters, expected {_length}";
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string Write(FileHelperEngine<PackageAsset> engine, PackageAsset[] records) => engine.WriteString(records);
    }

    /// <summary>
    /// The untimed data check: the records the library reads must give the checksum of the file's text, and, with
    /// --expect-checksum, the checksum the other build of the pair gave. Otherwise the run is refused before timing.
    /// </summary>
    private static bool CheckData(FileHelperEngine<PackageAsset> engine, string path, HarnessOptions options, out ulong checksum)
    {
        checksum = 0;
        ulong textChecksum;
        int textRows;
        try
        {
            (textChecksum, textRows) = DataChecksum.OfFile(path);
        }
        catch (InvalidDataException ex)
        {
            Refuse("data check: " + ex.Message);
            return false;
        }
        var (readChecksum, readRows) = DataChecksum.OfRecords(engine, path);
        checksum = readChecksum;
        if (textRows != options.Rows || readRows != textRows || readChecksum != textChecksum)
        {
            Refuse($"data check: the text of {path} has {textRows} rows, checksum {Hex(textChecksum)}; "
                + $"the library read {readRows} records, checksum {Hex(readChecksum)}");
            return false;
        }
        if (options.ExpectChecksum is { } expected && expected != readChecksum)
        {
            Refuse($"data checksum {Hex(readChecksum)} differs from the expected {Hex(expected)} "
                + "(the other build of the pair read other values)");
            return false;
        }
        return true;
    }

    private static int Refuse(string reason)
    {
        Console.Error.WriteLine($"// REFUSED, nothing timed: {reason}");
        return 3;
    }

    /// <summary>
    /// The start-up lines (benchmarks/tools/ab.py checks them). The variant line has the format verify-variant
    /// prints; build-aot-variants.sh checks it against the DLL the AOT compiler read.
    /// </summary>
    private static void PrintStartup()
    {
        Console.WriteLine($"// ModernLibs.FileHelpers variant: {Variant}");
        Console.WriteLine($"// ModernLibs.FileHelpers library: {HarnessBuild.Library}");
        Console.WriteLine($"// Build: {BuildLine}");
        Console.WriteLine($"// Runtime: {RuntimeInformation.FrameworkDescription}");
    }

    private static string Variant => $"tfm={HarnessBuild.LibraryTfm} runtime-async={HarnessBuild.RuntimeAsync}";

    // A JIT process (dotnet <dll>, the apphost) is started by the .NET host, which hands the runtime its list of
    // trusted platform assemblies; a Native AOT process has no host and no such list. RuntimeFeature cannot tell
    // the two apart: PublishAot also turns IsDynamicCodeSupported off in the JIT build's runtimeconfig.json.
    private static bool IsNativeAot => AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is null;

    private static string BuildLine =>
        $"native-aot={(IsNativeAot ? "yes" : "no")} app-tfm={HarnessBuild.AppTfm} rid={Rid}";

    private static string Rid => HarnessBuild.RuntimeIdentifier.Length == 0 ? "none" : HarnessBuild.RuntimeIdentifier;

    private static string Hex(ulong value) => "0x" + value.ToString("x16", CultureInfo.InvariantCulture);

    private static string ResultJson(string scenario, int rows, int warmup, ulong checksum, long[] times)
    {
        var json = new StringBuilder(256 + (times.Length * 12));
        var inv = CultureInfo.InvariantCulture;
        json.Append("{\"harness\":\"aot\"");
        AppendString(json, "scenario", scenario);
        AppendString(json, "variant", Variant);
        AppendString(json, "library", HarnessBuild.Library);
        AppendString(json, "build", BuildLine);
        AppendString(json, "runtime", RuntimeInformation.FrameworkDescription);
        json.Append(inv, $",\"rows\":{rows},\"warmup\":{warmup},\"iterations\":{times.Length}");
        AppendString(json, "checksum", Hex(checksum));
        json.Append(",\"times_ns\":[");
        for (int i = 0; i < times.Length; i++)
            json.Append(inv, $"{(i == 0 ? "" : ",")}{times[i]}");
        json.Append("]}");
        return json.ToString();
    }

    private static void AppendString(StringBuilder json, string name, string value)
    {
        json.Append(",\"").Append(name).Append("\":\"");
        foreach (char c in value)
        {
            if (c is '"' or '\\')
                json.Append('\\').Append(c);
            else if (c < ' ')
                json.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:x4}");
            else
                json.Append(c);
        }
        json.Append('"');
    }
}
