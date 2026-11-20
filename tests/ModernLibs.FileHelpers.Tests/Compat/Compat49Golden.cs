using System.Text;

namespace ModernLibs.FileHelpers.Tests.Compat;

/// <summary>
/// The golden canonical texts of the 4.9.1 descriptors: <c>Golden49/&lt;Shape&gt;.txt</c>, embedded in the test
/// assembly. Each file has one section per input and kind of run:
/// <c>#### &lt;index&gt; &lt;input kind&gt; read</c> and <c>#### &lt;index&gt; &lt;input kind&gt; round-trip</c>, followed by
/// the text of <see cref="Compat49Matrix"/> for the old descriptor. Canonical lines are never empty and never start
/// with <c>####</c>.
/// </summary>
/// <remarks>
/// The files were captured when the library source equalled <c>3a045e0</c> (tag <c>v4.9.1</c>; only the project
/// file differed), so they hold 4.9.1 behaviour. Regenerating them needs a reviewed reason: see
/// <c>Generated49/README.md</c>.
/// </remarks>
internal static class Compat49Golden
{
    /// <summary>Set to a directory to write the golden files there instead of checking them.</summary>
    public const string RegenerateVariable = "COMPAT49_GOLDEN_DIR";

    public static string? RegenerateDirectory => Environment.GetEnvironmentVariable(RegenerateVariable) is { Length: > 0 } dir ? dir : null;

    public static string Header(int index, string run) => $"#### {index} {Compat49Inputs.Kinds[index]} {run}";

    /// <summary>The golden text of one section, or null when the file or the section is missing.</summary>
    public static string? Expected(string shape, int index, string run)
    {
        var sections = Load(shape);
        return sections is not null && sections.TryGetValue(Header(index, run), out string? text) ? text : null;
    }

    /// <summary>Writes the golden file of <paramref name="shape"/> to <paramref name="directory"/>.</summary>
    public static void Write(string directory, string shape, IEnumerable<(string Header, string Text)> sections)
    {
        var file = new StringBuilder();
        foreach (var (header, text) in sections)
            file.Append(header).Append('\n').Append(text);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, shape + ".txt"), file.ToString(), new UTF8Encoding(false));
    }

    private static Dictionary<string, string>? Load(string shape)
    {
        using var stream = typeof(Compat49Golden).Assembly.GetManifestResourceStream("Golden49/" + shape + ".txt");
        if (stream is null)
            return null;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        string? header = null;
        var text = new StringBuilder();
        foreach (string line in reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.Length == 0)
                continue;
            if (line.StartsWith("#### ", StringComparison.Ordinal))
            {
                if (header is not null)
                    sections[header] = text.ToString();
                header = line;
                text.Clear();
            }
            else
            {
                text.Append(line).Append('\n');
            }
        }

        if (header is not null)
            sections[header] = text.ToString();
        return sections;
    }
}
