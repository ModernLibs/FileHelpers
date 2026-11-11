using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;

/// <summary>The datasets of <see cref="CreateStringBenchmarks"/>.</summary>
public enum StringFieldDataset
{
    /// <summary>The 12 string columns of 10,000 lines of the 25-column file (two of them often empty).</summary>
    Columns25,

    /// <summary>The Name and Category columns of 10,000 lines of the seeded quoted set (Category is one character).</summary>
    Quoted,
}

/// <summary>
/// Creating the string of an unquoted text field (design B3, R4): <c>new string(span)</c>, as the generated parser
/// does today, against the R4 helper <c>CreateString</c> of design 4.6 (empty gives <see cref="string.Empty"/>, one
/// character below 128 a cached string, anything else <c>new string</c>).
/// </summary>
/// <remarks>
/// The field positions are found in the setup. Both benchmarks return the total length of the strings; each setup
/// compares it with the lengths from <see cref="string.Split(char, StringSplitOptions)"/> and checks every string's text.
/// </remarks>
[MemoryDiagnoser]
public class CreateStringBenchmarks
{
    private static readonly int[] s_columns25 = [1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 22, 23];

    // Name and Category: never quoted in the quoted set.
    private static readonly int[] s_quotedColumns = [1, 4];

    private static readonly string[] s_ascii = CreateAscii();

    private string[] _lines = [];

    // (line, start, length) of every string field.
    private int[] _fields = [];

    [Params(StringFieldDataset.Columns25, StringFieldDataset.Quoted)]
    public StringFieldDataset Dataset { get; set; }

    private static string[] CreateAscii()
    {
        var strings = new string[128];
        for (int c = 0; c < strings.Length; c++)
            strings[c] = ((char)c).ToString();
        return strings;
    }

    private void Prepare()
    {
        string dir = ComponentData.DataDir();
        var fields = new List<int>();
        bool columns25 = Dataset == StringFieldDataset.Columns25;
        _lines = columns25
            ? DataCheck.DataLines(DatasetGenerator.EnsurePackageAssets(dir, 10_000), hasHeader: true)
            : DataCheck.DataLines(DatasetGenerator.EnsureQuotedFieldsRandom(dir, 10_000), hasHeader: true);
        for (int li = 0; li < _lines.Length; li++)
        {
            // The reference splitter, not a benchmarked tokenizer: the setup runs no other benchmark's code.
            var bounds = ComponentData.QuotedFields(_lines[li]);
            DataCheck.Equal(columns25 ? 25 : 5, bounds.Count, $"line {li} field count");
            foreach (int c in columns25 ? s_columns25 : s_quotedColumns)
            {
                DataCheck.That(!bounds[c].Quoted, $"line {li} column {c} is quoted");
                fields.AddRange([li, bounds[c].Start, bounds[c].End - bounds[c].Start]);
            }
        }
        _fields = fields.ToArray();
    }

    private void Check(string name, Func<ReadOnlySpan<char>, string> create, long checksum)
    {
        long expected = 0;
        foreach (string line in _lines)
        {
            string[] parts = line.Split(',');
            if (Dataset == StringFieldDataset.Columns25)
            {
                foreach (int c in s_columns25)
                    expected += parts[c].Length;
            }
            else
            {
                expected += parts[1].Length + parts[^1].Length;
            }
        }
        for (int k = 0; k < _fields.Length; k += 3)
        {
            string text = _lines[_fields[k]].Substring(_fields[k + 1], _fields[k + 2]);
            DataCheck.Equal(text, create(text), $"{name} line {_fields[k]} field at {_fields[k + 1]}");
        }
        DataCheck.Equal(expected, checksum, name + " total length");
    }

    [GlobalSetup(Target = nameof(CreateString_New))]
    public void SetupNew()
    {
        Prepare();
        Check(nameof(CreateString_New), CreateNew, CreateString_New());
    }

    [GlobalSetup(Target = nameof(CreateString_Cached))]
    public void SetupCached()
    {
        Prepare();
        Check(nameof(CreateString_Cached), CreateCached, CreateString_Cached());
    }

    [Benchmark(Baseline = true)]
    public long CreateString_New()
    {
        long length = 0;
        string[] lines = _lines;
        int[] fields = _fields;
        for (int k = 0; k < fields.Length; k += 3)
            length += CreateNew(lines[fields[k]].AsSpan(fields[k + 1], fields[k + 2])).Length;
        return length;
    }

    [Benchmark]
    public long CreateString_Cached()
    {
        long length = 0;
        string[] lines = _lines;
        int[] fields = _fields;
        for (int k = 0; k < fields.Length; k += 3)
            length += CreateCached(lines[fields[k]].AsSpan(fields[k + 1], fields[k + 2])).Length;
        return length;
    }

    /// <summary>Today's generated code: <c>new string(span)</c>.</summary>
    internal static string CreateNew(ReadOnlySpan<char> value) => new(value);

    /// <summary>The R4 helper of design 4.6.</summary>
    internal static string CreateCached(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
            return string.Empty;
        if (value.Length == 1 && value[0] < 128)
            return s_ascii[value[0]];
        return new string(value);
    }
}
