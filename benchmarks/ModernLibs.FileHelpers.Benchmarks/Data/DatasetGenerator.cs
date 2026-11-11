// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004

using System.Globalization;
using System.Text;

namespace ModernLibs.FileHelpers.Benchmarks.Data;

/// <summary>Generates deterministic benchmark datasets.</summary>
/// <remarks>
/// Every dataset is a pure function of its row count: each generator uses its own <see cref="Random"/>
/// with a fixed seed and dates are offsets from <see cref="BaseDate"/>, so two runs (or two machines)
/// produce byte-identical files. The file names carry a format version (<c>v2</c> for the first datasets, <c>v1</c> for those added in 4.10); bump it whenever the
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

    /// <summary>
    /// In <see cref="EnsurePackageAssetsBadEvery100"/>, the rows whose zero-based index is <c>BadRowEvery - 1</c>
    /// modulo this value have a bad integer: <c>rows / BadRowEvery</c> of them when <c>rows</c> is a multiple.
    /// </summary>
    public const int BadRowEvery = 100;

    /// <summary>
    /// The PackageAssets file of <see cref="EnsurePackageAssets"/> with an <c>x</c> before the DownloadCount (column 6)
    /// of every 100th data row (rows 99, 199, ...), so that each of those rows fails to convert.
    /// </summary>
    public static string EnsurePackageAssetsBadEvery100(string dataDir, int rows)
    {
        var path = Path.Combine(dataDir, $"package_assets_bad100_v1_{rows}.csv");
        if (File.Exists(path)) return path;

        string source = EnsurePackageAssets(dataDir, rows);
        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);
        int row = -1; // the header line
        foreach (string line in File.ReadLines(source))
        {
            if (row % BadRowEvery == BadRowEvery - 1)
            {
                var fields = line.Split(',');
                fields[5] = "x" + fields[5];
                writer.WriteLine(string.Join(',', fields));
            }
            else
            {
                writer.WriteLine(line);
            }
            row++;
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

    /// <summary>Per-row pattern index (0 to 9) of the seeded quoted-fields dataset: row i uses description and note <c>[pattern]</c>.</summary>
    public static int[] QuotedRandomPatterns(int rows)
    {
        var rng = new Random(Seed);
        var patterns = new int[rows];
        for (int i = 0; i < rows; i++)
            patterns[i] = rng.Next(QuotedDescriptions.Length);
        return patterns;
    }

    /// <summary>
    /// The quoted-fields dataset with the same five columns and ten patterns as <see cref="EnsureQuotedFields"/>,
    /// but the pattern of each row is drawn from a seeded <see cref="Random"/> instead of cycling, so the
    /// quote pattern changes unpredictably from row to row (branch predictors cannot learn it).
    /// </summary>
    public static string EnsureQuotedFieldsRandom(string dataDir, int rows = 100_000)
    {
        var path = Path.Combine(dataDir, $"quoted_random_v1_{rows}.csv");
        if (File.Exists(path)) return path;

        var patterns = QuotedRandomPatterns(rows);
        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);
        writer.WriteLine("Id,Name,Description,Notes,Category");

        var categories = new[] { "A", "B", "C", "D", "E" };
        var names = new[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon" };

        for (int i = 0; i < rows; i++)
        {
            var desc = Rfc4180Escape(QuotedDescriptions[patterns[i]]);
            var note = Rfc4180Escape(QuotedNotes[patterns[i]]);
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{i},{names[i % names.Length]},{desc},{note},{categories[i % categories.Length]}"));
        }

        return path;
    }

    private static readonly string[] s_plainDescriptions = ["Simple text", "Normal text", "Plain"];
    private static readonly string[] s_plainNotes = ["No notes", "Regular note", "Short"];

    /// <summary>Rows whose index is a multiple of this contain quotes in the sparse-quotes dataset (1 % of the rows).</summary>
    public const int SparseQuoteEvery = 100;

    /// <summary>Description of row <paramref name="i"/> of the sparse-quotes dataset, unescaped.</summary>
    public static string SparseDescription(int i)
        => i % SparseQuoteEvery == 0 ? "Text with \"quotes\", and comma" : s_plainDescriptions[i % s_plainDescriptions.Length];

    /// <summary>Notes of row <paramref name="i"/> of the sparse-quotes dataset, unescaped.</summary>
    public static string SparseNote(int i)
        => i % SparseQuoteEvery == 0 ? "Important \"flag\"" : s_plainNotes[i % s_plainNotes.Length];

    /// <summary>
    /// The quoted-fields layout (header, Id, Name, Description, Notes, Category) where only 1 % of the rows contain
    /// a quote; every other row has plain text and no delimiter or quote inside a value.
    /// </summary>
    public static string EnsureSparseQuotes(string dataDir, int rows = 100_000)
    {
        var path = Path.Combine(dataDir, $"sparse_quotes_v1_{rows}.csv");
        if (File.Exists(path)) return path;

        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);
        writer.WriteLine("Id,Name,Description,Notes,Category");

        var categories = new[] { "A", "B", "C", "D", "E" };
        var names = new[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon" };

        for (int i = 0; i < rows; i++)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{i},{names[i % names.Length]},{Rfc4180Escape(SparseDescription(i))},{Rfc4180Escape(SparseNote(i))},{categories[i % categories.Length]}"));
        }

        return path;
    }

    /// <summary>Length every data line of the long-lines dataset reaches at least.</summary>
    public const int LongLineMinLength = 500;

    /// <summary>
    /// Delimited file without header: an 8-digit Id and seven random alphanumeric columns of 70 characters,
    /// 505 characters per line.
    /// </summary>
    public static string EnsureLongLines(string dataDir, int rows = 20_000)
    {
        var path = Path.Combine(dataDir, $"long_lines_v1_{rows}.csv");
        if (File.Exists(path)) return path;

        var rng = new Random(Seed);
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);
        for (int i = 0; i < rows; i++)
        {
            var sb = new StringBuilder(LongLineMinLength + 16);
            sb.Append(i.ToString("D8", CultureInfo.InvariantCulture));
            for (int c = 0; c < 7; c++)
                sb.Append(',').Append(RandomString(rng, chars, 70));
            writer.WriteLine(sb.ToString());
        }

        return path;
    }

    /// <summary>Text of row <paramref name="i"/> of the multiline dataset, unescaped: every tenth row holds one line break.</summary>
    public static string MultilineText(int i)
        => i % 10 == 0
            ? string.Create(CultureInfo.InvariantCulture, $"First line of {i}\nsecond line of {i}")
            : string.Create(CultureInfo.InvariantCulture, $"Single line text {i}");

    /// <summary>Number of rows of a <paramref name="rows"/>-row multiline dataset that hold a line break.</summary>
    public static int MultilineRowCount(int rows) => (rows + 9) / 10;

    /// <summary>
    /// Delimited file without header (Id, Name, Text, Category) where every tenth row has a quoted Text with
    /// one embedded line break.
    /// </summary>
    public static string EnsureMultilineValues(string dataDir, int rows = 100_000)
    {
        var path = Path.Combine(dataDir, $"multiline_v1_{rows}.csv");
        if (File.Exists(path)) return path;

        var categories = new[] { "A", "B", "C", "D", "E" };
        var names = new[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon" };
        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);
        for (int i = 0; i < rows; i++)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{i},{names[i % names.Length]},{Rfc4180Escape(MultilineText(i))},{categories[i % categories.Length]}"));
        }

        return path;
    }

    /// <summary>Column count of the wide dataset: even columns (0, 2, ...) are int, odd columns are string.</summary>
    public const int WideColumns = 150;

    /// <summary>
    /// Delimited file without header with <see cref="WideColumns"/> columns: 75 int columns (random 0 to 999,999)
    /// alternating with 75 string columns (8 random alphanumeric characters).
    /// </summary>
    public static string EnsureWideRecords(string dataDir, int rows = 10_000)
    {
        var path = Path.Combine(dataDir, $"wide_150_v1_{rows}.csv");
        if (File.Exists(path)) return path;

        var rng = new Random(Seed);
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        using var writer = new StreamWriter(path, false, Encoding.UTF8, 65536);
        var sb = new StringBuilder(2048);
        for (int i = 0; i < rows; i++)
        {
            sb.Clear();
            for (int c = 0; c < WideColumns; c++)
            {
                if (c > 0) sb.Append(',');
                if (c % 2 == 0)
                    sb.Append(rng.Next(0, 1_000_000).ToString(CultureInfo.InvariantCulture));
                else
                    sb.Append(RandomString(rng, chars, 8));
            }
            writer.WriteLine(sb.ToString());
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
