// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using System.Text;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

[DelimitedRecord(",")]
[IgnoreEmptyLines]
public sealed class QpRecord
{
    [FieldOrder(1)] public string A { get; set; } = string.Empty;

    [FieldOrder(2)]
    [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.AllowForBoth)]
    public string B { get; set; } = string.Empty;

    [FieldOrder(3)]
    [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.NotAllow)]
    [FieldTrim(TrimMode.Both)]
    public string C { get; set; } = string.Empty;

    [FieldOrder(4)]
    [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.AllowForRead)]
    [FieldOptional]
    public string? D { get; set; }
}

[DelimitedRecord(",")]
[IgnoreEmptyLines]
public sealed class QpDelimited
{
    [FieldOrder(1)]
    [FieldDelimiter(";")]
    [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.AllowForBoth)]
    public string A { get; set; } = string.Empty;

    [FieldOrder(2)]
    [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.NotAllow)]
    public string B { get; set; } = string.Empty;

    [FieldOrder(3)]
    [FieldOptional]
    public string? C { get; set; }
}

[DelimitedRecord(",")]
[IgnoreCommentedLines('#')]
public sealed class QpCommented
{
    [FieldOrder(1)] public string A { get; set; } = string.Empty;

    [FieldOrder(2)]
    [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.AllowForBoth)]
    public string B { get; set; } = string.Empty;

    [FieldOrder(3)] public string C { get; set; } = string.Empty;
}

/// <summary>
/// The field count validation of a single-line record reuses the quote scan the line assembler made; the
/// outcome (record or exception) is that of validating the text from scratch.
/// </summary>
[Category("ReadCore")]
public sealed class QuotedPrescanTests
{
    private static readonly string[] Tokens =
    [
        "a", "b c", "\"q\"", "\"x,y\"", "\"\"", "\"a\"\"b\"", " \"t\"", "  \"u\" ", "\"open", "close\"", "\"", ",", ",", ",", " ", "x\"y",
    ];

    private static readonly string[] DelimitedTokens =
    [
        "a", "\"q\"", "\"x;y\"", "\"x,y\"", "\"\"", "\"a\"\"b\"", "\"open", "close\"", "\"", ",", ",", ";", ";", " ", "x\"y",
    ];

    private static string Describe<T>(Func<object?> read, Func<T, string> show)
    {
        try
        {
            return read() switch
            {
                T r => $"record [{show(r)}]",
                T[] { Length: 1 } rs => $"record [{show(rs[0])}]",
                T[] rs => $"{rs.Length} records",
                var other => other?.ToString() ?? "null",
            };
        }
        catch (Exception ex)
        {
            return $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    [Test]
    [DisplayName("Generated quoted lines: reading through the engine gives what validating and parsing the text from scratch gives")]
    public async Task GeneratedLines_MatchValidationFromScratch() =>
        await CompareGeneratedLines<QpRecord>(Tokens, r => $"{r.A}|{r.B}|{r.C}|{r.D}");

    [Test]
    [DisplayName("Generated quoted lines with a per-field delimiter: the engine gives what validating the text from scratch gives")]
    public async Task GeneratedLines_FieldDelimiter_MatchValidationFromScratch() =>
        await CompareGeneratedLines<QpDelimited>(DelimitedTokens, r => $"{r.A}|{r.B}|{r.C}");

    [Test]
    [DisplayName("A comment line that opens a quote is skipped and does not change how the next record is validated")]
    public async Task CommentLineOpeningQuote_IsSkipped()
    {
        // Arrange: each comment line ends inside a quote; the records after them are valid or have a wrong field count
        const string input = "#,\"open\r\na,\"b\",c\r\n#\"x,y\r\nd,e,f\r\n# \"\r\ng,\"h,i\",j\r\n";
        const string shortAfterQuote = "#,\"open\r\na,\"b\"\r\n";
        const string shortAfterPlain = "# plain\r\na,\"b\"\r\n";

        // Act
        static string Show(QpCommented r) => $"{r.A}|{r.B}|{r.C}";
        string fromString = string.Join(" ", new FileHelperEngine<QpCommented>().ReadString(input).Select(Show));
        string fromReader = string.Join(" ", new FileHelperEngine<QpCommented>().ReadStream(new StringReader(input)).Select(Show));
        string reference = Describe<QpCommented>(() => new FileHelperEngine<QpCommented>().ReadString(shortAfterPlain), Show);
        string afterQuote = Describe<QpCommented>(() => new FileHelperEngine<QpCommented>().ReadString(shortAfterQuote), Show);
        string afterQuoteReader = Describe<QpCommented>(() => new FileHelperEngine<QpCommented>().ReadStream(new StringReader(shortAfterQuote)), Show);

        // Assert: the short record after a quote-opening comment fails as after a plain comment
        await Assert.That(fromString).IsEqualTo("a|b|c d|e|f g|h,i|j");
        await Assert.That(fromReader).IsEqualTo(fromString);
        await Assert.That(reference).DoesNotStartWith("record");
        await Assert.That(afterQuote).IsEqualTo(reference);
        await Assert.That(afterQuoteReader).IsEqualTo(reference);
    }

    private static async Task CompareGeneratedLines<T>(string[] tokens, Func<T, string> show)
        where T : class
    {
        // Arrange
        var engine = new FileHelperEngine<T>();
        var core = engine.Core;
        var random = new Random(4242);
        int compared = 0, differences = 0;
        string? firstDifference = null;

        for (int i = 0; i < 30_000; i++)
        {
            var line = new StringBuilder();
            int count = random.Next(1, 9);
            for (int t = 0; t < count; t++)
                line.Append(tokens[random.Next(tokens.Length)]);
            string text = line.ToString();

            // Only lines that complete a record on their own (no multiline record opened)
            if (core.OpensMultiline(text, 0, out _, out _) || text.Length == 0)
                continue;

            // Act
            string expected = Describe(() => core.Parse(text, recycle: false), show);
            // The engine adds the line number to the message ("Line: 1. " or "Line: 1 " before a column).
            string actual = Describe(() => engine.ReadString(text), show)
                .Replace("Line: 1. ", string.Empty, StringComparison.Ordinal)
                .Replace("Line: 1 ", string.Empty, StringComparison.Ordinal);

            // Assert (collected): the same record, or the same exception type and message
            compared++;
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                differences++;
                firstDifference ??= $"'{text}': from scratch {expected}; engine {actual}";
            }
        }

        await Assert.That(differences).IsEqualTo(0).Because(firstDifference ?? string.Empty);
        await Assert.That(compared).IsGreaterThan(10_000);
    }

    [Test]
    [DisplayName("A BeforeReadRecord handler that replaces a balanced line with an unbalanced one still gets the unclosed-quote error")]
    public async Task ReplacedText_IsValidatedAgain()
    {
        // Arrange: the original line is balanced; the replacement leaves field C (no multiline) open
        var engine = new FileHelperEngine<QpRecord>();
        engine.BeforeReadRecord += (_, e) => e.RecordLine = "a,\"b\",\"open c";

        // Act
        var error = Assert.Throws<BadUsageException>(() => engine.ReadString("a,\"b\",c"));

        // Assert
        await Assert.That(error.Message).Contains("The field 'C' has an unclosed quoted string");
    }
}
