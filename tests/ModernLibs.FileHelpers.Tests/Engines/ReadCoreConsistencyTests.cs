// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0051: ReadEverywhere lists every read path in one place on purpose.
#pragma warning disable MA0051
// MA0048: Fixture type kept next to the test that uses it.
#pragma warning disable MA0048

using System.Text;
using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>Header, footer, filters and multiline quoted fields on one record type.</summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForRead)]
[IgnoreFirst(1)]
[IgnoreLast(2)]
[IgnoreEmptyLines]
[IgnoreCommentedLines('#')]
public sealed class RcEverything
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldQuoted] public string Notes { get; set; } = string.Empty;
    [FieldOrder(3)] public int Qty { get; set; }
}

/// <summary>Header, footer and filters on a two-line [FieldInNewLine] record.</summary>
[DelimitedRecord(",")]
[IgnoreFirst(1)]
[IgnoreLast(1)]
[IgnoreEmptyLines]
[IgnoreCommentedLines('#')]
public sealed class RcInNewLine
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldInNewLine] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public int Qty { get; set; }
}

/// <summary>Header and comments on a record whose first field is on a new line (blank first line, 3.x).</summary>
[DelimitedRecord(",")]
[IgnoreFirst(1)]
[IgnoreCommentedLines('#')]
public sealed class RcFirstInNewLine
{
    [FieldOrder(1)] [FieldInNewLine] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
}

/// <summary>Header, footer, comments and a multiline quoted value in a two-line [FieldInNewLine] record.</summary>
[DelimitedRecord(",")]
[IgnoreFirst(1)]
[IgnoreLast(1)]
[IgnoreCommentedLines('#')]
public sealed class RcInNewLineMultiline
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.AllowForBoth)] public string Notes { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldInNewLine] public string Name { get; set; } = string.Empty;
    [FieldOrder(4)] public int Qty { get; set; }
}

/// <summary>Header, footer and a multiline quoted field on a record with a [FieldDelimiter] field.</summary>
[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForRead)]
[IgnoreFirst(1)]
[IgnoreLast(1)]
public sealed class RcPerFieldDelimiter
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldDelimiter("|")] [FieldQuoted(QuoteMode.OptionalForRead)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public int Qty { get; set; }
}

/// <summary>
/// The same input and options read through every path yield the same records, HeaderText/FooterText,
/// errors with line numbers, TotalRecords and LineNumber.
/// </summary>
[Category("ReadCore")]
public sealed class ReadCoreConsistencyTests
{
    private const string MultilineInput =
        "HEADER,\"x\",0\r\n" +
        "1,\"plain\",10\r\n" +
        "\r\n" +
        "# comment\r\n" +
        "2,\"multi\r\n\r\n# not a comment\r\nline\",20\r\n" +
        "3,\"bad qty\",oops\r\n" +
        "4,\"extra\",40,EXTRA\r\n" +
        "5,\"last\",50\r\n" +
        "TOTAL,\"5\",0\r\n" +
        "END";

    private sealed record Outcome(string Records, string? Header, string? Footer, string Errors, int Total, int LineNumber)
    {
        public override string ToString() =>
            $"records=[{Records}] header={Header} footer={Footer?.Replace("\r\n", "\\r\\n", StringComparison.Ordinal)} errors=[{Errors}] total={Total} line={LineNumber}";
    }

    private static string Summarize<T>(IEnumerable<T> records, Func<T, string> format) => string.Join("|", records.Select(format));

    private static Outcome Of<T>(FileHelperEngine<T> engine, IEnumerable<T> records, Func<T, string> format) =>
        new(Summarize(records, format), engine.HeaderText, engine.FooterText,
            string.Join(",", engine.ErrorManager.Errors.Select(e => $"{e.LineNumber}:{e.ExceptionInfo?.GetType().Name}")),
            engine.TotalRecords, engine.LineNumber);

    private static Outcome Of<T>(FileHelperAsyncEngine<T> engine, IEnumerable<T> records, Func<T, string> format) =>
        new(Summarize(records, format), engine.HeaderText, engine.FooterText,
            string.Join(",", engine.ErrorManager.Errors.Select(e => $"{e.LineNumber}:{e.ExceptionInfo?.GetType().Name}")),
            engine.TotalRecords, engine.LineNumber);

    private static async Task<Dictionary<string, Outcome>> ReadEverywhere<T>(string input, Func<T, string> format)
    {
        string path = Path.Combine(Path.GetTempPath(), "fh-consistency-" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(path, input);
        var outcomes = new Dictionary<string, Outcome>(StringComparer.Ordinal);
        FileHelperEngine<T> New() => new() { ErrorMode = ErrorMode.SaveAndContinue };
        FileHelperAsyncEngine<T> NewAsync() => new() { ErrorMode = ErrorMode.SaveAndContinue };
        try
        {
            var e = New();
            outcomes["ReadString"] = Of(e, e.ReadString(input), format);
            e = New();
            outcomes["ReadSpan"] = Of(e, e.ReadSpan(input.AsSpan()), format);
            e = New();
            outcomes["ReadFile"] = Of(e, e.ReadFile(path), format);
            e = New();
            outcomes["ReadStream"] = Of(e, e.ReadStream(new StringReader(input)), format);
            e = New();
            outcomes["ReadSync(string)"] = Of(e, e.ReadSync(input).ToList(), format);
            e = New();
            outcomes["ReadSync(TextReader)"] = Of(e, e.ReadSync(new StringReader(input)).ToList(), format);

            e = New();
            var viaAsync = new List<T>();
            await foreach (var r in e.ReadAsync(path))
                viaAsync.Add(r);
            outcomes["ReadAsync"] = Of(e, viaAsync, format);

            e = New();
            var viaCallback = new List<T>();
            e.ReadString(input, viaCallback.Add);
            outcomes["ReadString(callback)"] = Of(e, viaCallback, format);

            e = New();
            var viaFileCallback = new List<T>();
            e.ReadFile(path, viaFileCallback.Add);
            outcomes["ReadFile(callback)"] = Of(e, viaFileCallback, format);

            e = New();
            var viaFileAsync = new List<T>();
            await e.ReadFileAsync(path, viaFileAsync.Add);
            outcomes["ReadFileAsync"] = Of(e, viaFileAsync, format);

            e = New();
            outcomes["ParallelReadString"] = Of(e, e.ParallelReadString(input, 64 * 1024), format);

            using (var a = NewAsync())
            {
                a.BeginReadString(input);
                var pulled = new List<T>();
                while (a.ReadNext(1) is [var one])
                    pulled.Add(one);
                outcomes["AsyncEngine.ReadNext"] = Of(a, pulled, format);
            }

            using (var a = NewAsync())
            {
                a.BeginReadFile(path);
                var iterated = new List<T>();
                foreach (var r in a)
                    iterated.Add(r);
                outcomes["AsyncEngine.foreach"] = Of(a, iterated, format);
            }

            using (var a = NewAsync())
            {
                var streamed = new List<T>();
                await foreach (var r in a.ReadAsync(path))
                    streamed.Add(r);
                outcomes["AsyncEngine.ReadAsync"] = Of(a, streamed, format);
            }
        }
        finally
        {
            File.Delete(path);
        }

        return outcomes;
    }

    private static async Task AssertAllEqual(Dictionary<string, Outcome> outcomes)
    {
        var reference = outcomes["ReadString"];
        var mismatches = outcomes
            .Where(kv => kv.Value != reference)
            .Select(kv => $"{kv.Key}: {kv.Value}")
            .ToList();
        await Assert.That(string.Join("\n", mismatches)).IsEqualTo(string.Empty);
    }

    [Test]
    [DisplayName("Consistency: header, footer, filters, multiline and errors are identical on every read path")]
    public async Task MultilineInput_SameOutcomeOnEveryPath()
    {
        // Arrange: input exercising every pipeline step
        static string Format(RcEverything r) => $"{r.Id}/{r.Notes.Replace("\r\n", "\\n", StringComparison.Ordinal)}/{r.Qty}";

        // Act: read through all paths
        var outcomes = await ReadEverywhere<RcEverything>(MultilineInput, Format);

        // Assert: identical outcomes, and the reference itself is right
        await AssertAllEqual(outcomes);
        var reference = outcomes["ReadString"];
        await Assert.That(reference.Records).IsEqualTo("1/plain/10|2/multi\\n\\n# not a comment\\nline/20|5/last/50");
        await Assert.That(reference.Header).IsEqualTo("HEADER,\"x\",0");
        await Assert.That(reference.Footer).IsEqualTo("TOTAL,\"5\",0\r\nEND");
        await Assert.That(reference.Errors).IsEqualTo("9:ConvertException,10:BadUsageException");
        await Assert.That(reference.Total).IsEqualTo(3);
        await Assert.That(reference.LineNumber).IsEqualTo(11);
    }

    [Test]
    [DisplayName("Consistency: [FieldInNewLine] records with comment/empty lines between them are identical on every read path")]
    public async Task FieldInNewLineInput_SameOutcomeOnEveryPath()
    {
        // Arrange: 3.x layout, a comment and an empty line between records, bad records (line 8 lacks the
        // delimiter before its line break)
        const string input =
            "HEADER\r\n" +
            "1,\r\nalpha,10\r\n" +
            "# comment\r\n" +
            "\r\n" +
            "2,\r\nbeta,oops\r\n" +
            "3\r\ngamma,30\r\n" +
            "4,x,\r\nbad,40\r\n" +
            "5,\r\nlast,50\r\n" +
            "FOOTER";
        static string Format(RcInNewLine r) => $"{r.Id}/{r.Name}/{r.Qty}";

        // Act: read through all paths
        var outcomes = await ReadEverywhere<RcInNewLine>(input, Format);

        // Assert: identical outcomes, and the reference itself is right
        await AssertAllEqual(outcomes);
        var reference = outcomes["ReadString"];
        await Assert.That(reference.Records).IsEqualTo("1/alpha/10|5/last/50");
        await Assert.That(reference.Header).IsEqualTo("HEADER");
        await Assert.That(reference.Footer).IsEqualTo("FOOTER");
        // 3.x: errors on the failing physical line; after a bad first line the next line starts a record
        await Assert.That(reference.Errors).IsEqualTo("7:ConvertException,8:FileHelpersException,9:BadUsageException,10:BadUsageException,11:BadUsageException");
        await Assert.That(reference.Total).IsEqualTo(2);
        await Assert.That(reference.LineNumber).IsEqualTo(13);
    }

    [Test]
    [DisplayName("Consistency: records whose first field is on a new line are identical on every read path")]
    public async Task FirstFieldInNewLineInput_SameOutcomeOnEveryPath()
    {
        // Arrange: blank first lines (one with spaces), a comment between records, a bad value, text before a field
        const string input =
            "HEADER\r\n" +
            "\r\n1,alpha\r\n" +
            "# comment\r\n" +
            "\r\nx,beta\r\n" +
            "junk\r\n3,gamma\r\n" +
            "  \r\n4,delta\r\n";
        static string Format(RcFirstInNewLine r) => $"{r.Id}/{r.Name}";

        // Act: read through all paths
        var outcomes = await ReadEverywhere<RcFirstInNewLine>(input, Format);

        // Assert: identical outcomes, and the reference itself is right
        await AssertAllEqual(outcomes);
        var reference = outcomes["ReadString"];
        await Assert.That(reference.Records).IsEqualTo("1/alpha|4/delta");
        await Assert.That(reference.Header).IsEqualTo("HEADER");
        // 3.x: the bad value is on line 6; after the bad first line "junk" the next line starts a record
        await Assert.That(reference.Errors).IsEqualTo("6:ConvertException,7:BadUsageException,8:BadUsageException");
        await Assert.That(reference.LineNumber).IsEqualTo(10);
    }

    [Test]
    [DisplayName("Consistency: [FieldInNewLine] records written by every write path read back unchanged")]
    public async Task FieldInNewLine_WriteThenRead_RoundTrips()
    {
        // Arrange: records plus header/footer
        var rows = new[]
        {
            new RcInNewLine { Id = 1, Name = "alpha", Qty = 10 },
            new RcInNewLine { Id = 2, Name = "beta", Qty = 20 },
        };
        var engine = new FileHelperEngine<RcInNewLine> { HeaderText = "HEADER", FooterText = "FOOTER" };
        using var asyncEngine = new FileHelperAsyncEngine<RcInNewLine> { HeaderText = "HEADER", FooterText = "FOOTER" };

        // Act: write through the batch and the streaming engine, then read back
        string batch = engine.WriteString(rows);
        asyncEngine.BeginWriteString();
        asyncEngine.WriteNext(rows);
        string streamed = asyncEngine.EndWriteString();
        var back = new FileHelperEngine<RcInNewLine>().ReadString(batch);

        // Assert: identical text in the 3.x layout, values preserved
        await Assert.That(batch).IsEqualTo("HEADER\r\n1,\r\nalpha,10\r\n2,\r\nbeta,20\r\nFOOTER\r\n");
        await Assert.That(streamed).IsEqualTo(batch);
        await Assert.That(string.Join("|", back.Select(r => $"{r.Id}/{r.Name}/{r.Qty}"))).IsEqualTo("1/alpha/10|2/beta/20");
    }

    [Test]
    [DisplayName("Consistency: [FieldInNewLine] records with multiline quoted values are identical on every read path")]
    public async Task FieldInNewLineMultilineInput_SameOutcomeOnEveryPath()
    {
        // Arrange: values spanning lines (one with a comment-like line and the delimiter), a comment, a bad record
        const string input =
            "HEADER\r\n" +
            "1,\"two\r\nlines\",\r\nalpha,10\r\n" +
            "# comment\r\n" +
            "2,\"a,\r\n# not a comment\r\nb\",\r\nbeta,oops\r\n" +
            "3,plain,\r\ngamma,30\r\n" +
            "4,\"x\r\ny\",\r\ndelta,40\r\n" +
            "FOOTER";
        static string Format(RcInNewLineMultiline r) => $"{r.Id}/{r.Notes.Replace("\r\n", "\\n", StringComparison.Ordinal)}/{r.Name}/{r.Qty}";

        // Act: read through all paths
        var outcomes = await ReadEverywhere<RcInNewLineMultiline>(input, Format);

        // Assert: identical outcomes, and the reference itself is right
        await AssertAllEqual(outcomes);
        var reference = outcomes["ReadString"];
        await Assert.That(reference.Records).IsEqualTo("1/two\\nlines/alpha/10|3/plain/gamma/30|4/x\\ny/delta/40");
        await Assert.That(reference.Header).IsEqualTo("HEADER");
        await Assert.That(reference.Footer).IsEqualTo("FOOTER");
        // The bad value is on physical line 9 (3.x reports that line)
        await Assert.That(reference.Errors).IsEqualTo("9:ConvertException");
        await Assert.That(reference.LineNumber).IsEqualTo(14);
    }

    [Test]
    [DisplayName("Consistency: records with a [FieldDelimiter] field are identical on every read path")]
    public async Task PerFieldDelimiterInput_SameOutcomeOnEveryPath()
    {
        // Arrange: the delimiter after Name is its own "|" (3.x), one value spans two lines, two bad layouts
        const string input =
            "HEADER\r\n" +
            "1,alpha|10\r\n" +
            "2,\"multi\r\nline|x\"|20\r\n" +
            "3|bad,30\r\n" +
            "4,beta|40,extra\r\n" +
            "5,\"q,|\"|50\r\n" +
            "FOOTER";
        static string Format(RcPerFieldDelimiter r) => $"{r.Id}/{r.Name.Replace("\r\n", "\\n", StringComparison.Ordinal)}/{r.Qty}";

        // Act: read through all paths
        var outcomes = await ReadEverywhere<RcPerFieldDelimiter>(input, Format);

        // Assert: identical outcomes, and the reference itself is right
        await AssertAllEqual(outcomes);
        var reference = outcomes["ReadString"];
        await Assert.That(reference.Records).IsEqualTo("1/alpha/10|2/multi\\nline|x/20|5/q,|/50");
        await Assert.That(reference.Footer).IsEqualTo("FOOTER");
        await Assert.That(reference.Errors).IsEqualTo("5:FileHelpersException,6:BadUsageException");
        await Assert.That(reference.LineNumber).IsEqualTo(7);
    }

    [Test]
    [DisplayName("Consistency: a large single-line input (parallel chunking active) is identical on every read path")]
    public async Task LargeInput_SameOutcomeOnEveryPath()
    {
        // Arrange: ~300 KB framed input with empty, comment and bad lines
        var sb = new StringBuilder("HEADER\r\n");
        for (int i = 1; i <= 25_000; i++)
        {
            sb.Append((i % 4000) switch
            {
                1 => string.Empty,
                2 => "# comment",
                3 => "x,bad",
                _ => $"{i},n{i}",
            }).Append("\r\n");
        }

        sb.Append("F1\r\nF2");

        // Act: read through all paths
        var outcomes = await ReadEverywhere<RcFramed>(sb.ToString(), r => $"{r.Id}/{r.Name}");

        // Assert: identical outcomes
        await AssertAllEqual(outcomes);
        await Assert.That(outcomes["ReadString"].Total).IsEqualTo(25_000 - 21);
        await Assert.That(outcomes["ReadString"].Footer).IsEqualTo("F1\r\nF2");
    }
}
