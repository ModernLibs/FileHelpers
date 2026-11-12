using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;

/// <summary>The plain (unquoted) datasets of the tokenizer benchmarks.</summary>
public enum TokenizerDataset
{
    /// <summary>10,000 lines of the 25-column PackageAssets file (header skipped).</summary>
    Columns25,

    /// <summary>1,000 lines of the 150-column file.</summary>
    Columns150,

    /// <summary>5,000 lines of 505 characters, 8 columns.</summary>
    LongLines,
}

/// <summary>Datasets and reference results shared by the component benchmarks and <c>--check-prototypes</c>.</summary>
internal static class ComponentData
{
    /// <summary>The <c>Data</c> folder next to the benchmark assembly, where the datasets are cached.</summary>
    public static string DataDir()
    {
        string dir = Path.Combine(Path.GetDirectoryName(typeof(ComponentData).Assembly.Location)!, "Data");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>The data lines of <paramref name="dataset"/>, generated in <paramref name="dataDir"/> when missing.</summary>
    public static string[] Lines(TokenizerDataset dataset, string dataDir) => dataset switch
    {
        TokenizerDataset.Columns25 => DataCheck.DataLines(DatasetGenerator.EnsurePackageAssets(dataDir, 10_000), hasHeader: true),
        TokenizerDataset.Columns150 => DataCheck.DataLines(DatasetGenerator.EnsureWideRecords(dataDir, 1_000), hasHeader: false),
        TokenizerDataset.LongLines => DataCheck.DataLines(DatasetGenerator.EnsureLongLines(dataDir, 5_000), hasHeader: false),
        _ => throw new ArgumentOutOfRangeException(nameof(dataset), dataset, "unknown dataset"),
    };

    /// <summary>The field count of every line of <paramref name="dataset"/>.</summary>
    public static int FieldCount(TokenizerDataset dataset) => dataset switch
    {
        TokenizerDataset.Columns25 => 25,
        TokenizerDataset.Columns150 => DatasetGenerator.WideColumns,
        TokenizerDataset.LongLines => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(dataset), dataset, "unknown dataset"),
    };

    /// <summary>
    /// The fields of an RFC 4180 line, with the rules of the seeded quoted set: a quote opens a quoted value only as
    /// the first character of a field, a doubled quote inside it is an escaped quote, and the value ends at its
    /// closing quote, which the delimiter follows. A reference for the checks, written without the library's
    /// scanner; it throws on anything else.
    /// </summary>
    /// <returns>Per field: start, end (exclusive, quotes included) and whether the field is quoted.</returns>
    public static List<(int Start, int End, bool Quoted)> QuotedFields(string line, char delimiter = ',', char quote = '"')
    {
        var fields = new List<(int, int, bool)>();
        int start = 0;
        while (true)
        {
            if (start < line.Length && line[start] == quote)
            {
                int i = start + 1;
                while (true)
                {
                    if (i >= line.Length)
                        throw new InvalidOperationException($"unclosed quote in '{line}'");
                    if (line[i] == quote)
                    {
                        if (i + 1 < line.Length && line[i + 1] == quote)
                        {
                            i += 2;
                            continue;
                        }
                        break;
                    }
                    i++;
                }
                int end = i + 1;
                fields.Add((start, end, true));
                if (end == line.Length)
                    return fields;
                if (line[end] != delimiter)
                    throw new InvalidOperationException($"text after a closing quote in '{line}'");
                start = end + 1;
            }
            else
            {
                int found = line.AsSpan(start).IndexOf(delimiter);
                if (found < 0)
                {
                    fields.Add((start, line.Length, false));
                    return fields;
                }
                int next = start + found;
                fields.Add((start, next, false));
                start = next + 1;
            }
        }
    }
}
