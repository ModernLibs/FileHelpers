// Entry point for BenchmarkDotNet — benchmarks added in Phase 10
// Run with: dotnet run -c Release -- --filter '*'
using BenchmarkDotNet.Running;

namespace ModernLibs.FileHelpers.Benchmarks;

internal static class Program
{
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
