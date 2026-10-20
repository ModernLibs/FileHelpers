// Entry point for the BenchmarkDotNet suite (see BENCHMARKS.md).
// Run with: dotnet run -c Release --project benchmarks/ModernLibs.FileHelpers.Benchmarks -- --filter '*'
using BenchmarkDotNet.Running;

namespace ModernLibs.FileHelpers.Benchmarks;

internal static class Program
{
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
