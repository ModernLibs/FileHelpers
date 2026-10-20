// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004

using System.Globalization;
using System.Text;

namespace ModernLibs.FileHelpers.Benchmarks.Data;

/// <summary>Generates deterministic benchmark datasets.</summary>
/// <remarks>
/// Every dataset is a pure function of its row count: each generator uses its own <see cref="Random"/>
/// with a fixed seed and dates are offsets from <see cref="BaseDate"/>, so two runs (or two machines)
/// produce byte-identical files. The file names carry a format version (<c>v2</c>); bump it whenever the
/// generated text changes so that stale files cached next to the benchmark assembly are not reused.
/// </remarks>
public static class DatasetGenerator
{
    private const int Seed = 42;

    /// <summary>Fixed reference date for generated dates (replaces <c>DateTime.UtcNow</c>).</summary>
    public static readonly DateTime BaseDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// Format of the date columns of the PackageAssets dataset: the sortable ISO 8601 pattern
    /// <c>yyyy-MM-ddTHH:mm:ss</c>, without fraction or offset. FileHelpers (4.8 and 3.x) parse it with
    /// <c>[FieldConverter(ConverterKind.Date, "s")]</c>; the CSV readers parse it with their default
    /// invariant <c>DateTime</c> parsing.
    /// </summary>
    public const string DateFormat = "s";

    /// <summary>Description column values of the quoted-fields dataset (row i uses index i % 10), unescaped.</summary>
    public static readonly string[] QuotedDescriptions =
    [
        "Simple text",
        "Text with, comma",
        "Text with \"quotes\"",
        "Text with, \"commas and quotes\"",
        "Normal text",
        "Line with embedded \"stuff, here\" and more",
        "Plain",
        "Has, multiple, commas",
        "Quote at \"start",
        "End with quote\"",
    ];

    /// <summary>Notes column values of the quoted-fields dataset (row i uses index i % 10), unescaped.</summary>
    public static readonly string[] QuotedNotes =
    [
        "No notes",
        "Note with, comma",
        "Important \"flag\"",
        "",
        "Regular note",
        "Note with \"embedded, comma\" value",
        "Short",
        "Long note with lots of text and, punctuation",
        "Special \"case\" note",
        "Trailing comma,",
    ];

    public static string EnsurePackageAssets(string dataDir, int rows = 1_000_000)
    {
        var path = Path.Combine(dataDir, $"package_assets_v2_{rows}.csv");
        if (File.Exists(path)) return path;

        var rng = new Random(Seed);
        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);
        writer.WriteLine("Id,Version,Authors,Description,Tags,DownloadCount,ProjectUrl,LicenseUrl,IconUrl,Language,Framework,MinClientVersion,Type,IsPrerelease,Published,LastUpdated,PackageSize,DependencyCount,HasReadme,HasLicense,HasIcon,IsSigned,RepositoryType,RepositoryUrl,RequireLicenseAcceptance");

        var descriptions = new[] { "A useful library", "High-performance framework", "Simple utility", "Modern toolkit", "Core component" };
        var tags = new[] { "library;framework", "utility;tool", "core;runtime", "data;parsing", "async;streaming" };
        var authors = new[] { "Author A", "Author B", "Author C", "Team X", "Org Y" };
        var langs = new[] { "en-US", "en-GB", "de-DE", "fr-FR", "ja-JP" };
        var frameworks = new[] { "net10.0", "net8.0", "netstandard2.0", "net6.0", "net472" };
        var repoTypes = new[] { "git", "svn", "hg", "" };

        for (int i = 0; i < rows; i++)
        {
            var version = string.Create(CultureInfo.InvariantCulture, $"{rng.Next(0, 10)}.{rng.Next(0, 20)}.{rng.Next(0, 100)}");
            var downloads = rng.Next(100, 10_000_000);
            var isPrerelease = rng.Next(0, 10) == 0;
            if (isPrerelease) version += string.Create(CultureInfo.InvariantCulture, $"-beta.{rng.Next(1, 5)}");
            var pubDate = BaseDate.AddDays(-rng.Next(0, 3650)).AddSeconds(rng.Next(0, 86_400));
            var size = rng.Next(1000, 50_000_000);
            var depCount = rng.Next(0, 20);

            // MinClientVersion (column 12) is always empty and RepositoryType (column 23) is empty on every
            // fourth row: both are string columns, so an empty value is a valid empty string.
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{i},{version},{authors[i % authors.Length]},{descriptions[i % descriptions.Length]},{tags[i % tags.Length]},{downloads},https://example.com/{i},https://license.example.com/{i},https://icon.example.com/{i},{langs[i % langs.Length]},{frameworks[i % frameworks.Length]},,{rng.Next(0, 3)},{isPrerelease},{pubDate:s},{pubDate.AddDays(1):s},{size},{depCount},{rng.Next(0, 2) == 1},{rng.Next(0, 2) == 1},{rng.Next(0, 2) == 1},{rng.Next(0, 2) == 1},{repoTypes[i % repoTypes.Length]},https://repo.example.com/{i},{rng.Next(0, 2) == 1}"));
        }

        return path;
    }

    public static string EnsureFloats(string dataDir, int rows = 100_000)
    {
        var path = Path.Combine(dataDir, $"floats_v2_{rows}.csv");
        if (File.Exists(path)) return path;

        var rng = new Random(Seed);
        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);
        writer.WriteLine("Id,Value1,Value2,Value3,Value4,Value5,Value6,Value7,Value8,Value9,Value10");

        for (int i = 0; i < rows; i++)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{i},{rng.NextDouble() * 1e9},{rng.NextDouble() * 1e6},{rng.NextDouble() * 1e3},{rng.NextDouble() * 100},{rng.NextDouble() * 10},{rng.NextDouble()},{rng.NextDouble() * 0.001},{rng.NextDouble() * 0.000001},{rng.NextDouble() * 1e-9},{rng.NextDouble() * 1e-12}"));
        }

        return path;
    }

    public static string EnsureFixedWidth(string dataDir, int rows = 500_000)
    {
        var path = Path.Combine(dataDir, $"fixed_width_v2_{rows}.txt");
        if (File.Exists(path)) return path;

        var rng = new Random(Seed);
        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);

        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        for (int i = 0; i < rows; i++)
        {
            // 80-char record: 8 + 12 + 10 + 15 + 20 + 5 + 10 = 80
            var sb = new StringBuilder(80);
            sb.Append(i.ToString("D8", CultureInfo.InvariantCulture));
            sb.Append(RandomString(rng, chars, 12));
            sb.Append(rng.Next(0, 1_000_000_000).ToString("D10", CultureInfo.InvariantCulture));
            sb.Append(RandomString(rng, chars, 15));
            sb.Append(rng.NextDouble().ToString("F10", CultureInfo.InvariantCulture).PadLeft(20, '0'));
            sb.Append(rng.Next(0, 100).ToString("D5", CultureInfo.InvariantCulture));
            sb.Append(RandomString(rng, chars, 10));
            writer.WriteLine(sb.ToString());
        }

        return path;
    }

    private static string RandomString(Random rng, string chars, int length)
    {
        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[rng.Next(chars.Length)];
        return new string(result);
    }

    public static string EnsureQuotedFields(string dataDir, int rows = 100_000)
    {
        var path = Path.Combine(dataDir, $"quoted_fields_v2_{rows}.csv");
        if (File.Exists(path)) return path;

        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);
        writer.WriteLine("Id,Name,Description,Notes,Category");

        var categories = new[] { "A", "B", "C", "D", "E" };
        var names = new[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon" };

        for (int i = 0; i < rows; i++)
        {
            var name = names[i % names.Length];
            var desc = Rfc4180Escape(QuotedDescriptions[i % QuotedDescriptions.Length]);
            var note = Rfc4180Escape(QuotedNotes[i % QuotedNotes.Length]);
            var cat = categories[i % categories.Length];
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{i},{name},{desc},{note},{cat}"));
        }

        return path;
    }

    private static string Rfc4180Escape(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
