// The tokens, the tokenizer and TokenBuffer are new in 4.10: a build against the 4.9.1 package or source (round.sh
// baselines, FILEHELPERS_BASELINE) has no TokensPassingBenchmarks.
#if !FILEHELPERS_BASELINE
using System.Globalization;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;

/// <summary>
/// Freeze F-1: <c>in RecordTokens</c> against <c>RecordTokens</c> by value. The same 25-column parse body
/// (<see cref="PackageAsset"/> from <c>GetValue</c> and <c>GetString</c>, with the BCL parsers) sits behind two
/// interfaces that differ only in how the tokens are passed, and is called through the interface as the runtime
/// calls a descriptor: each line is tokenized into one <c>TokenBuffer</c>, then the record is parsed.
/// </summary>
/// <remarks>
/// <see cref="Parse_In"/> is the frozen signature. The decision is re-opened only if <see cref="Parse_ByValue"/> is at
/// least 3 % faster by the B4 rule on both VMs of the T19 round (a local run is smoke only, D1). Each setup checks its
/// own checksum against <see cref="string.Split(char, StringSplitOptions)"/>; <c>--check-prototypes</c> checks that
/// both give the records of the engine, field by field.
/// </remarks>
[MemoryDiagnoser]
public class TokensPassingBenchmarks
{
    private const int Rows = 10_000;

    /// <summary>The tokens passed with <c>in</c>, as <c>ITokenizedDescriptor&lt;T&gt;.TryParse</c>.</summary>
    internal interface IParseIn
    {
        bool TryParse(ReadOnlySpan<char> record, in RecordTokens tokens, out PackageAsset result);
    }

    /// <summary>The tokens passed by value.</summary>
    internal interface IParseByValue
    {
        bool TryParse(ReadOnlySpan<char> record, RecordTokens tokens, out PackageAsset result);
    }

    private string[] _lines = [];
    private TokenBuffer _buffer = new(25);
    private IParseIn _in = new ParseIn();
    private IParseByValue _byValue = new ParseByValue();

    private void Prepare()
    {
        _lines = ComponentData.Lines(TokenizerDataset.Columns25, ComponentData.DataDir());
        _buffer = new TokenBuffer(25);
        _in = new ParseIn();
        _byValue = new ParseByValue();
    }

    [GlobalSetup(Target = nameof(Parse_In))]
    public void SetupIn()
    {
        Prepare();
        DataCheck.Equal(Reference(_lines), Parse_In(), nameof(Parse_In) + " checksum");
    }

    [GlobalSetup(Target = nameof(Parse_ByValue))]
    public void SetupByValue()
    {
        Prepare();
        DataCheck.Equal(Reference(_lines), Parse_ByValue(), nameof(Parse_ByValue) + " checksum");
    }

    [Benchmark(Baseline = true)]
    public long Parse_In()
    {
        long sum = 0;
        var buffer = _buffer;
        var parser = _in;
        foreach (string line in _lines)
        {
            var tokens = Tokenize(buffer, line);
            if (!parser.TryParse(line, in tokens, out var record))
                ThrowNotHandled();
            sum += Checksum(record);
        }
        return sum;
    }

    [Benchmark]
    public long Parse_ByValue()
    {
        long sum = 0;
        var buffer = _buffer;
        var parser = _byValue;
        foreach (string line in _lines)
        {
            var tokens = Tokenize(buffer, line);
            if (!parser.TryParse(line, tokens, out var record))
                ThrowNotHandled();
            sum += Checksum(record);
        }
        return sum;
    }

    /// <summary>The records of both paths for <c>--check-prototypes</c>.</summary>
    internal static (List<PackageAsset> In, List<PackageAsset> ByValue) Collect(string[] lines)
    {
        var buffer = new TokenBuffer(25);
        IParseIn byIn = new ParseIn();
        IParseByValue byValue = new ParseByValue();
        var a = new List<PackageAsset>(lines.Length);
        var b = new List<PackageAsset>(lines.Length);
        foreach (string line in lines)
        {
            var tokens = Tokenize(buffer, line);
            if (!byIn.TryParse(line, in tokens, out var x))
                ThrowNotHandled();
            a.Add(x);
            tokens = Tokenize(buffer, line);
            if (!byValue.TryParse(line, tokens, out var y))
                ThrowNotHandled();
            b.Add(y);
        }
        return (a, b);
    }

    private static RecordTokens Tokenize(TokenBuffer buffer, string line)
    {
        int n = RecordTokenizer.TokenizePlain(line, ',', buffer.Ends, vectors: true);
        if (n != 25)
            ThrowNotHandled();
        return buffer.Tokens(line, n, delimiterLength: 1);
    }

    private static long Checksum(PackageAsset r) =>
        r.Id + r.DownloadCount + r.Type + r.DependencyCount + r.PackageSize + r.Version.Length + r.RepositoryUrl.Length
        + (r.IsPrerelease ? 1 : 0) + (r.RequireLicenseAcceptance ? 2 : 0) + r.Published.Day + r.LastUpdated.Month;

    /// <summary>The checksum computed with <see cref="string.Split(char, StringSplitOptions)"/> and the same BCL parsers.</summary>
    private static long Reference(string[] lines)
    {
        long sum = 0;
        foreach (string line in lines)
        {
            string[] f = line.Split(',');
            DataCheck.Equal(25, f.Length, "TokensPassing reference field count");
            sum += int.Parse(f[0], CultureInfo.InvariantCulture) + int.Parse(f[5], CultureInfo.InvariantCulture)
                + int.Parse(f[12], CultureInfo.InvariantCulture) + int.Parse(f[17], CultureInfo.InvariantCulture)
                + long.Parse(f[16], CultureInfo.InvariantCulture) + f[1].Length + f[23].Length
                + (bool.Parse(f[13]) ? 1 : 0) + (bool.Parse(f[24]) ? 2 : 0)
                + DateTime.ParseExact(f[14], DatasetGenerator.DateFormat, CultureInfo.InvariantCulture).Day
                + DateTime.ParseExact(f[15], DatasetGenerator.DateFormat, CultureInfo.InvariantCulture).Month;
        }
        return sum;
    }

    private static void ThrowNotHandled() =>
        throw new InvalidOperationException("Benchmark correctness check failed: a line was not parsed.");

    /// <summary>The parse body both interfaces share; inlined into each, so only the passing differs.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PackageAsset Build(in RecordTokens t) => new()
    {
        Id = int.Parse(t.GetValue(0), NumberStyles.Integer, CultureInfo.InvariantCulture),
        Version = t.GetString(1),
        Authors = t.GetString(2),
        Description = t.GetString(3),
        Tags = t.GetString(4),
        DownloadCount = int.Parse(t.GetValue(5), NumberStyles.Integer, CultureInfo.InvariantCulture),
        ProjectUrl = t.GetString(6),
        LicenseUrl = t.GetString(7),
        IconUrl = t.GetString(8),
        Language = t.GetString(9),
        Framework = t.GetString(10),
        MinClientVersion = t.GetString(11),
        Type = int.Parse(t.GetValue(12), NumberStyles.Integer, CultureInfo.InvariantCulture),
        IsPrerelease = bool.Parse(t.GetValue(13)),
        Published = DateTime.ParseExact(t.GetValue(14), DatasetGenerator.DateFormat, CultureInfo.InvariantCulture),
        LastUpdated = DateTime.ParseExact(t.GetValue(15), DatasetGenerator.DateFormat, CultureInfo.InvariantCulture),
        PackageSize = long.Parse(t.GetValue(16), NumberStyles.Integer, CultureInfo.InvariantCulture),
        DependencyCount = int.Parse(t.GetValue(17), NumberStyles.Integer, CultureInfo.InvariantCulture),
        HasReadme = bool.Parse(t.GetValue(18)),
        HasLicense = bool.Parse(t.GetValue(19)),
        HasIcon = bool.Parse(t.GetValue(20)),
        IsSigned = bool.Parse(t.GetValue(21)),
        RepositoryType = t.GetString(22),
        RepositoryUrl = t.GetString(23),
        RequireLicenseAcceptance = bool.Parse(t.GetValue(24)),
    };

    private sealed class ParseIn : IParseIn
    {
        public bool TryParse(ReadOnlySpan<char> record, in RecordTokens tokens, out PackageAsset result)
        {
            result = Build(in tokens);
            return true;
        }
    }

    private sealed class ParseByValue : IParseByValue
    {
        public bool TryParse(ReadOnlySpan<char> record, RecordTokens tokens, out PackageAsset result)
        {
            result = Build(in tokens);
            return true;
        }
    }
}
#endif
