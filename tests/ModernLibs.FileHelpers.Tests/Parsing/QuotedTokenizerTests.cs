// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests.
#pragma warning disable CA1849
// CA1859: Interface-typed variables are intentional — tests verify interface-based usage.
#pragma warning disable CA1859

using System.Globalization;
using System.Text;
using ModernLibs.FileHelpers.Parsing;
using ModernLibs.FileHelpers.Tests.Fuzz.DiffFuzz;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// <see cref="RecordTokenizer.TokenizeQuoted"/> (T20, R6): the walk of <see cref="QuoteBalanceScanner"/> that also
/// writes the end entries. <c>(lastField, endsInside)</c> always equal <see cref="QuoteBalanceScanner.EndsInsideQuotes(ReadOnlySpan{char}, bool, ReadOnlySpan{char}, ReadOnlySpan{char}, ReadOnlySpan{string?}, ref int, ReadOnlySpan{string?})"/>;
/// the tokens equal a char-by-char reference of the 3.x rules, or "no tokens" (-1) outside the success shape. Both
/// loops run: <c>vectors: true</c> (whatever this machine accelerates) and <c>vectors: false</c> (<c>IndexOfAny</c>).
/// </summary>
[Category("Parsing")]
public sealed class QuotedTokenizerTests
{
    private const int Quoted = TokenBuffer.QuotedBit;
    private const int Doubled = TokenBuffer.DoubledBit;
    private static readonly bool[] BothLoops = [true, false];

    /// <summary>
    /// The 3.x rules char by char: a field whose quote char is its first char is a quoted value up to the next single
    /// quote char (a doubled one is an escaped quote); any other field runs to the next delimiter. Returns the field
    /// count (at most <paramref name="ends"/>.Length) with the entries written, or -1 when a quoted value is unclosed
    /// or followed by text before its delimiter or the record end.
    /// </summary>
    private static int Reference(string line, string quotes, int[] ends)
    {
        int field = 0;
        int position = 0;
        bool refused = false;
        while (true)
        {
            char q = field < quotes.Length ? quotes[field] : '\0';
            int entry = 0;
            int end;
            if (q != '\0' && position < line.Length && line[position] == q)
            {
                entry = Quoted;
                int i = position + 1;
                while (true)
                {
                    if (i >= line.Length)
                        return -1;
                    if (line[i] == q)
                    {
                        if (i + 1 < line.Length && line[i + 1] == q)
                        {
                            entry |= Doubled;
                            i += 2;
                            continue;
                        }

                        break;
                    }

                    i++;
                }

                int d = line.AsSpan(i + 1).IndexOf(',');
                end = d < 0 ? line.Length : i + 1 + d;
                refused |= end != i + 1;
            }
            else
            {
                int d = line.AsSpan(position).IndexOf(',');
                end = d < 0 ? line.Length : position + d;
            }

            if (field < ends.Length)
                ends[field] = end | entry;
            if (end == line.Length)
                return refused ? -1 : Math.Min(field + 1, ends.Length);
            position = end + 1;
            field++;
        }
    }

    /// <summary>
    /// Tokenizes <paramref name="line"/> with both loops into a buffer of <paramref name="fieldCount"/> fields;
    /// <see langword="null"/> when each agrees with the scan and the reference, otherwise the first difference.
    /// </summary>
    private static string? Compare(string line, string quotes, int fieldCount)
    {
        int scanField = 0;
        bool scanInside = QuoteBalanceScanner.EndsInsideQuotes(line, startsInsideQuotes: false, ",", quotes, [], ref scanField);
        var expectedEnds = new int[fieldCount + 1];
        int expected = Reference(line, quotes, expectedEnds);

        foreach (bool vectors in BothLoops)
        {
            var buffer = new TokenBuffer(fieldCount);
            int count = RecordTokenizer.TokenizeQuoted(line, ',', '"', quotes, buffer, vectors, out int lastField, out bool endsInside);
            if (lastField != scanField || endsInside != scanInside)
                return Describe(line, quotes, vectors, $"(lastField, endsInside) = ({lastField}, {endsInside}), scan ({scanField}, {scanInside})");
            if (count != expected)
                return Describe(line, quotes, vectors, $"count {count}, reference {expected}");
            for (int k = 0; k < count; k++)
            {
                if (buffer.Ends[k] != expectedEnds[k])
                    return Describe(line, quotes, vectors, $"entry {k} is {buffer.Ends[k]:X8}, reference {expectedEnds[k]:X8}");
            }
        }

        return null;
    }

    private static string Describe(string line, string quotes, bool vectors, string what) =>
        string.Create(CultureInfo.InvariantCulture, $"vectors: {vectors}, quotes '{Escape(quotes)}', line ({line.Length} chars) '{Escape(line)}': {what}");

    private static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c is < ' ' or > '~')
                escaped.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:X4}");
            else
                escaped.Append(c);
        }

        return escaped.ToString();
    }

    /// <summary>The quote chars of <paramref name="fieldCount"/> fields: every field, none, or every other field.</summary>
    private static string Quotes(int pattern, int fieldCount) =>
        string.Create(fieldCount, pattern, static (span, p) =>
        {
            for (int i = 0; i < span.Length; i++)
                span[i] = p switch { 0 => '"', 1 => '\0', _ => i % 2 == 0 ? '"' : '\0' };
        });

    [Test]
    public async Task Old_Scan_And_Tokenizer_Agree_On_Fuzzed_Input()
    {
        // LineGenerator puts CRLF, LF and CR inside 3 % of the quoted values, so multi-line records are part of the input.
        var generator = new LineGenerator(4920);
        int checkedLines = 0;
        int withTokens = 0;
        int withLineBreak = 0;
        for (int i = 0; i < 200_000; i++)
        {
            int fieldCount = 1 + generator.Next(6);
            string line = generator.NextRecord(fieldCount);
            withLineBreak += line.AsSpan().ContainsAny('\r', '\n') ? 1 : 0;
            for (int pattern = 0; pattern < 3; pattern++)
            {
                string quotes = Quotes(pattern, fieldCount);
                string? difference = Compare(line, quotes, fieldCount);
                if (difference is not null)
                    Fail.Test(difference);
                withTokens += Reference(line, quotes, new int[fieldCount + 1]) > 0 ? 1 : 0;
                checkedLines++;
            }
        }

        await Assert.That(checkedLines).IsEqualTo(600_000);
        await Assert.That(withTokens).IsGreaterThan(300_000);
        await Assert.That(withLineBreak).IsGreaterThan(1_000);
    }

    [Test]
    [Arguments("\"a\"x,b")]
    [Arguments("\"a")]
    [Arguments("\"a\" ,b")]
    [Arguments("a,\"b\"z")]
    [Arguments("\"a\"x\",b")]
    [Arguments("\"a\"\"")]
    [Arguments("\"\"\"")]
    [Arguments("\"")]
    [Arguments("a,\"")]
    [Arguments("\",a")]
    [Arguments("a,\"b\" ")]
    public async Task Outside_The_Success_Shape_Returns_No_Tokens(string line)
    {
        foreach (bool vectors in BothLoops)
        {
            int count = RecordTokenizer.TokenizeQuoted(line, ',', '"', "\"\"", new TokenBuffer(2), vectors, out _, out _);
            await Assert.That(count).IsEqualTo(-1).Because($"'{line}', vectors: {vectors}");
        }

        await Assert.That(Compare(line, "\"\"", 2)).IsNull();
    }

    /// <summary>
    /// A buffer of a 2-field record type holds 3 entries (<see cref="TokenBuffer"/> keeps one more): three or more
    /// fields return 3, the capacity, never -1; <c>lastField</c> still counts every field.
    /// </summary>
    [Test]
    [Arguments("a,b,c", 2)]
    [Arguments("a,\"b\",c,\"d\"", 3)]
    public async Task One_Extra_Field_Returns_Capacity(string line, int lastFieldExpected)
    {
        foreach (bool vectors in BothLoops)
        {
            var buffer = new TokenBuffer(2);
            int count = RecordTokenizer.TokenizeQuoted(line, ',', '"', "\"\"\"\"", buffer, vectors, out int lastField, out bool endsInside);
            await Assert.That(count).IsEqualTo(3);
            await Assert.That(buffer.Ends.Length).IsEqualTo(3);
            await Assert.That(lastField).IsEqualTo(lastFieldExpected);
            await Assert.That(endsInside).IsFalse();
        }
    }

    [Test]
    public async Task Values_Are_Slices_Between_Quotes_Or_Unescaped_Copies()
    {
        const string record = "\"a,b\",\"c\"\"d\",e";
        static (int Count, string V0, int C0, string V1, int C1, bool Q0, bool Q1, bool Q2, string V2) Probe(bool vectors)
        {
            var buffer = new TokenBuffer(3);
            int count = RecordTokenizer.TokenizeQuoted(record, ',', '"', "\"\"\"", buffer, vectors, out _, out _);
            var tokens = buffer.Tokens(record, count, delimiterLength: 1, '"');
            var v0 = tokens.GetValue(0);
            var v1 = tokens.GetValue(1);
            return (count, v0.ToString(), RecordParsing.Column(record, v0), v1.ToString(), RecordParsing.Column(record, v1),
                tokens.IsQuoted(0), tokens.IsQuoted(1), tokens.IsQuoted(2), tokens.GetString(2));
        }

        foreach (bool vectors in BothLoops)
        {
            var p = Probe(vectors);
            await Assert.That(p.Count).IsEqualTo(3);
            await Assert.That(p.V0).IsEqualTo("a,b");
            await Assert.That(p.C0).IsEqualTo(2);
            await Assert.That(p.V1).IsEqualTo("c\"d");
            await Assert.That(p.C1).IsEqualTo(-1);
            await Assert.That(p.Q0).IsTrue();
            await Assert.That(p.Q1).IsTrue();
            await Assert.That(p.Q2).IsFalse();
            await Assert.That(p.V2).IsEqualTo("e");
        }
    }

    [Test]
    public async Task Quote_Inside_An_Unquoted_Value_Is_Literal()
    {
        const string record = "5\" ruler,x";
        static (int Count, string V0, bool Q0, string V1) Probe(bool vectors)
        {
            var buffer = new TokenBuffer(2);
            int count = RecordTokenizer.TokenizeQuoted(record, ',', '"', "\"\"", buffer, vectors, out _, out _);
            var tokens = buffer.Tokens(record, count, delimiterLength: 1, '"');
            return (count, tokens.GetValue(0).ToString(), tokens.IsQuoted(0), tokens.GetValue(1).ToString());
        }

        foreach (bool vectors in BothLoops)
        {
            await Assert.That(Probe(vectors)).IsEqualTo((2, "5\" ruler", false, "x"));
        }

        await Assert.That(Compare(record, "\"\"", 2)).IsNull();
    }

    /// <summary>
    /// Review Focus 6: a quote char that opens, closes, doubles, or a delimiter inside quotes at the last and the first
    /// index of a <c>Vector128</c> or <c>Vector256</c> block (15, 16, 31, 32), and chars whose low byte is the quote
    /// (U+0122) or whose high and low bytes are (U+2222): only a full 16-bit compare tells them from <c>"</c>.
    /// </summary>
    [Test]
    public async Task Block_Edge_And_Low_Byte_Inputs()
    {
        const string low = "Ģ∢";
        var lines = new List<string>();
        foreach (int p in new[] { 15, 16, 31, 32 })
        {
            string prefix = new string('a', p - 1) + ",";
            string filler = new('b', p - 1);
            lines.Add(prefix + "\"x" + low + ",y\"," + low + "z");                // opening quote at p
            lines.Add("\"" + filler + "\",z" + low);                              // closing quote at p
            lines.Add("\"" + filler + "\"\"c\",z");                               // doubled quote at p, p + 1
            lines.Add("\"" + filler[..^1] + "\"\"c\"," + low);                    // doubled quote at p - 1, p
            lines.Add("\"" + filler + ",c\",z");                                  // delimiter inside quotes at p
            lines.Add(prefix + low + "\"" + low + "," + low);                    // a quote not at the field start
            lines.Add(prefix + "\"" + low + "\"" + low + ",z");                  // text after the closing quote
            lines.Add(prefix + "\"" + low + new string('c', 40) + low);          // unclosed over several blocks
            lines.Add(string.Concat(Enumerable.Repeat("\"" + low + "\"\"" + low + "\",", 6)) + low);
        }

        foreach (string line in lines)
        {
            foreach (string quotes in new[] { "\"\"\"\"\"\"\"\"", "\0\"\0\"\0\"\0\"", "\"\0\"\0\"\0\"\0" })
                await Assert.That(Compare(line, quotes, 8)).IsNull();
        }

        // The quoted shapes above are tokenized (not refused), so the comparison checked real entries.
        int count = RecordTokenizer.TokenizeQuoted(lines[0], ',', '"', "\"\"\"\"", new TokenBuffer(3), vectors: true, out _, out _);
        await Assert.That(count).IsEqualTo(3);
    }

    /// <summary>A record without the quote char takes the plain tokenizer; <c>lastField</c> still counts past the capacity.</summary>
    [Test]
    public async Task Record_Without_A_Quote_Char_Matches_The_Scan()
    {
        foreach (string line in new[] { "", "a", "a,b", "a,b,c,d,e,f,g", new string(',', 40), "xĢ,∢y" })
        {
            await Assert.That(Compare(line, "\"\"\"", 3)).IsNull();
        }
    }
}
