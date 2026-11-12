// Native AOT timing harness (T47, design 3.1 row C2). See BENCHMARKS.md, "Native AOT timing harness".
//
//   ModernLibs.FileHelpers.Benchmarks.Aot [--rows N] [--warmup N] [--iterations N] [--data-dir DIR]
//                                         [--expect-checksum HEX] [--checksum-only] [--info]
//
// Times the work of DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard (one ReadFile of the PackageAssets file
// with a callback that adds each record's Id) in this process: one untimed data check, --warmup untimed reads,
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
    public const string Scenario = "PackageAssets_ParseAndDiscard";

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

        string dataDir = options.DataDir ?? Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dataDir);
        string path = DatasetGenerator.EnsurePackageAssets(dataDir, options.Rows);
        var engine = new FileHelperEngine<PackageAsset>();

        if (!CheckData(engine, path, options, out ulong checksum))
            return 3;
        if (options.ChecksumOnly)
        {
            Console.WriteLine(ResultJson(options.Rows, warmup: 0, checksum, []));
            return 0;
        }

        long idSum = (long)options.Rows * (options.Rows - 1) / 2;
        for (int i = 0; i < options.Warmup; i++)
        {
            if (ParseAndDiscard(engine, path) != idSum)
                return Refuse($"warm-up read {i + 1} returned another Id sum than {idSum}");
        }

        var times = new long[options.Iterations];
        for (int i = 0; i < times.Length; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long start = Stopwatch.GetTimestamp();
            long sum = ParseAndDiscard(engine, path);
            long end = Stopwatch.GetTimestamp();
            if (sum != idSum)
                return Refuse($"timed read {i + 1} returned the Id sum {sum}, expected {idSum}");
            times[i] = (long)((end - start) * (1_000_000_000.0 / Stopwatch.Frequency));
        }

        Console.WriteLine(ResultJson(options.Rows, options.Warmup, checksum, times));
        return 0;
    }

    /// <summary>The measured work, the body of DelimitedParseBenchmarks.FileHelpers_ParseAndDiscard.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long ParseAndDiscard(FileHelperEngine<PackageAsset> engine, string path)
    {
        long checksum = 0;
        engine.ReadFile(path, r => checksum += r.Id);
        return checksum;
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

    private static string ResultJson(int rows, int warmup, ulong checksum, long[] times)
    {
        var json = new StringBuilder(256 + (times.Length * 12));
        var inv = CultureInfo.InvariantCulture;
        json.Append("{\"harness\":\"aot\"");
        AppendString(json, "scenario", Scenario);
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
