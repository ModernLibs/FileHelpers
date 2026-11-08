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

    private static string Describe(Func<object?> read)
    {
        try
        {
            return read() switch
            {
                QpRecord r => $"record [{r.A}|{r.B}|{r.C}|{r.D}]",
                QpRecord[] { Length: 1 } rs => $"record [{rs[0].A}|{rs[0].B}|{rs[0].C}|{rs[0].D}]",
                QpRecord[] rs => $"{rs.Length} records",
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
    public async Task GeneratedLines_MatchValidationFromScratch()
    {
        // Arrange
        var engine = new FileHelperEngine<QpRecord>();
        var core = engine.Core;
        var random = new Random(4242);
        int compared = 0, differences = 0;
        string? firstDifference = null;

        for (int i = 0; i < 30_000; i++)
        {
            var line = new StringBuilder();
            int count = random.Next(1, 9);
            for (int t = 0; t < count; t++)
                line.Append(Tokens[random.Next(Tokens.Length)]);
            string text = line.ToString();

            // Only lines that complete a record on their own (no multiline record opened)
            if (core.OpensMultiline(text, 0, out _, out _) || text.Length == 0)
                continue;

            // Act
            string expected = Describe(() => core.Parse(text, recycle: false));
            // The engine adds the line number to the message ("Line: 1. " or "Line: 1 " before a column).
            string actual = Describe(() => engine.ReadString(text))
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
