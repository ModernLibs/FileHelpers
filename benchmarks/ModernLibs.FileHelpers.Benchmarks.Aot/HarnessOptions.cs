using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace ModernLibs.FileHelpers.Benchmarks.Aot;

/// <summary>Command-line options of the AOT harness (see Program.cs).</summary>
internal sealed class HarnessOptions
{
    public const string Usage =
        "usage: ModernLibs.FileHelpers.Benchmarks.Aot [--rows N] [--warmup N] [--iterations N] [--data-dir DIR] "
        + "[--expect-checksum HEX] [--checksum-only] [--info]";

    /// <summary>Data rows of the PackageAssets file (BenchmarkDotNet uses 10K, 100K and 1M).</summary>
    public int Rows { get; private set; } = 100_000;

    /// <summary>Untimed reads after the data check.</summary>
    public int Warmup { get; private set; } = 5;

    /// <summary>Timed reads; the process reports each one.</summary>
    public int Iterations { get; private set; } = 20;

    /// <summary>Folder of the generated dataset (default: Data next to the executable).</summary>
    public string? DataDir { get; private set; }

    /// <summary>The data checksum the reads must give; the run is refused before timing otherwise.</summary>
    public ulong? ExpectChecksum { get; private set; }

    /// <summary>Run the data check, print the result line without times, and exit.</summary>
    public bool ChecksumOnly { get; private set; }

    /// <summary>Print the start-up lines and exit.</summary>
    public bool Info { get; private set; }

    public static bool TryParse(string[] args, out HarnessOptions options, [NotNullWhen(false)] out string? error)
    {
        options = new HarnessOptions();
        error = null;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--checksum-only":
                    options.ChecksumOnly = true;
                    continue;
                case "--info":
                    options.Info = true;
                    continue;
                case "--rows" or "--warmup" or "--iterations" or "--data-dir" or "--expect-checksum":
                    break;
                default:
                    error = $"unknown argument '{arg}'";
                    return false;
            }

            if (i + 1 >= args.Length)
            {
                error = $"{arg} needs a value";
                return false;
            }
            string value = args[++i];
            if (!options.TrySet(arg, value))
            {
                error = $"{arg}: invalid value '{value}'";
                return false;
            }
        }
        return true;
    }

    private bool TrySet(string name, string value)
    {
        switch (name)
        {
            case "--data-dir":
                DataDir = value;
                return value.Length > 0;
            case "--expect-checksum":
                string hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
                if (!ulong.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong checksum))
                    return false;
                ExpectChecksum = checksum;
                return true;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            return false;
        switch (name)
        {
            case "--rows" when number >= 1:
                Rows = number;
                return true;
            case "--warmup":
                Warmup = number;
                return true;
            case "--iterations" when number >= 1:
                Iterations = number;
                return true;
            default:
                return false;
        }
    }
}
