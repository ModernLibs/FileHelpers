using System.Text;
using ModernLibs.FileHelpers.Tests.Compat;

namespace ModernLibs.FileHelpers.Tests.Fuzz.DiffFuzz;

/// <summary>
/// Shape-aware differential input for the <see cref="Compat49Matrix"/> shapes: a file of valid rows of the shape with
/// one row in the middle whose one field is replaced by a generated record. The surrounding rows give the fuzzed field
/// a typed context, so the per-field code of the descriptor (empty values, null handling, trim, unquote, assignment)
/// runs on generated text, and the <c>RecycleRecords</c> read parses several records in a row.
/// </summary>
/// <remarks>
/// <para>The file is: the shape's header line (if any), its valid rows, the fuzzed row, its valid rows again, its
/// footer line (if any); every line ends with CRLF.</para>
/// <para>Delimited shapes: the field bounds are the separators of the shape in the row (<c>::</c> and <c>|</c> for
/// <c>Compat49PerFieldDelimiter</c>, CRLF for the <c>[FieldInNewLine]</c> field of <c>Compat49NewLine</c>, <c>;</c> for
/// <c>Compat49Struct</c>, <c>,</c> otherwise). A separator inside a quoted value counts as a bound too: that is one
/// more mutation. Fixed-length shapes: the slices 5, 8 and 6 of <c>Compat49Inputs</c>; the new text is padded or cut
/// to the width two times out of three and kept as generated otherwise.</para>
/// <para>Every pick (row, field, width) is drawn from the <see cref="LineGenerator"/>, so its seed is the only source
/// of randomness. This file is also compiled into <c>ModernLibs.FileHelpers.Fuzz.Tests</c> (a linked source file).</para>
/// </remarks>
internal static class ShapeInput
{
    /// <summary>The slice widths of the four <c>Compat49Fixed...</c> shapes (Id 5 right, Name 8 left, Amount 6 right).</summary>
    private static readonly int[] FixedWidths = [5, 8, 6];

    private static readonly bool[] FixedRightAligned = [true, false, true];

    private static readonly string[] Comma = [","];

    /// <summary>The separators of the delimited shapes that do not use only <c>,</c>; longer ones first.</summary>
    private static readonly Dictionary<string, string[]> Separators = new(StringComparer.Ordinal)
    {
        ["Compat49PerFieldDelimiter"] = ["::", "|", ","],
        ["Compat49NewLine"] = ["\r\n", ","],
        ["Compat49Struct"] = [";"],
    };

    /// <summary>A generator for <paramref name="name"/>: its delimiter is the shape's record delimiter.</summary>
    public static LineGenerator CreateGenerator(string name, int seed) =>
        new(seed, string.Equals(name, "Compat49Struct", StringComparison.Ordinal) ? ';' : ',');

    /// <summary>The file of <see cref="Next"/> without the fuzzed row: what the valid rows alone read to.</summary>
    public static string Baseline(string name) => File(name, fuzzedRow: null);

    /// <summary>The next shape-aware input for <paramref name="name"/>.</summary>
    public static string Next(string name, LineGenerator generator)
    {
        ArgumentNullException.ThrowIfNull(generator);

        IReadOnlyList<string> rows = Compat49Inputs.ValidRows(name);
        string row = rows[generator.Next(rows.Count)];
        string fuzzed = IsFixed(name)
            ? ReplaceSlice(row, generator)
            : ReplaceField(row, Separators.GetValueOrDefault(name, Comma), generator);
        return File(name, fuzzed);
    }

    private static bool IsFixed(string name) => name.StartsWith("Compat49Fixed", StringComparison.Ordinal);

    private static string ReplaceField(string row, string[] separators, LineGenerator generator)
    {
        // Field spans between separator matches.
        var starts = new List<int> { 0 };
        var ends = new List<int>();
        for (int i = 0; i < row.Length;)
        {
            string? match = null;
            foreach (string separator in separators)
            {
                if (string.CompareOrdinal(row, i, separator, 0, separator.Length) == 0)
                {
                    match = separator;
                    break;
                }
            }

            if (match is null)
            {
                i++;
                continue;
            }

            ends.Add(i);
            i += match.Length;
            starts.Add(i);
        }

        ends.Add(row.Length);

        int field = generator.Next(starts.Count);
        string text = generator.NextRecord(1);
        return string.Concat(row.AsSpan(0, starts[field]), text, row.AsSpan(ends[field]));
    }

    private static string ReplaceSlice(string row, LineGenerator generator)
    {
        int field = generator.Next(FixedWidths.Length);
        int start = 0;
        for (int i = 0; i < field; i++)
            start += FixedWidths[i];
        start = Math.Min(start, row.Length);
        int end = Math.Min(start + FixedWidths[field], row.Length);

        string text = generator.NextRecord(1);
        if (generator.Next(3) < 2)
        {
            int width = FixedWidths[field];
            text = text.Length >= width ? text[..width]
                : FixedRightAligned[field] ? text.PadLeft(width) : text.PadRight(width);
        }

        return string.Concat(row.AsSpan(0, start), text, row.AsSpan(end));
    }

    private static string File(string name, string? fuzzedRow)
    {
        var file = new StringBuilder();
        if (Compat49Inputs.Header(name) is { } header)
            file.Append(header).Append("\r\n");
        IReadOnlyList<string> rows = Compat49Inputs.ValidRows(name);
        foreach (string row in rows)
            file.Append(row).Append("\r\n");
        if (fuzzedRow is not null)
            file.Append(fuzzedRow).Append("\r\n");
        foreach (string row in rows)
            file.Append(row).Append("\r\n");
        if (Compat49Inputs.Footer(name) is { } footer)
            file.Append(footer).Append("\r\n");
        return file.ToString();
    }
}
