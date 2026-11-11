using System.Globalization;
using System.Text;

namespace ModernLibs.FileHelpers.Tests.Fuzz.DiffFuzz;

/// <summary>
/// Makes delimited text for differential fuzzing. The same seed gives the same text on every runtime: the only
/// source of randomness is a <see cref="Random"/> made with <paramref name="seed"/> (a seeded
/// <see cref="Random"/> keeps its algorithm across .NET versions).
/// </summary>
/// <remarks>
/// <para>Each field is one of (weight in percent): plain text 50, empty 10, quoted 15, quoted with a doubled
/// quote 8, quoted with the delimiter 5, quoted with a line break 3, a quote not at the field start 3, blanks
/// around a quoted field 3, text after the closing quote 2, plain text with a non-ASCII char 1.</para>
/// <para>Each record is one of (weight in percent): the asked field count 85, one field short 5, one field extra
/// 5, an unclosed quote 3 (one field opens a quote and the fields after it have no quote char, so the record ends
/// inside the quote), an empty line 2.</para>
/// <para>Plain text never holds the delimiter, the quote char or a line break. It is a word, a number, a bool, a
/// date, a hex number, text with blanks around it, text with the separators of other record shapes
/// (<c>; | : # -</c>), or a long text, so that converters both accept and reject values.</para>
/// <para>This file is also compiled into <c>ModernLibs.FileHelpers.Fuzz.Tests</c> (a linked source file).</para>
/// </remarks>
internal sealed class LineGenerator(int seed, char delimiter = ',', char quote = '"')
{
    private const string WordChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const string SeparatorChars = ";|:#-";

    /// <summary>
    /// Non-ASCII text, by code point: e acute, "ete" with accents, u umlaut, sharp s, a CJK pair, an emoji (a
    /// surrogate pair), a no-break space, omega, e followed by a combining acute accent.
    /// </summary>
    private static readonly string[] NonAscii =
    [
        Text(0xE9), Text(0xE9, 't', 0xE9), Text(0xFC), Text(0xDF), Text(0x4E2D, 0x6587), Text(0x1F600), Text(0xA0),
        Text(0x3A9), Text('e', 0x301),
    ];

    /// <summary>Line breaks inside a quoted field: CRLF is the most common.</summary>
    private static readonly string[] LineBreaks = ["\r\n", "\r\n", "\n", "\r"];

    private static readonly string[] Bools = ["true", "false", "True", "False", "TRUE", "1", "0", "maybe"];

    private static readonly string[] Dates =
        ["2026-11-08", "2024-02-29", "2026-02-30", "11/08/2026", "13/01/2026", "2026-11-08T10:20:30", "2026-01-01 00:00"];

    private readonly Random _random = new(seed);

    /// <summary>One logical record, without a line end; it may contain line breaks inside quotes.</summary>
    public string NextRecord(int fieldCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fieldCount, 1);

        int roll = _random.Next(100);
        if (roll < 2)
            return string.Empty;                                  // empty line: 2

        int count = roll switch
        {
            < 87 => fieldCount,                                   // asked field count: 85
            < 92 => fieldCount - 1,                               // one field short: 5
            < 97 => fieldCount + 1,                               // one field extra: 5
            _ => fieldCount,                                      // unclosed quote: 3
        };
        int unclosedAt = roll >= 97 ? _random.Next(count) : -1;

        var record = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                record.Append(delimiter);

            if (unclosedAt < 0 || i < unclosedAt)
                AppendField(record);
            else if (i == unclosedAt)
                record.Append(quote).Append(Plain());             // a quote that is never closed
            else
                record.Append(_random.Next(4) == 0 ? string.Empty : Plain()); // no quote char after it
        }

        return record.ToString();
    }

    /// <summary><paramref name="records"/> records, each followed by CRLF.</summary>
    public string NextFile(int records, int fieldCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(records);

        var file = new StringBuilder();
        for (int i = 0; i < records; i++)
            file.Append(NextRecord(fieldCount)).Append("\r\n");
        return file.ToString();
    }

    private void AppendField(StringBuilder field)
    {
        int roll = _random.Next(100);
        switch (roll)
        {
            case < 50:                                            // plain text: 50
                field.Append(Plain());
                break;
            case < 60:                                            // empty: 10
                break;
            case < 75:                                            // quoted: 15 (a fifth of them empty)
                field.Append(quote).Append(_random.Next(5) == 0 ? string.Empty : Plain()).Append(quote);
                break;
            case < 83:                                            // quoted with a doubled quote: 8
                field.Append(quote).Append(Word(0)).Append(quote).Append(quote).Append(Word(0)).Append(quote);
                break;
            case < 88:                                            // quoted with the delimiter: 5
                field.Append(quote).Append(Word(0)).Append(delimiter).Append(Word(0)).Append(quote);
                break;
            case < 91:                                            // quoted with a line break: 3
                field.Append(quote).Append(Word(0)).Append(Pick(LineBreaks)).Append(Word(0)).Append(quote);
                break;
            case < 94:                                            // a quote not at the field start: 3
                field.Append(Word(1)).Append(quote).Append(Word(0));
                if (_random.Next(2) == 0)
                    field.Append(quote);
                break;
            case < 97:                                            // blanks around a quoted field: 3
                int sides = _random.Next(3);                      // 0: before, 1: after, 2: both
                if (sides != 1)
                    field.Append(Blanks());
                field.Append(quote).Append(Plain()).Append(quote);
                if (sides != 0)
                    field.Append(Blanks());
                break;
            case < 99:                                            // text after the closing quote: 2
                field.Append(quote).Append(Word(0)).Append(quote).Append(Word(1));
                break;
            default:                                              // non-ASCII: 1
                string text = Word(0);
                int at = _random.Next(text.Length + 1);
                field.Append(text, 0, at).Append(Pick(NonAscii)).Append(text, at, text.Length - at);
                break;
        }
    }

    /// <summary>Plain text: never the delimiter, the quote char or a line break.</summary>
    private string Plain()
    {
        int roll = _random.Next(100);
        string text = roll switch
        {
            < 35 => Word(1),
            < 50 => _random.Next(-999, 100000).ToString(CultureInfo.InvariantCulture),
            < 60 => Decimal(),
            < 68 => Pick(Bools),
            < 76 => Pick(Dates),
            < 81 => _random.Next(0, 4096).ToString(_random.Next(2) == 0 ? "X" : "x", CultureInfo.InvariantCulture),
            < 88 => Blanks() + Word(1) + Blanks(),
            < 95 => Word(0) + SeparatorChars[_random.Next(SeparatorChars.Length)] + Word(0),
            _ => Word(20, 80),
        };
        return Clean(text);
    }

    private string Decimal() => _random.Next(4) switch
    {
        0 => (_random.Next(-100000, 100000) / 100m).ToString("0.00", CultureInfo.InvariantCulture),
        1 => (_random.Next(-1000, 1000) / 1000m).ToString(CultureInfo.InvariantCulture),
        2 => _random.Next(1, 10).ToString(CultureInfo.InvariantCulture) + "e" + _random.Next(-3, 4).ToString(CultureInfo.InvariantCulture),
        _ => "." + _random.Next(0, 100).ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>A word of 1 to 8 letters or digits; <paramref name="min"/> 0 allows an empty word.</summary>
    private string Word(int min) => Word(min, 8);

    private string Word(int min, int max)
    {
        int length = _random.Next(min, max + 1);
        return string.Create(length, this, static (span, self) =>
        {
            for (int i = 0; i < span.Length; i++)
                span[i] = WordChars[self._random.Next(WordChars.Length)];
        });
    }

    private string Blanks() => _random.Next(4) switch
    {
        0 => " ",
        1 => "  ",
        2 => "\t",
        _ => " \t ",
    };

    private string Pick(string[] values) => values[_random.Next(values.Length)];

    /// <summary>Replaces the delimiter and the quote char, which the field kinds place on purpose only.</summary>
    private string Clean(string text) =>
        text.Contains(delimiter, StringComparison.Ordinal) || text.Contains(quote, StringComparison.Ordinal)
            ? text.Replace(delimiter, '_').Replace(quote, '_')
            : text;

    /// <summary>The text of <paramref name="codePoints"/>; a code point above U+FFFF becomes a surrogate pair.</summary>
    private static string Text(params int[] codePoints) => string.Concat(codePoints.Select(char.ConvertFromUtf32));
}
