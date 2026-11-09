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
    [DisplayName("Well-formed records of up to ~400 chars (the 64-char block path) scan as the full scan does")]
    public async Task WellFormedRecords_MatchFullScan()
    {
        // Arrange: valid 3.x quoting (quoted values with "" and delimiters, plain values without quotes), so the
        // block path takes most lines; one record in ten gets a literal quote or ends inside a value.
        string quotes = "\0\"\"\"\0\"";
        var plan = QuoteBalanceScanner.Plan(",", quotes, [], default);
        var random = new Random(4711);
        int compared = 0, differences = 0;
        string? first = null;
        for (int n = 0; n < 30_000; n++)
        {
            var line = new StringBuilder();
            int fields = random.Next(1, 12);
            for (int f = 0; f < fields; f++)
            {
                if (f > 0)
                    line.Append(',');
                bool quoted = f < quotes.Length && quotes[f] != '\0' && random.Next(3) > 0;
                int size = random.Next(0, 60);
                if (quoted)
                {
                    line.Append('"');
                    for (int k = 0; k < size; k++)
                        line.Append(random.Next(8) switch { 0 => ",", 1 => "\"\"", 2 => " ", _ => "q" });
                    line.Append('"');
                }
                else
                {
                    line.Append('p', size);
                }
            }

            switch (random.Next(20))
            {
                case 0: line.Insert(random.Next(line.Length + 1), '"'); break;
                case 1: line.Append(",\"open, value"); break;
            }

            foreach (bool startsInside in new[] { false, true })
            {
                // Act
                string text = line.ToString();
                int full = startsInside ? 1 : 0, planned = full;
                bool fullInside = QuoteBalanceScanner.EndsInsideQuotes(text, startsInside, ",", quotes, [], ref full);
                bool plannedInside = QuoteBalanceScanner.EndsInsideQuotes(text, startsInside, ",", quotes, [], ref planned, default, plan);

                // Assert (collected)
                compared++;
                if (fullInside != plannedInside || full != planned)
                {
                    differences++;
                    first ??= $"'{text}' inside {startsInside}: full ({fullInside}, {full}), planned ({plannedInside}, {planned})";
                }
            }
        }

        await Assert.That(differences).IsEqualTo(0).Because(first ?? string.Empty);
        await Assert.That(compared).IsEqualTo(60_000);
    }

    [Test]
    [DisplayName("Delimiters, opening, closing and doubled quotes on both sides of the 64- and 128-char block edges")]
    public async Task BlockEdgeEvents_MatchFullScan()
    {
        // Arrange: a plain prefix moves each event across positions 50 to 140
        string quotes = "\0\"\"\"";
        var plan = QuoteBalanceScanner.Plan(",", quotes, [], default);
        string[] events = ["a,\"b\"", "a,\"\"\"\",c", "a,\"x\"\"y\",c", "a,\"x,y\",\"z\"", "a,\"open", "a,\"q\"\"", "\"x\"y,z"];
        foreach (string e in events)
        {
            for (int offset = 50; offset <= 140; offset++)
            {
                string line = new string('p', offset) + e;
                string continued = new string('v', offset) + "\"" + e;
                foreach (var (text, startsInside) in new[] { (line, false), (line, true), (continued, true), (continued, false) })
                {
                    // Act
                    int full = startsInside ? 1 : 0, planned = full;
                    bool fullInside = QuoteBalanceScanner.EndsInsideQuotes(text, startsInside, ",", quotes, [], ref full);
                    bool plannedInside = QuoteBalanceScanner.EndsInsideQuotes(text, startsInside, ",", quotes, [], ref planned, default, plan);

                    // Assert
                    await Assert.That((plannedInside, planned)).IsEqualTo((fullInside, full)).Because($"'{text}' inside {startsInside}");
                }
            }
        }
    }

    [Test]
    [DisplayName("The 64-char block scan accepts well-formed quoted lines and gives the full scan's result")]
    public async Task MaskedScan_AcceptsWellFormedLines()
    {
        // Arrange: valid 3.x quoting, so no quote is literal text and the block scan must not fall back
        string quotes = "\0\"\"\0";
        (string Line, bool StartsInside)[] cases =
        [
            ("1,\"a\",\"b\",x", false),
            ("1,\"a,b\"\"c\",\"\",x", false),
            (new string('p', 70) + ",\"" + new string('q', 80) + "\"\",\",\"z\",tail", false),
            ("1,\"" + new string('v', 200), false),
            ("1,\"\"\"\"\"\",\"x\"", false),
            // Continuations of the quoted field 1 opened on the line before
            ("b\"\"c,d\",\"z\",x", true),
            ("\",\"z\"", true),
            (new string('v', 63) + "\"\"" + new string('w', 70) + "\",x", true),
        ];

        foreach (var (line, startsInside) in cases)
        {
            // Act
            int full = startsInside ? 1 : 0, masked = full;
            bool fullInside = QuoteBalanceScanner.EndsInsideQuotes(line, startsInside, ",", quotes, [], ref full);
            bool accepted = QuoteBalanceScanner.TryScanMasked(line, startsInside, ',', '"', quotes, ref masked, out bool maskedInside);

            // Assert
            await Assert.That(accepted).IsTrue().Because($"'{line}' inside {startsInside}");
            await Assert.That((maskedInside, masked)).IsEqualTo((fullInside, full)).Because($"'{line}' inside {startsInside}");
        }
    }

    [Test]
    [DisplayName("A quote at the start of an unquoted field falls back on both sides of the 64- and 128-char block edges")]
    public async Task UnquotedFieldQuoteAtBlockEdge_FallsBack()
    {
        // Arrange: field 1 is not quoted, so its leading quote is literal text and the block scan must refuse
        string quotes = "\0\0\"";
        var plan = QuoteBalanceScanner.Plan(",", quotes, [], default);
        for (int offset = 50; offset <= 140; offset++)
        {
            string line = new string('p', offset) + ",\"a\",b";

            // Act
            int full = 0, planned = 0, masked = 0;
            bool fullInside = QuoteBalanceScanner.EndsInsideQuotes(line, false, ",", quotes, [], ref full);
            bool plannedInside = QuoteBalanceScanner.EndsInsideQuotes(line, false, ",", quotes, [], ref planned, default, plan);
            bool accepted = QuoteBalanceScanner.TryScanMasked(line, false, ',', '"', quotes, ref masked, out _);

            // Assert
            await Assert.That(accepted).IsFalse().Because($"offset {offset}");
            await Assert.That(masked).IsEqualTo(0).Because($"offset {offset}");
            await Assert.That((plannedInside, planned)).IsEqualTo((fullInside, full)).Because($"offset {offset}");

            // The same line started in field 1 puts the quote at the start of the quoted field 2: accepted
            int fullShifted = 1, maskedShifted = 1;
            bool fullShiftedInside = QuoteBalanceScanner.EndsInsideQuotes(line, false, ",", quotes, [], ref fullShifted);
            bool acceptedShifted = QuoteBalanceScanner.TryScanMasked(line, false, ',', '"', quotes, ref maskedShifted, out bool maskedShiftedInside);
            await Assert.That(acceptedShifted).IsTrue().Because($"offset {offset}, start field 1");
            await Assert.That((maskedShiftedInside, maskedShifted)).IsEqualTo((fullShiftedInside, fullShifted)).Because($"offset {offset}, start field 1");
        }
    }

    [Test]
    [DisplayName("Long quoted values with escaped quotes across block boundaries scan as the full scan does")]
    public async Task LongQuotedValues_MatchFullScan()
    {
        // Arrange: an escaped quote pair at every offset around the 8-, 16- and 64-char block edges
        for (int offset = 0; offset < 150; offset++)
        {
            string value = new string('v', offset) + "\"\"" + new string('w', 150 - offset);
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
