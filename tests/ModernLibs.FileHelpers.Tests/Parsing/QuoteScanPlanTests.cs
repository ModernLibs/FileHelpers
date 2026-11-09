// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using System.Text;
using ModernLibs.FileHelpers.Parsing;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// The quote scan with the shortcuts of <see cref="QuoteBalanceScanner.Plan"/> (a line without the quote
/// character, and the vectorised walk over delimiter and quote positions) gives the field and quote state
/// of the full scan for every line.
/// </summary>
[Category("Parsing")]
public sealed class QuoteScanPlanTests
{
    private static readonly (string Delimiter, string Quotes, string?[] TrimStart, char Quote, bool Vectorized)[] Rules =
    [
        (",", "\0\"\"\"\0", [], '"', true),
        (",", "\"\"\"\"\"\"", [], '"', true),
        ("|", "\0'", [], '\'', true),
        (";", "\"\0\0\0\"", [null, " \t", null, null, null], '"', true),
        (",", "\"\0\"", [" \t", null, null], '"', false),
        ("::", "\"\"\0\"", [], '"', false),
        (",", "\"\"", [], '"', true),
        // Non-ASCII delimiter and quote: the walk compares whole UTF-16 code units, as IndexOf does.
        ("§", "\0««", [], '«', true),
    ];

    private static readonly string[] Pieces =
    [
        "a", "bc", " ", ",", ",", "|", ";", ":", "::", "\"", "\"", "\"\"", "'", "''", "x\"y", "\t", "long text without separators",
        "é", "€", "\U0001F600", "§", "«", "««", "Ĭ", "Ⱒ",
    ];

    [Test]
    [DisplayName("The planned scan finds the same field and quote state as the full scan for random lines")]
    public async Task RandomLines_MatchFullScan()
    {
        // Arrange: lines up to ~120 chars, so quote pairs straddle the 8- and 16-char blocks and the tail
        var random = new Random(20261109);
        int compared = 0, differences = 0;
        string? first = null;

        for (int n = 0; n < 40_000; n++)
        {
            var builder = new StringBuilder();
            int pieces = random.Next(0, 24);
            for (int p = 0; p < pieces; p++)
                builder.Append(Pieces[random.Next(Pieces.Length)]);
            string line = builder.ToString();

            foreach (var (delimiter, quotes, trimStart, _, _) in Rules)
            {
                var plan = QuoteBalanceScanner.Plan(delimiter, quotes, trimStart, default);
                foreach (bool startsInside in new[] { false, true })
                {
                    for (int startField = 0; startField < 3; startField++)
                    {
                        // Act
                        int full = startField, planned = startField;
                        bool fullInside = QuoteBalanceScanner.EndsInsideQuotes(line, startsInside, delimiter, quotes, trimStart, ref full);
                        bool plannedInside = QuoteBalanceScanner.EndsInsideQuotes(line, startsInside, delimiter, quotes, trimStart, ref planned, default, plan);

                        // Assert (collected)
                        compared++;
                        if (fullInside != plannedInside || full != planned)
                        {
                            differences++;
                            first ??= $"'{line}' delimiter '{delimiter}' quotes '{quotes.Replace('\0', '0')}' inside {startsInside} field {startField}: full ({fullInside}, {full}), planned ({plannedInside}, {planned})";
                        }
                    }
                }
            }
        }

        await Assert.That(differences).IsEqualTo(0).Because(first ?? string.Empty);
        await Assert.That(compared).IsGreaterThan(1_000_000);
    }

    [Test]
    [DisplayName("Plan takes the shortcuts only for rules where they give the full scan's result")]
    public async Task Plan_ChoosesShortcuts()
    {
        foreach (var (delimiter, quotes, trimStart, quote, vectorized) in Rules)
        {
            var plan = QuoteBalanceScanner.Plan(delimiter, quotes, trimStart, default);
            await Assert.That(plan).IsEqualTo(new QuoteScanPlan(quote, vectorized)).Because($"'{delimiter}' '{quotes.Replace('\0', '0')}'");
        }

        // Two quote characters, a field delimiter of its own, no quoted field, the quote as delimiter
        await Assert.That(QuoteBalanceScanner.Plan(",", "\"'", [], default)).IsEqualTo(default(QuoteScanPlan));
        await Assert.That(QuoteBalanceScanner.Plan(",", "\"\"", [], [null, ";"])).IsEqualTo(default(QuoteScanPlan));
        await Assert.That(QuoteBalanceScanner.Plan(",", "\0\0", [], default)).IsEqualTo(default(QuoteScanPlan));
        await Assert.That(QuoteBalanceScanner.Plan("\"", "\"\"", [], default)).IsEqualTo(new QuoteScanPlan('"', Vectorized: false));
    }

    [Test]
    [DisplayName("Long quoted values with escaped quotes across block boundaries scan as the full scan does")]
    public async Task LongQuotedValues_MatchFullScan()
    {
        // Arrange: an escaped quote pair at every offset around the 8- and 16-char block edges
        for (int offset = 0; offset < 40; offset++)
        {
            string value = new string('v', offset) + "\"\"" + new string('w', 40 - offset);
            foreach (string line in new[] { $"1,\"{value}\",x", $"1,\"{value}", $"{value}\",2,\"z\"", $"1,\"{value}\"tail,\"" })
            {
                foreach (bool startsInside in new[] { false, true })
                {
                    // Act
                    int full = startsInside ? 1 : 0, planned = full;
                    var plan = QuoteBalanceScanner.Plan(",", "\0\"\"", [], default);
                    bool fullInside = QuoteBalanceScanner.EndsInsideQuotes(line, startsInside, ",", "\0\"\"", [], ref full);
                    bool plannedInside = QuoteBalanceScanner.EndsInsideQuotes(line, startsInside, ",", "\0\"\"", [], ref planned, default, plan);

                    // Assert
                    await Assert.That((plannedInside, planned)).IsEqualTo((fullInside, full)).Because($"'{line}' inside {startsInside}");
                }
            }
        }
    }
}
