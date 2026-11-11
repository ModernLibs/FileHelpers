// Entry point for the BenchmarkDotNet suite (see BENCHMARKS.md).
// Run with: dotnet run -c Release -f net11.0 --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --filter '*'
// or, for a configuration built by benchmarks/tools/build-variants.sh:
//   dotnet <out-dir>/net11-ra-on/ModernLibs.FileHelpers.Benchmarks.dll --filter '*'
using BenchmarkDotNet.Running;

namespace ModernLibs.FileHelpers.Benchmarks;

internal static class Program
{
    /// <returns>0 when every selected benchmark ran, 1 when one failed, 2 for unusable arguments.</returns>
    private static int Main(string[] args)
    {
        var (config, arguments, error) = BenchConfig.FromArguments(args);
        if (config is null)
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(arguments, config);
        bool failed = summaries.Any(s => s.HasCriticalValidationErrors
            || s.Reports.Length != s.BenchmarksCases.Length
            || s.Reports.Any(r => !r.Success));
        return failed ? 1 : 0;
    }
}
