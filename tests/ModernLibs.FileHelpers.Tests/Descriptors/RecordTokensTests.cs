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

using ModernLibs.FileHelpers.Parsing;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Descriptors;

/// <summary>
/// The member semantics of <see cref="RecordTokens"/> (design 4.3, freeze F-5 and F-6), with the field ends written
/// into a <see cref="TokenBuffer"/> by hand. <see cref="RecordTokens"/> is a ref struct, so each test reads it in a
/// synchronous helper and asserts the plain results.
/// </summary>
[Category("Descriptors")]
public sealed class RecordTokensTests
{
    private const int Q = TokenBuffer.QuotedBit;
    private const int D = TokenBuffer.DoubledBit;

    private static RecordTokens Tokens(TokenBuffer buffer, ReadOnlySpan<char> record, int[] ends, int delimiterLength = 1, char quote = '\0')
    {
        ends.CopyTo(buffer.Ends, 0);
        return buffer.Tokens(record, ends.Length, delimiterLength, quote);
    }

    [Test]
    public async Task Default_Has_Count_Zero_And_Never_Throws()
    {
        static (int Count, bool Quoted0, bool QuotedMinus1, bool Empty0, bool EmptyMinus1, string String0, string StringMinus1) Probe()
        {
            RecordTokens tokens = default;
            return (tokens.Count, tokens.IsQuoted(0), tokens.IsQuoted(-1), tokens.GetValue(0).IsEmpty, tokens.GetValue(-1).IsEmpty,
                tokens.GetString(0), tokens.GetString(-1));
        }

        var p = Probe();
        await Assert.That(p.Count).IsEqualTo(0);
        await Assert.That(p.Quoted0).IsFalse();
        await Assert.That(p.QuotedMinus1).IsFalse();
        await Assert.That(p.Empty0).IsTrue();
        await Assert.That(p.EmptyMinus1).IsTrue();
        await Assert.That(ReferenceEquals(p.String0, string.Empty)).IsTrue();
        await Assert.That(ReferenceEquals(p.StringMinus1, string.Empty)).IsTrue();
    }

    [Test]
    public async Task Empty_Fields_Keep_Their_Position()
    {
        const string record = "ab,,c,";
        static (int Count, int[] Columns, int[] Legacy, string[] Values, bool String1IsEmpty, string String4, bool Quoted, bool Value4Empty) Probe()
        {
            var tokens = Tokens(new TokenBuffer(4), record, [2, 3, 5, 6]);
            ReadOnlySpan<char> text = record;
            int[] columns = new int[6];
            for (int i = -1; i <= 4; i++)
                columns[i + 1] = RecordParsing.Column(text, tokens.GetValue(i));
            int[] legacy =
            [
                LegacyEmittedCopies.Column(text, ReadOnlySpan<char>.Empty),
                LegacyEmittedCopies.Column(text, text.Slice(0, 2)),
                LegacyEmittedCopies.Column(text, text.Slice(3, 0)),
                LegacyEmittedCopies.Column(text, text.Slice(4, 1)),
                LegacyEmittedCopies.Column(text, text.Slice(6, 0)),
                LegacyEmittedCopies.Column(text, ReadOnlySpan<char>.Empty),
            ];
            string[] values = [tokens.GetValue(0).ToString(), tokens.GetValue(1).ToString(), tokens.GetValue(2).ToString(), tokens.GetValue(3).ToString()];
            bool quoted = tokens.IsQuoted(0) || tokens.IsQuoted(1) || tokens.IsQuoted(2) || tokens.IsQuoted(3);
            return (tokens.Count, columns, legacy, values, ReferenceEquals(tokens.GetString(1), string.Empty), tokens.GetString(4), quoted, tokens.GetValue(4).IsEmpty);
        }

        var p = Probe();
        await Assert.That(p.Count).IsEqualTo(4);
        await Assert.That(p.Columns).IsEquivalentTo(new[] { -1, 1, 4, 5, 7, -1 });
        await Assert.That(p.Columns).IsEquivalentTo(p.Legacy);
        await Assert.That(p.Values).IsEquivalentTo(new[] { "ab", string.Empty, "c", string.Empty });
        await Assert.That(p.String1IsEmpty).IsTrue();
        await Assert.That(ReferenceEquals(p.String4, string.Empty)).IsTrue();
        await Assert.That(p.Quoted).IsFalse();
        await Assert.That(p.Value4Empty).IsTrue();
    }

    [Test]
    public async Task Delimiter_Length_Sets_The_Field_Start()
    {
        const string record = "a::bc::";
        static (string V0, string V1, string V2, int C1, int C2) Probe()
        {
            var tokens = Tokens(new TokenBuffer(3), record, [1, 5, 7], delimiterLength: 2);
            return (tokens.GetValue(0).ToString(), tokens.GetValue(1).ToString(), tokens.GetValue(2).ToString(),
                RecordParsing.Column(record, tokens.GetValue(1)), RecordParsing.Column(record, tokens.GetValue(2)));
        }

        var p = Probe();
        await Assert.That(p.V0).IsEqualTo("a");
        await Assert.That(p.V1).IsEqualTo("bc");
        await Assert.That(p.V2).IsEqualTo(string.Empty);
        await Assert.That(p.C1).IsEqualTo(4);
        await Assert.That(p.C2).IsEqualTo(8);
    }

    [Test]
    public async Task Quoted_Values()
    {
        const string record = "\"x\",\"\"";
        static (string V0, int C0, bool V1Empty, int C1, bool Q0, bool Q1, bool Q9, bool QMinus1, string S0, bool S1Empty) Probe()
        {
            var tokens = Tokens(new TokenBuffer(2), record, [3 | Q, 6 | Q], quote: '"');
            return (tokens.GetValue(0).ToString(), RecordParsing.Column(record, tokens.GetValue(0)),
                tokens.GetValue(1).IsEmpty, RecordParsing.Column(record, tokens.GetValue(1)),
                tokens.IsQuoted(0), tokens.IsQuoted(1), tokens.IsQuoted(9), tokens.IsQuoted(-1),
                tokens.GetString(0), ReferenceEquals(tokens.GetString(1), string.Empty));
        }

        var p = Probe();
        await Assert.That(p.V0).IsEqualTo("x");
        await Assert.That(p.C0).IsEqualTo(2);
        await Assert.That(p.V1Empty).IsTrue();
        await Assert.That(p.C1).IsEqualTo(6);
        await Assert.That(p.Q0).IsTrue();
        await Assert.That(p.Q1).IsTrue();
        await Assert.That(p.Q9).IsFalse();
        await Assert.That(p.QMinus1).IsFalse();
        await Assert.That(p.S0).IsEqualTo("x");
        await Assert.That(p.S1Empty).IsTrue();
    }

    /// <summary>
    /// <c>"a""b","c""d"</c>: the two unescaped values must both stay valid (F-6). <paramref name="scratch"/> is the
    /// scratch length the tokenizer would have ensured: the record length (both values in the scratch), a length
    /// that holds only the first value, and none (both are new arrays).
    /// </summary>
    [Test]
    [Arguments(13)]
    [Arguments(4)]
    [Arguments(0)]
    public async Task Two_Doubled_Quote_Fields_Keep_Both_Values(int scratch)
    {
        const string record = "\"a\"\"b\",\"c\"\"d\"";
        (string V0, string V1, int C0, int C1, string S0, string S1, bool Q0, bool Q1) Probe()
        {
            var buffer = new TokenBuffer(2);
            if (scratch > 0)
                buffer.EnsureScratch(scratch);
            var tokens = Tokens(buffer, record, [6 | Q | D, 13 | Q | D], quote: '"');
            ReadOnlySpan<char> v0 = tokens.GetValue(0);
            ReadOnlySpan<char> v1 = tokens.GetValue(1);
            return (v0.ToString(), v1.ToString(), RecordParsing.Column(record, v0), RecordParsing.Column(record, v1),
                tokens.GetString(0), tokens.GetString(1), tokens.IsQuoted(0), tokens.IsQuoted(1));
        }

        var p = Probe();
        await Assert.That(record.Length).IsEqualTo(13);
        await Assert.That(p.V0).IsEqualTo("a\"b");
        await Assert.That(p.V1).IsEqualTo("c\"d");
        await Assert.That(p.C0).IsEqualTo(-1);
        await Assert.That(p.C1).IsEqualTo(-1);
        await Assert.That(p.S0).IsEqualTo("a\"b");
        await Assert.That(p.S1).IsEqualTo("c\"d");
        await Assert.That(p.Q0).IsTrue();
        await Assert.That(p.Q1).IsTrue();
    }

    /// <summary>Calling <c>GetValue</c> again for a doubled-quote field appends again; earlier spans keep their text.</summary>
    [Test]
    public async Task Repeated_GetValue_Keeps_Earlier_Values()
    {
        const string record = "\"a\"\"b\",x,\"\"\"\"";
        static (string First, string Second, string Third, string Plain, string Quote) Probe()
        {
            var buffer = new TokenBuffer(3);
            buffer.EnsureScratch(record.Length);
            var tokens = Tokens(buffer, record, [6 | Q | D, 8, 13 | Q | D], quote: '"');
            ReadOnlySpan<char> first = tokens.GetValue(0);
            ReadOnlySpan<char> second = tokens.GetValue(0);
            ReadOnlySpan<char> quote = tokens.GetValue(2);
            ReadOnlySpan<char> third = tokens.GetValue(0);
            return (first.ToString(), second.ToString(), third.ToString(), tokens.GetValue(1).ToString(), quote.ToString());
        }

        var p = Probe();
        await Assert.That(p.First).IsEqualTo("a\"b");
        await Assert.That(p.Second).IsEqualTo("a\"b");
        await Assert.That(p.Third).IsEqualTo("a\"b");
        await Assert.That(p.Plain).IsEqualTo("x");
        await Assert.That(p.Quote).IsEqualTo("\"");
    }

    /// <summary>
    /// A new record resets the write position of the scratch: its first unescaped value is written where the first
    /// value of the record before was (that span is no longer valid, design 4.3 "Lifetime").
    /// </summary>
    [Test]
    public async Task Each_Record_Starts_At_The_Scratch_Start()
    {
        static (string A, string B, bool Reused) Probe()
        {
            var buffer = new TokenBuffer(1);
            buffer.EnsureScratch(6);
            ReadOnlySpan<char> first = Tokens(buffer, "\"a\"\"b\"", [6 | Q | D], quote: '"').GetValue(0);
            string a = first.ToString();
            var second = Tokens(buffer, "\"c\"\"d\"", [6 | Q | D], quote: '"');
            ReadOnlySpan<char> v = second.GetValue(0);
            ReadOnlySpan<char> w = second.GetValue(0);
            return (a, v.ToString() + w.ToString(), first.Overlaps(v, out int offset) && offset == 0);
        }

        var p = Probe();
        await Assert.That(p.A).IsEqualTo("a\"b");
        await Assert.That(p.B).IsEqualTo("c\"dc\"d");
        await Assert.That(p.Reused).IsTrue();
    }

    /// <summary>
    /// Ruling P18 (T15 review M2): a quoted entry shorter than its two quotes has no value between them. The runtime
    /// never makes one (the tokenizer refuses an unclosed quote), so this is the "no member throws" contract only:
    /// an empty value, <see cref="string.Empty"/>, with or without the doubled flag.
    /// </summary>
    [Test]
    [Arguments("\"", 1)]
    [Arguments("", 0)]
    public async Task Quoted_Entry_Shorter_Than_Two_Chars_Is_Empty(string record, int end)
    {
        static (bool Quoted, bool ValueEmpty, string String, bool DoubledValueEmpty, string DoubledString) Probe(string record, int end)
        {
            var tokens = Tokens(new TokenBuffer(1), record, [end | Q], quote: '"');
            var doubled = Tokens(new TokenBuffer(1), record, [end | Q | D], quote: '"');
            return (tokens.IsQuoted(0), tokens.GetValue(0).IsEmpty, tokens.GetString(0), doubled.GetValue(0).IsEmpty, doubled.GetString(0));
        }

        var p = Probe(record, end);
        await Assert.That(p.Quoted).IsTrue();
        await Assert.That(p.ValueEmpty).IsTrue();
        await Assert.That(ReferenceEquals(p.String, string.Empty)).IsTrue();
        await Assert.That(p.DoubledValueEmpty).IsTrue();
        await Assert.That(ReferenceEquals(p.DoubledString, string.Empty)).IsTrue();
    }

    [Test]
    public async Task Buffer_Holds_One_End_More_Than_The_Field_Count()
    {
        await Assert.That(new TokenBuffer(5).Ends.Length).IsEqualTo(6);
    }
}
