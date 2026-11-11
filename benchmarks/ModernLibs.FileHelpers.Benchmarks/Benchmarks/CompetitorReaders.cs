// MA0051: the field-by-field mappings are long on purpose.
#pragma warning disable MA0051

using System.Collections.Concurrent;
using System.Globalization;
using FlameCsv;
using FlameCsv.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
using RecordParser.Builders.Reader;
using RecordParser.Extensions;
using RecordParser.Parsers;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

// FlameCsv generates its binding at compile time from these declarations (the same approach as the
// [CsvHelper.CsvRecord] rows); the columns are bound by header name. The file is read as UTF-8 bytes, the
// way FlameCsv is meant to read a file.
[CsvTypeMap<byte, PackageAssetRow>]
internal sealed partial class PackageAssetFlameMap;

[CsvTypeMap<byte, QuotedCompetitorRow>]
internal sealed partial class QuotedFlameMap;

/// <summary>RecordParser reader definitions for the datasets. RecordParser maps by column index and builds its reader once.</summary>
internal static class RecordParserReaders
{
    public static IVariableLengthReader<PackageAssetRow> PackageAssets() => new VariableLengthReaderBuilder<PackageAssetRow>()
        .Map(x => x.Id, 0)
        .Map(x => x.Version, 1)
        .Map(x => x.Authors, 2)
        .Map(x => x.Description, 3)
        .Map(x => x.Tags, 4)
        .Map(x => x.DownloadCount, 5)
        .Map(x => x.ProjectUrl, 6)
        .Map(x => x.LicenseUrl, 7)
        .Map(x => x.IconUrl, 8)
        .Map(x => x.Language, 9)
        .Map(x => x.Framework, 10)
        .Map(x => x.MinClientVersion, 11)
        .Map(x => x.Type, 12)
        .Map(x => x.IsPrerelease, 13)
        .Map(x => x.Published, 14)
        .Map(x => x.LastUpdated, 15)
        .Map(x => x.PackageSize, 16)
        .Map(x => x.DependencyCount, 17)
        .Map(x => x.HasReadme, 18)
        .Map(x => x.HasLicense, 19)
        .Map(x => x.HasIcon, 20)
        .Map(x => x.IsSigned, 21)
        .Map(x => x.RepositoryType, 22)
        .Map(x => x.RepositoryUrl, 23)
        .Map(x => x.RequireLicenseAcceptance, 24)
        .Build(",", CultureInfo.InvariantCulture, () => new PackageAssetRow());

    public static IVariableLengthReader<QuotedCompetitorRow> Quoted() => new VariableLengthReaderBuilder<QuotedCompetitorRow>()
        .Map(x => x.Id, 0)
        .Map(x => x.Name, 1)
        .Map(x => x.Description, 2)
        .Map(x => x.Notes, 3)
        .Map(x => x.Category, 4)
        .Build(",", CultureInfo.InvariantCulture, () => new QuotedCompetitorRow());

    // Fields keep their padding (no trim), like FixedRecord.
    public static IFixedLengthReader<FixedRecord> Fixed() => new FixedLengthReaderBuilder<FixedRecord>()
        .Map(x => x.Id, 0, 8)
        .Map(x => x.Name, 8, 12)
        .Map(x => x.NumericField, 20, 10)
        .Map(x => x.Description, 30, 15)
        .Map(x => x.DecimalField, 45, 20)
        .Map(x => x.Code, 65, 5)
        .Map(x => x.Suffix, 70, 10)
        .Build(CultureInfo.InvariantCulture, () => new FixedRecord());
}

/// <summary>
/// RecordParser 2.3.0 parses on the thread pool by default (<c>ParallelismOptions.Enabled</c> is true, one worker per
/// core, original order kept). Every other library in the tables is single-threaded, so every RecordParser
/// benchmark gets its options from here, with parallel processing switched off. The setups of those benchmarks call
/// <see cref="AssertSingleThreaded"/>.
/// </summary>
internal static class RecordParserOptions
{
    private static ParallelismOptions Serial() => new() { Enabled = false };

    /// <summary>Header line, quote-aware (the 2.3.0 default, stated here so it cannot change unnoticed), one thread.</summary>
    public static VariableLengthReaderOptions Variable() => new()
    {
        HasHeader = true,
        ContainsQuotedFields = true,
        ParallelismOptions = Serial(),
    };

    public static FixedLengthReaderOptions<FixedRecord> Fixed(FuncSpanT<FixedRecord> parser) => new()
    {
        Parser = parser,
        ParallelismOptions = Serial(),
    };

    /// <summary>
    /// Fails unless the first 20,000 lines of the file are parsed on the calling thread only. It runs the real
    /// options with a parser that records the thread it runs on; with parallel processing on, the same call reports
    /// worker threads.
    /// </summary>
    public static void AssertSingleThreaded(string path, bool fixedWidth)
    {
        var threads = new ConcurrentDictionary<int, bool>();
        int caller = Environment.CurrentManagedThreadId;
        var lines = File.ReadLines(path).Take(20_000);
        using var text = new StringReader(string.Join('\n', lines));
        if (fixedWidth)
        {
            var parser = RecordParserReaders.Fixed();
            foreach (var _ in text.ReadRecords(Fixed(span =>
            {
                threads[Environment.CurrentManagedThreadId] = true;
                return parser.Parse(span);
            })))
            {
            }
        }
        else
        {
            var reader = new VariableLengthReaderBuilder<QuotedCompetitorRow>()
                .Map(x => x.Id, 0, span =>
                {
                    threads[Environment.CurrentManagedThreadId] = true;
                    return int.Parse(span, provider: CultureInfo.InvariantCulture);
                })
                .Build(",", CultureInfo.InvariantCulture, () => new QuotedCompetitorRow());
            foreach (var _ in text.ReadRecords(reader, Variable()))
            {
            }
        }
        DataCheck.That(threads.Count == 1 && threads.ContainsKey(caller),
            $"RecordParser must parse on the calling thread only, it used {threads.Count} thread(s).");
    }
}

/// <summary>The FlameCsv and RecordParser read loops for the PackageAssets file, shared by the classes that compare on it.</summary>
internal static class CompetitorRuns
{
    public static List<PackageAssetRow> FlamePackageAssets(string path, int capacity)
    {
        var results = new List<PackageAssetRow>(capacity);
        using var stream = File.OpenRead(path);
        foreach (var r in CsvReader.Read(stream, PackageAssetFlameMap.Default, CsvOptions<byte>.Default))
            results.Add(r);
        return results;
    }

    public static long FlamePackageAssetsDiscard(string path)
    {
        long checksum = 0;
        using var stream = File.OpenRead(path);
        foreach (var r in CsvReader.Read(stream, PackageAssetFlameMap.Default, CsvOptions<byte>.Default))
            checksum += r.Id;
        return checksum;
    }

    public static List<PackageAssetRow> RecordParserPackageAssets(IVariableLengthReader<PackageAssetRow> parser, string path, int capacity)
    {
        var results = new List<PackageAssetRow>(capacity);
        using var reader = new StreamReader(path);
        foreach (var r in reader.ReadRecords(parser, RecordParserOptions.Variable()))
            results.Add(r);
        return results;
    }

    public static long RecordParserPackageAssetsDiscard(IVariableLengthReader<PackageAssetRow> parser, string path)
    {
        long checksum = 0;
        using var reader = new StreamReader(path);
        foreach (var r in reader.ReadRecords(parser, RecordParserOptions.Variable()))
            checksum += r.Id;
        return checksum;
    }

    public static List<FixedRecord> RecordParserFixed(IFixedLengthReader<FixedRecord> parser, string path, int capacity)
    {
        var results = new List<FixedRecord>(capacity);
        using var reader = new StreamReader(path);
        foreach (var r in reader.ReadRecords(RecordParserOptions.Fixed(parser.Parse)))
            results.Add(r);
        return results;
    }

    public static long RecordParserFixedDiscard(IFixedLengthReader<FixedRecord> parser, string path)
    {
        long checksum = 0;
        using var reader = new StreamReader(path);
        foreach (var r in reader.ReadRecords(RecordParserOptions.Fixed(parser.Parse)))
            checksum += r.Id[^1];
        return checksum;
    }
}
