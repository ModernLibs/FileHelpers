using System.Globalization;
using System.Text;

namespace ModernLibs.FileHelpers.Tests.Fuzz.DiffFuzz;

/// <summary>
/// Runs two read functions on the same input and compares what they produced. This file is also compiled into
/// <c>ModernLibs.FileHelpers.Fuzz.Tests</c> (a linked source file).
/// </summary>
/// <remarks>
/// <para>Each run ends in one of three outcomes: a returned text, a returned <see langword="null"/>, or an
/// exception. The two runs are equal only when the outcome kind is the same and the texts are equal ordinal, so a
/// returned text never equals an exception or a <see langword="null"/> that is shown the same way.</para>
/// <para>An exception is shown as <c>EX:type:message</c> (full type name), followed by
/// <c> ---&gt; EX:type:message</c> for each inner exception; the inner exceptions are part of the comparison.
/// Stack traces are not.</para>
/// </remarks>
internal static class ReadComparer
{
    private enum Kind
    {
        Text,
        Null,
        Exception,
    }

    private readonly record struct Outcome(Kind Kind, string Text);

    /// <summary>
    /// <see langword="null"/> when equal; otherwise a message that contains the input, both results, and the
    /// first difference.
    /// </summary>
    public static string? Compare(string input, Func<string, string> readA, Func<string, string> readB)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(readA);
        ArgumentNullException.ThrowIfNull(readB);

        Outcome a = Run(input, readA);
        Outcome b = Run(input, readB);
        if (a.Kind == b.Kind && string.Equals(a.Text, b.Text, StringComparison.Ordinal))
            return null;

        return Describe(input, a, b);
    }

    private static Outcome Run(string input, Func<string, string> read)
    {
        try
        {
            string? text = read(input);
            return text is null ? new Outcome(Kind.Null, "<null result>") : new Outcome(Kind.Text, text);
        }
        catch (Exception ex)
        {
            var text = new StringBuilder();
            for (Exception? e = ex; e is not null; e = e.InnerException)
            {
                if (e != ex)
                    text.Append(" ---> ");
                text.Append("EX:").Append(e.GetType().FullName).Append(':').Append(e.Message);
            }

            return new Outcome(Kind.Exception, text.ToString());
        }
    }

    private static string Describe(string input, Outcome a, Outcome b)
    {
        var message = new StringBuilder();
        message.Append("The two reads differ.\n");
        message.Append("input (C# literal, ").Append(input.Length.ToString(CultureInfo.InvariantCulture)).Append(" chars): ")
            .Append(Literal(input)).Append('\n');

        if (a.Kind != b.Kind)
        {
            message.Append("first difference: the outcome, A ").Append(Name(a.Kind)).Append(", B ").Append(Name(b.Kind)).Append('\n');
        }

        int index = FirstDifference(a.Text, b.Text);
        if (index >= 0)
        {
            (int line, int column) = Position(a.Text, index);
            message.Append("first difference: char ").Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(", line ").Append(line.ToString(CultureInfo.InvariantCulture))
                .Append(", column ").Append(column.ToString(CultureInfo.InvariantCulture)).Append('\n');
            message.Append("  A: ").Append(Literal(LineAt(a.Text, line))).Append('\n');
            message.Append("  B: ").Append(Literal(LineAt(b.Text, line))).Append('\n');
        }

        message.Append("--- A (").Append(Name(a.Kind)).Append(") ---\n").Append(a.Text).Append('\n');
        message.Append("--- B (").Append(Name(b.Kind)).Append(") ---\n").Append(b.Text).Append('\n');
        return message.ToString();
    }

    private static string Name(Kind kind) => kind switch
    {
        Kind.Text => "returned text",
        Kind.Null => "returned null",
        _ => "threw",
    };

    /// <summary>The index of the first char that differs, the shorter length when one text starts the other, or -1.</summary>
    private static int FirstDifference(string a, string b)
    {
        int common = a.AsSpan().CommonPrefixLength(b);
        return common == a.Length && common == b.Length ? -1 : common;
    }

    /// <summary>The 1-based line and column of <paramref name="index"/>; lines end with <c>\n</c>.</summary>
    private static (int Line, int Column) Position(string text, int index)
    {
        int line = 1, lineStart = 0;
        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, index - lineStart + 1);
    }

    /// <summary>Line <paramref name="line"/> (1-based) of <paramref name="text"/>, or an empty text past the end.</summary>
    private static string LineAt(string text, int line)
    {
        string[] lines = text.Split('\n');
        return line <= lines.Length ? lines[line - 1] : string.Empty;
    }

    /// <summary>A C# string literal: every char outside printable ASCII is an escape, so the text can be pasted back.</summary>
    private static string Literal(string text)
    {
        var literal = new StringBuilder(text.Length + 2);
        literal.Append('"');
        foreach (char c in text)
        {
            switch (c)
            {
                case '"': literal.Append("\\\""); break;
                case '\\': literal.Append("\\\\"); break;
                case '\r': literal.Append("\\r"); break;
                case '\n': literal.Append("\\n"); break;
                case '\t': literal.Append("\\t"); break;
                case < ' ' or > '~':
                    literal.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    break;
                default: literal.Append(c); break;
            }
        }

        return literal.Append('"').ToString();
    }
}
