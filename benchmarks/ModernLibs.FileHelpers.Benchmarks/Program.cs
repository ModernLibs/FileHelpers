// Entry point for the BenchmarkDotNet suite (see BENCHMARKS.md).
// Run with: dotnet run -c Release -f net11.0 --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --filter '*'
// or, for a configuration built by benchmarks/tools/build-variants.sh:
//   dotnet <out-dir>/net11-ra-on/ModernLibs.FileHelpers.Benchmarks.dll --filter '*'
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using BenchmarkDotNet.Running;
using ModernLibs.FileHelpers.Benchmarks.Benchmarks;

namespace ModernLibs.FileHelpers.Benchmarks;

internal static class Program
{
    /// <returns>
    /// 0 when at least one benchmark ran and every selected benchmark ran successfully; 1 when one failed or
    /// nothing was measured (no match for the filter, <c>--list</c>, <c>--help</c>, <c>--info</c>); 2 for unusable
    /// arguments. A run that measured nothing is never exit 0, so a launcher cannot take it for a measurement
    /// (review-T08, Important 1).
    /// </returns>
    private static int Main(string[] args)
    {
        PrintVariant();
        var (config, arguments, error) = BenchConfig.FromArguments(args);
        if (config is null)
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(arguments, config).ToArray();
        bool measuredNothing = summaries.Sum(s => s.Reports.Length) == 0;
        if (measuredNothing)
            Console.Error.WriteLine("// No benchmark was measured; exit code 1.");
        bool failed = measuredNothing
            || summaries.Any(s => s.HasCriticalValidationErrors
                || s.Reports.Length != s.BenchmarksCases.Length
                || s.Reports.Any(r => !r.Success));
        return failed ? 1 : 0;
    }

    /// <summary>
    /// Proves which library this process measures (design D6): the ModernLibs.FileHelpers assembly it loaded, its
    /// target framework, its runtime-async state and the runtime. The check is the one verify-variant makes on the
    /// file: the Async flag (0x2000) of <c>FileHelperEngine&lt;T&gt;.ReadFileAsync(string, Action&lt;T&gt;, int,
    /// CancellationToken)</c>. benchmarks/tools/ab.py reads these lines and stops a run whose variant does not
    /// match the folder it started.
    /// </summary>
    private static void PrintVariant()
    {
        const string prefix = ".NETCoreApp,Version=v";
        var assembly = typeof(FileHelperEngine<>).Assembly;
        string framework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? "";
        string tfm = framework.StartsWith(prefix, StringComparison.Ordinal) ? "net" + framework[prefix.Length..] : "unknown";
        var method = typeof(FileHelperEngine<PackageAsset>).GetMethod(
            "ReadFileAsync", [typeof(string), typeof(Action<PackageAsset>), typeof(int), typeof(CancellationToken)]);
        string runtimeAsync = method is null ? "unknown"
            : method.MethodImplementationFlags.HasFlag((MethodImplAttributes)0x2000) ? "on" : "off";
        Console.WriteLine($"// ModernLibs.FileHelpers variant: tfm={tfm} runtime-async={runtimeAsync}");
        Console.WriteLine($"// ModernLibs.FileHelpers assembly: {assembly.Location}");
        Console.WriteLine($"// Runtime: {RuntimeInformation.FrameworkDescription}");
    }
}
