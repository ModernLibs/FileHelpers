using System.Globalization;

namespace ModernLibs.FileHelpers.Benchmarks.Data;

/// <summary>
/// Correctness guards run from each benchmark's <c>[GlobalSetup]</c>: every benchmark method is called
/// once and its result is compared with the raw text of the dataset, so a benchmark that parses the
/// data wrongly fails instead of measuring garbage.
/// </summary>
public static class DataCheck
{
    /// <summary>PackageAssets columns compared by <see cref="PackageAssets"/>, by zero-based index.</summary>
    /// <remarks>Id, Version, DownloadCount, MinClientVersion (empty), IsPrerelease, Published, LastUpdated,
    /// PackageSize, RepositoryType (empty on every fourth row), RequireLicenseAcceptance.</remarks>
    private static readonly int[] s_packageColumns = [0, 1, 5, 11, 13, 14, 15, 16, 22, 24];

    public static void That(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Benchmark correctness check failed: " + message);
    }

    /// <summary>Fails unless <paramref name="actual"/> equals <paramref name="expected"/>.</summary>
    public static void Equal<T>(T expected, T actual, string what)
        => That(EqualityComparer<T>.Default.Equals(expected, actual), $"{what}: expected '{expected}', got '{actual}'.");

    /// <summary>Data lines of a file (header skipped when <paramref name="hasHeader"/>), without line endings.</summary>
    public static string[] DataLines(string path, bool hasHeader)
        => File.ReadLines(path).Skip(hasHeader ? 1 : 0).ToArray();

    /// <summary>Lines of written text, accepting both <c>\n</c> and <c>\r\n</c>, without a trailing empty line.</summary>
    public static string[] TextLines(string text)
    {
        var lines = text.Split('\n');
        int count = lines.Length > 0 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
        var result = new string[count];
        for (int i = 0; i < count; i++)
            result[i] = lines[i].TrimEnd('\r');
        return result;
    }

    /// <summary>
    /// Checks rows parsed from the PackageAssets dataset: the row count, and for the first, fourth and
    /// last rows the columns listed in <see cref="s_packageColumns"/>, formatted back to text by
    /// <paramref name="project"/> and compared with the raw line.
    /// </summary>
    /// <param name="library">Name used in the failure message.</param>
    /// <param name="dataLines">Data lines of the file (from <see cref="DataLines"/>, header skipped).</param>
    /// <param name="count">Number of parsed rows.</param>
    /// <param name="project">Returns the compared columns of row <c>i</c>, in the order of <see cref="s_packageColumns"/>.</param>
    public static void PackageAssets(string library, string[] dataLines, int count, Func<int, string?[]> project)
    {
        Equal(dataLines.Length, count, library + " row count");
        foreach (var row in new[] { 0, 3, count - 1 })
        {
            if (row < 0 || row >= count) continue;
            var raw = dataLines[row].Split(',');
            var actual = project(row);
            for (int c = 0; c < s_packageColumns.Length; c++)
                Equal(raw[s_packageColumns[c]], actual[c] ?? "", $"{library} row {row} column {s_packageColumns[c]}");
        }
    }

    /// <summary>Formats the compared PackageAssets columns the way the dataset generator writes them.</summary>
    public static string?[] PackageColumns(
        int id, string? version, int downloadCount, string? minClientVersion, bool isPrerelease,
        DateTime published, DateTime lastUpdated, long packageSize, string? repositoryType, bool requireLicenseAcceptance)
        =>
        [
            id.ToString(CultureInfo.InvariantCulture),
            version,
            downloadCount.ToString(CultureInfo.InvariantCulture),
            minClientVersion,
            isPrerelease.ToString(),
            published.ToString(DatasetGenerator.DateFormat, CultureInfo.InvariantCulture),
            lastUpdated.ToString(DatasetGenerator.DateFormat, CultureInfo.InvariantCulture),
            packageSize.ToString(CultureInfo.InvariantCulture),
            repositoryType,
            requireLicenseAcceptance.ToString(),
        ];

    /// <summary>
    /// Checks rows parsed from the quoted-fields dataset: the row count, and for the first ten rows (every
    /// description and note variant) and the last row the unquoted Id, Description, Notes and Category.
    /// </summary>
    public static void QuotedFields(string library, int expectedRows, int count, Func<int, (int Id, string? Description, string? Notes, string? Category)> get)
    {
        Equal(expectedRows, count, library + " row count");
        var categories = "ABCDE";
        foreach (var row in Enumerable.Range(0, Math.Min(10, count)).Append(count - 1))
        {
            var (id, description, notes, category) = get(row);
            Equal(row, id, $"{library} row {row} Id");
            Equal(DatasetGenerator.QuotedDescriptions[row % 10], description ?? "", $"{library} row {row} Description");
            Equal(DatasetGenerator.QuotedNotes[row % 10], notes ?? "", $"{library} row {row} Notes");
            Equal(categories[row % 5].ToString(), category ?? "", $"{library} row {row} Category");
        }
    }

    /// <summary>
    /// Checks rows parsed from the fixed-width dataset: the row count, and for the first and last rows
    /// that the seven fields, concatenated, give back the raw 80-character line.
    /// </summary>
    public static void FixedWidth(string library, string[] dataLines, int count, Func<int, string> concatFields)
    {
        Equal(dataLines.Length, count, library + " row count");
        foreach (var row in new[] { 0, count - 1 })
            Equal(dataLines[row], concatFields(row), $"{library} row {row}");
    }

    /// <summary>
    /// Checks text written from the PackageAssets records: it must be the dataset file, header line included,
    /// line for line (line endings aside). FileHelpers writes back the header it skipped on read
    /// (<c>HeaderText</c>), as 3.x does; the other writers write their header from the column names, and
    /// all of them are configured to write dates in the generator's format.
    /// </summary>
    public static void WrittenPackageAssets(string library, string[] fileLines, string written)
    {
        var lines = TextLines(written);
        Equal(fileLines.Length, lines.Length, library + " written line count");
        for (int row = 0; row < lines.Length; row++)
        {
            if (!string.Equals(fileLines[row], lines[row], StringComparison.Ordinal))
                Equal(fileLines[row], lines[row], $"{library} written line {row}");
        }
    }

    /// <summary>
    /// Checks rows parsed from the seeded quoted-fields dataset (<see cref="DatasetGenerator.EnsureQuotedFieldsRandom"/>):
    /// the row count, that all ten patterns occur and the order is not the periodic one, and for every row
    /// the Id, Description, Notes and Category.
    /// </summary>
    public static void QuotedRandom(string library, int expectedRows, int count, Func<int, (int Id, string? Description, string? Notes, string? Category)> get)
    {
        Equal(expectedRows, count, library + " row count");
        var patterns = DatasetGenerator.QuotedRandomPatterns(expectedRows);
        if (expectedRows >= 1000)
        {
            That(patterns.Distinct().Count() == DatasetGenerator.QuotedDescriptions.Length, "random quoted set: not all patterns occur");
            That(Enumerable.Range(0, expectedRows).Any(i => patterns[i] != i % 10), "random quoted set: patterns are periodic");
        }
        var categories = "ABCDE";
        for (int row = 0; row < count; row++)
        {
            var (id, description, notes, category) = get(row);
            Equal(row, id, $"{library} row {row} Id");
            Equal(DatasetGenerator.QuotedDescriptions[patterns[row]], description ?? "", $"{library} row {row} Description");
            Equal(DatasetGenerator.QuotedNotes[patterns[row]], notes ?? "", $"{library} row {row} Notes");
            Equal(categories[row % 5].ToString(), category ?? "", $"{library} row {row} Category");
        }
    }

    /// <summary>
    /// Checks rows parsed from the sparse-quotes dataset: the row count, that between 0.5 % and 1.5 % of the
    /// lines contain a double quote, and for every row the Id, Description and Notes.
    /// </summary>
    public static void SparseQuotes(string library, string path, int expectedRows, int count, Func<int, (int Id, string? Description, string? Notes)> get)
    {
        Equal(expectedRows, count, library + " row count");
        var lines = DataLines(path, hasHeader: true);
        Equal(expectedRows, lines.Length, library + " file data lines");
        long quoted = lines.Count(l => l.Contains('"'));
        That(quoted * 1000 >= (long)expectedRows * 5 && quoted * 1000 <= (long)expectedRows * 15,
            $"sparse quotes: {quoted} of {expectedRows} lines contain a quote, expected 0.5 % to 1.5 %");
        for (int row = 0; row < count; row++)
        {
            var (id, description, notes) = get(row);
            Equal(row, id, $"{library} row {row} Id");
            Equal(DatasetGenerator.SparseDescription(row), description ?? "", $"{library} row {row} Description");
            Equal(DatasetGenerator.SparseNote(row), notes ?? "", $"{library} row {row} Notes");
        }
    }

    /// <summary>
    /// Checks rows parsed from the long-lines dataset: the row count, that every data line has at least
    /// <see cref="DatasetGenerator.LongLineMinLength"/> characters, and that every record, formatted back by
    /// <paramref name="text"/>, gives the raw line.
    /// </summary>
    public static void LongLines(string library, string path, int expectedRows, int count, Func<int, string> text)
    {
        Equal(expectedRows, count, library + " row count");
        var lines = DataLines(path, hasHeader: false);
        Equal(expectedRows, lines.Length, library + " file data lines");
        for (int row = 0; row < lines.Length; row++)
        {
            That(lines[row].Length >= DatasetGenerator.LongLineMinLength, $"long lines: line {row} has only {lines[row].Length} characters");
            Equal(lines[row], text(row), $"{library} row {row}");
        }
    }

    /// <summary>
    /// Checks rows parsed from the multiline dataset: the row count, that the file has the expected number of
    /// line breaks (one per record plus one in each multiline value), and for every row the Id and the text.
    /// </summary>
    public static void Multiline(string library, string path, int expectedRows, int count, Func<int, (int Id, string? Text)> get)
    {
        Equal(expectedRows, count, library + " row count");
        long breaks = 0;
        foreach (char c in File.ReadAllText(path))
        {
            if (c == '\n') breaks++;
        }
        Equal((long)expectedRows + DatasetGenerator.MultilineRowCount(expectedRows), breaks, "multiline file line breaks");
        for (int row = 0; row < count; row++)
        {
            var (id, value) = get(row);
            Equal(row, id, $"{library} row {row} Id");
            Equal(DatasetGenerator.MultilineText(row), value ?? "", $"{library} row {row} Text");
        }
    }

    /// <summary>
    /// Checks rows parsed from the 150-column dataset: the row count, that each line has 150 fields (even
    /// columns digits only, odd columns not), and that every record formatted back by <paramref name="text"/> gives the raw line.
    /// </summary>
    public static void Wide(string library, string path, int expectedRows, int count, Func<int, string> text)
    {
        Equal(expectedRows, count, library + " row count");
        var lines = DataLines(path, hasHeader: false);
        Equal(expectedRows, lines.Length, library + " file data lines");
        for (int row = 0; row < lines.Length; row++)
        {
            var fields = lines[row].Split(',');
            Equal(DatasetGenerator.WideColumns, fields.Length, $"wide row {row} field count");
            for (int c = 0; c < fields.Length; c++)
            {
                // Even columns are int (digits only), odd columns are string (8 characters, never digits only).
                bool digits = fields[c].All(char.IsAsciiDigit);
                That(digits == (c % 2 == 0), $"wide row {row} column {c}: '{fields[c]}' has the wrong kind");
            }
            Equal(lines[row], text(row), $"{library} row {row}");
        }
    }
}
