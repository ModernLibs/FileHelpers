// Entry point for BenchmarkDotNet — Phase 33 benchmark regression suite
// Run with: dotnet run -c Release -p benchmarks/ModernLibs.FileHelpers.Benchmarks -- --filter '*'
using BenchmarkDotNet.Running;

namespace ModernLibs.FileHelpers.Benchmarks;

internal static class Program
{
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
