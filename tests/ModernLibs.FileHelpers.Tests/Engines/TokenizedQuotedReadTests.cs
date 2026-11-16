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
// MA0048: the record types and the counting descriptors of these tests are declared next to them.
#pragma warning disable MA0048

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using ModernLibs.FileHelpers.Parsing;
using ModernLibs.FileHelpers.Tests.Fuzz.DiffFuzz;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// Records with <c>[FieldQuoted]</c> fields on the tokenized read path (T20, R6): the default engine reads the same
/// records and errors as the legacy path (<see cref="EnginePaths.LegacyParse"/>) for the 3.x quote cases, and the
/// counting descriptors show which path each record took. The generated tokenized bodies parse (no checking
/// descriptor in between), so the comparisons test the generated <c>TryParse</c> too.
/// </summary>
[Category("Engines")]
public sealed class TokenizedQuotedReadTests
{
    private static readonly EnginePaths TokensPath = new(LegacyParse: false, ScalarTokenizer: false, LegacyWriter: false);
    private static readonly EnginePaths LegacyPath = new(LegacyParse: true, ScalarTokenizer: false, LegacyWriter: false);
    private static readonly EnginePaths ScalarPath = new(LegacyParse: false, ScalarTokenizer: true, LegacyWriter: false);

    private static FileHelperEngine<T> Engine<T>(IRecordDescriptor<T> descriptor, EnginePaths paths) => new(descriptor, Encoding.UTF8, paths);

    private static FileHelperEngine<T> Engine<T>(EnginePaths paths) => Engine(FileHelperRegistry<T>.Descriptor, paths);

    /// <summary>The records and the errors (line, column, type, message, record text) of an array read with <see cref="ErrorMode.SaveAndContinue"/>.</summary>
    private static string Text<T>(FileHelperEngine<T> engine, string input)
    {
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        var text = new StringBuilder();
        foreach (var record in engine.ReadString(input))
            text.Append("record:").Append(record).Append('\n');
        foreach (var error in engine.ErrorManager.Errors)
        {
            text.Append(CultureInfo.InvariantCulture, $"error:{error.LineNumber}:{error.ColumnIndex}:{error.ExceptionInfo?.GetType().Name}:{error.ExceptionInfo?.Message}:{error.RecordString}")
                .Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// The default engine over <paramref name="counting"/> against the legacy engine over the generated descriptor,
    /// through <see cref="ReadComparer"/>; <see langword="null"/> when equal. The scalar tokenizer is compared too.
    /// </summary>
    private static string? Compare<T>(string input, IRecordDescriptor<T> counting) =>
        ReadComparer.Compare(input, i => Text(Engine(counting, TokensPath), i), i => Text(Engine<T>(LegacyPath), i))
        ?? ReadComparer.Compare(input, i => Text(Engine<T>(ScalarPath), i), i => Text(Engine<T>(LegacyPath), i));

    private static string Rows(params string[] rows) => string.Concat(rows.Select(r => r + "\r\n"));

    [Test]
    public async Task Quote_Not_At_Field_Start()
    {
        var counting = new CountingDescriptor<TqQuoted>();
        string input = Rows("1,x\"y\",t", "2,5\" ruler,t", "3, \"spaced\",t", "4,\"ok\",t");
        await Assert.That(Compare(input, counting)).IsNull();
        await Assert.That(counting.TryParseCalls).IsEqualTo(4);
        await Assert.That(Text(Engine<TqQuoted>(TokensPath), input)).Contains("record:2|5\" ruler|t");
    }

    [Test]
    public async Task Text_After_A_Closing_Quote_In_A_Middle_Field()
    {
        var counting = new CountingDescriptor<TqQuoted>();
        string input = Rows("1,\"a\"x,t", "2,\"a\"x\",t", "3,\"ok\",t");
        string read = Text(Engine(counting, TokensPath), input);
        await Assert.That(Compare(input, counting)).IsNull();
        await Assert.That(read).Contains("error:1:");
        await Assert.That(counting.TryParseCalls).IsEqualTo(2);         // two reads: only row 3 is tokenized

        // Blanks after the closing quote of a field that trims its end: legacy accepts them, the tokenizer refuses.
        var trimmed = new CountingDescriptor<TqTrimRight>();
        string blanks = Rows("1,\"a\"  ,t", "2,\"b\",t");
        await Assert.That(Compare(blanks, trimmed)).IsNull();
        await Assert.That(Text(Engine<TqTrimRight>(TokensPath), blanks)).Contains("record:1|a|t");
        await Assert.That(trimmed.TryParseCalls).IsEqualTo(1);
    }

    [Test]
    public async Task Text_After_A_Closing_Quote_In_The_Last_Field()
    {
        var counting = new CountingDescriptor<TqLastQuoted>();
        string input = Rows("1,\"a\"z", "2,\"b\" ", "3,\"c\"\"\"x", "4,\"d\"");
        await Assert.That(Compare(input, counting)).IsNull();
        await Assert.That(Text(Engine<TqLastQuoted>(TokensPath), input)).Contains("record:1|a");
        await Assert.That(counting.TryParseCalls).IsEqualTo(1);         // only row 4 is tokenized
    }

    [Test]
    public async Task Each_QuoteMode_With_Quoted_And_Unquoted_Values()
    {
        var counting = new CountingDescriptor<TqModes>();
        string input = Rows(
            "\"a\",\"b\",\"c\",\"1\",e",
            "a,b,c,1,e",
            "\"a\",b,\"c\",1,\"e\"",
            "\"a\",\"b\",c,\"1\",e",
            "a,\"b\",\"c\",\"1\",e",
            "\"a\",\"\",\"\",\"\",",
            "\"a\"\"x\",\"b,y\",\"c\r\nz\",\"2\",e");
        await Assert.That(new RecordReadCore<TqModes>(FileHelperRegistry<TqModes>.Descriptor, TokensPath).Tokenized).IsTrue();
        await Assert.That(Compare(input, counting)).IsNull();
        await Assert.That(counting.TryParseCalls).IsGreaterThan(0);
        await Assert.That(Text(Engine<TqModes>(TokensPath), input)).Contains("record:a\"x|b,y|c\r\nz|2|e");
    }

    /// <summary>Review Focus 3: an <c>AlwaysQuoted</c> trailing optional field that is absent is not checked (emitter rule 1).</summary>
    [Test]
    public async Task Absent_Optional_AlwaysQuoted_Trailing_Field_Is_Read_Tokenized()
    {
        var counting = new CountingDescriptor<TqOptionalAlways>();
        string input = Rows("1,a", "2,b", "3,c,\"t,u\"", "4,d");
        await Assert.That(Compare(input, counting)).IsNull();
        await Assert.That(counting.TryParseCalls).IsEqualTo(4);
        await Assert.That(counting.ParseCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Empty_Optional_AlwaysQuoted_Trailing_Field()
    {
        var counting = new CountingDescriptor<TqOptionalAlways>();
        await Assert.That(Compare(Rows("1,a,", "2,b,\"\"", "3,c,\"x\""), counting)).IsNull();
    }

    [Test]
    public async Task Multiline_Value_Is_Read_By_The_Legacy_Path()
    {
        var counting = new CountingDescriptor<TqQuoted>();
        string input = Rows("1,\"multi\r\nline\",t", "2,\"b\",t", "3,\"x\ny\",t", "4,\"c\",t");
        var legacy = Text(Engine<TqQuoted>(LegacyPath), input);
        await Assert.That(Text(Engine(counting, TokensPath), input)).IsEqualTo(legacy);
        await Assert.That(legacy).Contains("record:1|multi\r\nline|t");
        await Assert.That(counting.TryParseCalls).IsEqualTo(2);         // rows 2 and 4; the assembled rows 1 and 3 go legacy
        await Assert.That(counting.ParseCalls).IsEqualTo(2);
    }

    [Test]
    public async Task BeforeReadRecord_Replacing_A_Quoted_Record()
    {
        string Read(IRecordDescriptor<TqQuoted> descriptor, EnginePaths paths)
        {
            var engine = Engine(descriptor, paths);
            engine.BeforeReadRecord += (_, e) =>
            {
                if (e.RecordLine.StartsWith('1'))
                    e.RecordLine = "7,\"b,\"\"c\"\"\",u";
            };
            return Text(engine, Rows("1,\"a\",t", "2,\"z\",t"));
        }

        var counting = new CountingDescriptor<TqQuoted>();
        string tokenized = Read(counting, TokensPath);
        await Assert.That(tokenized).IsEqualTo(Read(FileHelperRegistry<TqQuoted>.Descriptor, LegacyPath));
        await Assert.That(tokenized).Contains("record:7|b,\"c\"|u");
        await Assert.That(counting.TryParseCalls).IsEqualTo(2);
    }

    /// <summary>F-11: a quoted field that trims its start skips blanks before its quote, which the tokenizer does not.</summary>
    [Test]
    public async Task Quoted_Field_With_A_Start_Trim_Is_Not_Tokenized()
    {
        await Assert.That(new RecordReadCore<TqTrimBoth>(FileHelperRegistry<TqTrimBoth>.Descriptor, TokensPath).Tokenized).IsFalse();
        await Assert.That(new RecordReadCore<TqTrimLeft>(FileHelperRegistry<TqTrimLeft>.Descriptor, TokensPath).Tokenized).IsFalse();
        await Assert.That(new RecordReadCore<TqTrimRight>(FileHelperRegistry<TqTrimRight>.Descriptor, TokensPath).Tokenized).IsTrue();
    }

    /// <summary>F-11: the quoted benchmark type (<c>QuotedFieldsBenchmarks.QuotedRecord</c>) is measured on the tokenized path.</summary>
    [Test]
    public async Task Benchmark_Quoted_Record_Is_Eligible()
    {
        var core = new RecordReadCore<TqBenchmarkQuoted>(FileHelperRegistry<TqBenchmarkQuoted>.Descriptor, TokensPath);
        await Assert.That(core.Tokenized).IsTrue();
        await Assert.That(core.Multiline).IsTrue();                     // read through the assembler: its scan tokenizes
    }

    /// <summary>
    /// <c>default(QuoteScan)</c> (here: the scan dropped for <c>BeforeReadRecord</c>) means "not tokenized yet", never
    /// "no tokens": a type whose fields are all optional must not parse an empty token list.
    /// </summary>
    [Test]
    public async Task Record_Type_With_Only_Optional_Fields_And_A_Default_Scan()
    {
        string Read(IRecordDescriptor<TqAllOptional> descriptor, EnginePaths paths)
        {
            var engine = Engine(descriptor, paths);
            engine.BeforeReadRecord += (_, _) => { };
            return Text(engine, Rows("\"a\",b", "c", "", "\"d,e\"", "\"f\r\ng\",h"));
        }

        var counting = new CountingDescriptor<TqAllOptional>();
        await Assert.That(new RecordReadCore<TqAllOptional>(counting, TokensPath).MandatoryFieldCount).IsEqualTo(0);
        string tokenized = Read(counting, TokensPath);
        await Assert.That(tokenized).IsEqualTo(Read(FileHelperRegistry<TqAllOptional>.Descriptor, LegacyPath));
        await Assert.That(tokenized).Contains("record:a|b");
        await Assert.That(counting.TryParseCalls).IsEqualTo(5);
    }

    /// <summary>
    /// Review D4: a lone quote char opens a quoted value that never closes. The tokenizer gives no tokens; the legacy
    /// path throws its error, the same type and text with both multiline modes.
    /// </summary>
    [Test]
    [Arguments("\"")]
    [Arguments("a,\"")]
    [Arguments("\",a")]
    public async Task Lone_Quote_Throws_The_Legacy_Error(string record)
    {
        string input = record + "\r\n";
        var notAllow = new CountingDescriptor<TqTwoQuotedNotAllow>();
        var multiline = new CountingDescriptor<TqTwoQuoted>();

        string? notAllowDifference = ReadComparer.Compare(
            input, i => string.Join("\n", Engine(notAllow, TokensPath).ReadString(i)), i => string.Join("\n", Engine<TqTwoQuotedNotAllow>(LegacyPath).ReadString(i)));
        string? multilineDifference = ReadComparer.Compare(
            input, i => string.Join("\n", Engine(multiline, TokensPath).ReadString(i)), i => string.Join("\n", Engine<TqTwoQuoted>(LegacyPath).ReadString(i)));

        await Assert.That(notAllowDifference).IsNull();
        await Assert.That(multilineDifference).IsNull();
        await Assert.That(Catch(() => Engine(notAllow, TokensPath).ReadString(input))).Contains("nclosed quoted");
        await Assert.That(Catch(() => Engine(multiline, TokensPath).ReadString(input))).Contains("nclosed quoted");
        await Assert.That(notAllow.TryParseCalls).IsEqualTo(0);
        await Assert.That(multiline.TryParseCalls).IsEqualTo(0);
    }

    private static string Catch(Action action)
    {
        try
        {
            action();
            return "<no exception>";
        }
        catch (Exception ex)
        {
            return ex.GetType().Name + ":" + ex.Message;
        }
    }

    /// <summary>
    /// Review required change 1: the assembler scan tokenizes only for a tokenized record type. A core that is not
    /// tokenized (start trim, or the legacy path) scans with its own rules when it is given a buffer anyway.
    /// </summary>
    [Test]
    public async Task Core_That_Is_Not_Tokenized_Scans_As_Before_With_A_Buffer()
    {
        static (bool Opens, int OpenField, QuoteScan Scan) Probe<T>(RecordReadCore<T> core, string line, TokenBuffer? buffer)
        {
            bool opens = core.OpensMultiline(line, 0, buffer, out int openField, out var scan);
            return (opens, openField, scan);
        }

        var trimmed = new RecordReadCore<TqTrimBoth>(FileHelperRegistry<TqTrimBoth>.Descriptor, TokensPath);
        var legacy = new RecordReadCore<TqQuoted>(FileHelperRegistry<TqQuoted>.Descriptor, LegacyPath);
        await Assert.That(trimmed.Tokenized).IsFalse();
        await Assert.That(legacy.Tokenized).IsFalse();

        foreach (string line in new[] { "1, \"a,b", "1, \"a\",t", "1,\"a,b", "1,\"a\",t" })
        {
            var trimmedScan = Probe(trimmed, line, new TokenBuffer(3));
            await Assert.That(trimmedScan).IsEqualTo(Probe(trimmed, line, null)).Because(line);
            await Assert.That(trimmedScan.Scan.TokenCount).IsEqualTo(0);

            var legacyScan = Probe(legacy, line, new TokenBuffer(3));
            await Assert.That(legacyScan).IsEqualTo(Probe(legacy, line, null)).Because(line);
            await Assert.That(legacyScan.Scan.TokenCount).IsEqualTo(0);
        }

        // The blanks before the quote are skipped for the start-trimmed field: the line opens a multiline value.
        await Assert.That(Probe(trimmed, "1, \"a,b", new TokenBuffer(3)).Opens).IsTrue();
    }

    // ---- Multi-line records in the readers that tokenize the whole text (review required change 3) --------------

    /// <summary>
    /// Multi-line quoted values with CRLF and LF breaks, a doubled quote inside one, fields after the closing quote on
    /// the last line, and a conversion error in a multi-line record (its line and column are compared).
    /// </summary>
    private const string MultiLineInput =
        "M,\"first\r\nline \"\"q\"\"\",1\r\n" +
        "D,\"d1\nmore\",2\r\n" +
        "D,\"plain\",3\r\n" +
        "M,\"bad\r\nrow\",x\r\n" +
        "D,\"two\r\nbreaks\nhere\",4\r\n" +
        "M,\"last\",5\r\n";

    private static string ShowAny(object record) => record.ToString() ?? "<null>";

    private static string Errors(ErrorManager errors) =>
        string.Concat(errors.Errors.Select(e => string.Create(CultureInfo.InvariantCulture, $"error:{e.LineNumber}:{e.ColumnIndex}:{e.ExceptionInfo?.GetType().Name}:{e.ExceptionInfo?.Message}\n")));

    [Test]
    public async Task MultiRecord_Reader_Tokenizes_Multi_Line_Records_Like_Legacy()
    {
        static (string Text, int MasterCalls, int DetailCalls) Read(EnginePaths paths)
        {
            var masterDescriptor = new CountingDescriptor<TqMaster>();
            var detailDescriptor = new CountingDescriptor<TqDetail>();
            var engine = new MultiRecordEngine(
                new MultiRecordType<TqMaster>(line => !line.IsEmpty && line[0] == 'M', masterDescriptor, paths),
                new MultiRecordType<TqDetail>(line => !line.IsEmpty && line[0] == 'D', detailDescriptor, paths))
            {
                ErrorMode = ErrorMode.SaveAndContinue,
            };
            string text = string.Concat(engine.ReadString(MultiLineInput).Select(r => ShowAny(r) + "\n")) + Errors(engine.ErrorManager);
            return (text, masterDescriptor.TryParseCalls, detailDescriptor.TryParseCalls);
        }

        var tokenized = Read(TokensPath);
        var legacy = Read(LegacyPath);
        await Assert.That(tokenized.Text).IsEqualTo(legacy.Text);
        await Assert.That(tokenized.Text).Contains("M first\r\nline \"q\"|1");
        await Assert.That(tokenized.Text).Contains("error:6:");
        await Assert.That(tokenized.MasterCalls).IsEqualTo(3);          // the multi-line records too: no scan, the whole text
        await Assert.That(tokenized.DetailCalls).IsEqualTo(3);
        await Assert.That(legacy.MasterCalls).IsEqualTo(0);
    }

    [Test]
    public async Task MasterDetail_Reader_Tokenizes_Multi_Line_Records_Like_Legacy()
    {
        static string Read(EnginePaths paths)
        {
            var engine = new MasterDetailEngine<TqMaster, TqDetail>(
                line => !line.IsEmpty && line[0] == 'M' ? RecordAction.Master : RecordAction.Detail, paths)
            {
                ErrorMode = ErrorMode.SaveAndContinue,
            };
            var text = new StringBuilder();
            foreach (var group in engine.ReadString(MultiLineInput))
            {
                text.Append(group.Master).Append('\n');
                foreach (var detail in group.Details)
                    text.Append("  ").Append(detail).Append('\n');
            }

            return text.Append(Errors(engine.ErrorManager)).ToString();
        }

        await Assert.That(new MasterDetailEngine<TqMaster, TqDetail>(_ => RecordAction.Master, TokensPath).MasterCore.Tokenized).IsTrue();
        string tokenized = Read(TokensPath);
        await Assert.That(tokenized).IsEqualTo(Read(LegacyPath));
        await Assert.That(tokenized).Contains("  D d1\nmore|2");           // the details of the failing master are dropped
        await Assert.That(tokenized).DoesNotContain("D two");
        await Assert.That(tokenized).Contains("error:6:");
    }

    /// <summary>
    /// With a <c>BeforeReadRecord</c> handler the session drops the scan, so <c>Parse</c> tokenizes the whole text of
    /// a multi-line record (spec 4.5): the same records and errors as legacy.
    /// </summary>
    [Test]
    public async Task BeforeReadRecord_Session_Tokenizes_Multi_Line_Records_Like_Legacy()
    {
        const string input = "1,\"first\r\nline \"\"q\"\"\",t\r\n2,\"x\ny\",u\r\n3,\"bad\r\nrow\",v,w\r\nx,\"b\r\nc\",t\r\n4,\"ok\",t\r\n";
        string Read(IRecordDescriptor<TqQuoted> descriptor, EnginePaths paths)
        {
            var engine = Engine(descriptor, paths);
            engine.BeforeReadRecord += (_, _) => { };
            return Text(engine, input);
        }

        var counting = new CountingDescriptor<TqQuoted>();
        string tokenized = Read(counting, TokensPath);
        await Assert.That(tokenized).IsEqualTo(Read(FileHelperRegistry<TqQuoted>.Descriptor, LegacyPath));
        await Assert.That(tokenized).Contains("record:1|first\r\nline \"q\"|t");
        await Assert.That(tokenized).Contains("error:7:");              // the conversion error of the record on lines 7-8
        await Assert.That(counting.TryParseCalls).IsEqualTo(4);         // every record but the one with an extra field
    }

    /// <summary><see cref="INotifyRead{T}"/> records go through <c>ParseInto</c>, which has no scan: the whole text is tokenized.</summary>
    [Test]
    public async Task NotifyRead_Records_Tokenize_Multi_Line_Records_Like_Legacy()
    {
        const string input = "1,\"first\r\nline \"\"q\"\"\"\r\n2,\"x\ny\"\r\nx,\"b\r\nc\"\r\n3,\"ok\"\r\n";
        var counting = new RecyclableCountingDescriptor<TqNotify>();
        string tokenized = Text(Engine(counting, TokensPath), input);
        await Assert.That(tokenized).IsEqualTo(Text(Engine<TqNotify>(LegacyPath), input));
        await Assert.That(tokenized).Contains("record:1|first\r\nline \"q\"|B,A");
        await Assert.That(tokenized).Contains("error:5:");
        await Assert.That(counting.TryParseIntoCalls).IsEqualTo(4);
        await Assert.That(counting.ParseIntoCalls).IsEqualTo(0);
    }
}

/// <summary>
/// The generated tokenized descriptor of <typeparamref name="T"/>, counting the calls of its parse methods; every
/// call forwards to the generated descriptor, so the generated bodies parse.
/// </summary>
internal class CountingDescriptor<T> : ITokenizedDescriptor<T>
{
    private int _tryParseCalls;
    private int _parseCalls;

    protected ITokenizedDescriptor<T> Inner { get; } = (ITokenizedDescriptor<T>)FileHelperRegistry<T>.Descriptor;

    public int TryParseCalls => Volatile.Read(ref _tryParseCalls);

    public int ParseCalls => Volatile.Read(ref _parseCalls);

    public ReadOnlySpan<FieldDescriptor> Fields => Inner.Fields;

    public RecordOptions Options => Inner.Options;

    public T Parse(ReadOnlySpan<char> lines, int lineCount)
    {
        Interlocked.Increment(ref _parseCalls);
        return Inner.Parse(lines, lineCount);
    }

    public void Format(T record, IBufferWriter<char> writer) => Inner.Format(record, writer);

    public T CreateDefaultInstance() => Inner.CreateDefaultInstance();

    public bool TryParse(ReadOnlySpan<char> record, in RecordTokens tokens, [MaybeNullWhen(false)] out T result)
    {
        Interlocked.Increment(ref _tryParseCalls);
        return Inner.TryParse(record, in tokens, out result);
    }
}

/// <summary><see cref="CountingDescriptor{T}"/> for a descriptor that parses into an existing instance.</summary>
internal sealed class RecyclableCountingDescriptor<T> : CountingDescriptor<T>, IRecyclableDescriptor<T>, ITokenizedDescriptor<T>
{
    private int _tryParseIntoCalls;
    private int _parseIntoCalls;

    public int TryParseIntoCalls => Volatile.Read(ref _tryParseIntoCalls);

    public int ParseIntoCalls => Volatile.Read(ref _parseIntoCalls);

    public void ParseInto(ReadOnlySpan<char> lines, int lineCount, T existing)
    {
        Interlocked.Increment(ref _parseIntoCalls);
        ((IRecyclableDescriptor<T>)Inner).ParseInto(lines, lineCount, existing);
    }

    public bool TryParseInto(ReadOnlySpan<char> record, in RecordTokens tokens, T existing)
    {
        Interlocked.Increment(ref _tryParseIntoCalls);
        return Inner.TryParseInto(record, in tokens, existing);
    }
}

/// <summary>Id, an <c>OptionalForRead</c> quoted name (multiline allowed: the default), a plain tail.</summary>
[DelimitedRecord(",")]
public sealed class TqQuoted
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public string Tail { get; set; } = string.Empty;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Id}|{Name}|{Tail}");
}

/// <summary>The quoted field is the last one: 3.x ignores text after its closing quote.</summary>
[DelimitedRecord(",")]
public sealed class TqLastQuoted
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted] public string Name { get; set; } = string.Empty;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Id}|{Name}");
}

/// <summary>A quoted field that trims its end: blanks after the closing quote are allowed (tokenized type).</summary>
[DelimitedRecord(",")]
public sealed class TqTrimRight
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted, FieldTrim(TrimMode.Right)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public string Tail { get; set; } = string.Empty;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Id}|{Name}|{Tail}");
}

/// <summary>Not tokenized (F-11): a quoted field that trims both ends.</summary>
[DelimitedRecord(",")]
public sealed class TqTrimBoth
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted, FieldTrim(TrimMode.Both)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public string Tail { get; set; } = string.Empty;
}

/// <summary>Not tokenized (F-11): a quoted field that trims its start.</summary>
[DelimitedRecord(",")]
public sealed class TqTrimLeft
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted, FieldTrim(TrimMode.Left)] public string Name { get; set; } = string.Empty;
}

/// <summary>One field per <see cref="QuoteMode"/>, the last one plain.</summary>
[DelimitedRecord(",")]
public sealed class TqModes
{
    [FieldOrder(1), FieldQuoted(QuoteMode.AlwaysQuoted)] public string A { get; set; } = string.Empty;
    [FieldOrder(2), FieldQuoted(QuoteMode.OptionalForRead)] public string B { get; set; } = string.Empty;
    [FieldOrder(3), FieldQuoted(QuoteMode.OptionalForWrite)] public string C { get; set; } = string.Empty;
    [FieldOrder(4), FieldQuoted(QuoteMode.OptionalForBoth)] public int D { get; set; }
    [FieldOrder(5)] public string E { get; set; } = string.Empty;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{A}|{B}|{C}|{D}|{E}");
}

/// <summary>A trailing <c>[FieldOptional]</c> field that must be quoted when present (Review Focus 3).</summary>
[DelimitedRecord(",")]
public sealed class TqOptionalAlways
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3), FieldOptional, FieldQuoted(QuoteMode.AlwaysQuoted)] public string? Tail { get; set; }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Id}|{Name}|{Tail ?? "<null>"}");
}

/// <summary>Every field optional: <c>MandatoryFieldCount</c> is 0.</summary>
[DelimitedRecord(",")]
public sealed class TqAllOptional
{
    [FieldOrder(1), FieldOptional, FieldQuoted] public string? A { get; set; }
    [FieldOrder(2), FieldOptional] public string? B { get; set; }

    public override string ToString() => $"{A ?? "<null>"}|{B ?? "<null>"}";
}

/// <summary>Two quoted fields whose values may span lines (the default).</summary>
[DelimitedRecord(",")]
public sealed class TqTwoQuoted
{
    [FieldOrder(1), FieldQuoted] public string A { get; set; } = string.Empty;
    [FieldOrder(2), FieldOptional, FieldQuoted] public string? B { get; set; }

    public override string ToString() => $"{A}|{B ?? "<null>"}";
}

/// <summary>Two quoted fields whose values may not span lines.</summary>
[DelimitedRecord(",")]
public sealed class TqTwoQuotedNotAllow
{
    [FieldOrder(1), FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.NotAllow)] public string A { get; set; } = string.Empty;
    [FieldOrder(2), FieldOptional, FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.NotAllow)] public string? B { get; set; }

    public override string ToString() => $"{A}|{B ?? "<null>"}";
}

/// <summary>The shape of the benchmark's <c>QuotedRecord</c> (<c>QuotedFieldsBenchmarks.cs</c>).</summary>
[DelimitedRecord(",")]
public sealed class TqBenchmarkQuoted
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted] public string Name { get; set; } = string.Empty;
    [FieldOrder(3), FieldQuoted] public string Description { get; set; } = string.Empty;
    [FieldOrder(4), FieldQuoted] public string Notes { get; set; } = string.Empty;
    [FieldOrder(5), FieldQuoted] public string Category { get; set; } = string.Empty;
}

/// <summary>Master record of the multi-line reader tests: lines that start with <c>M</c>.</summary>
[DelimitedRecord(",")]
public sealed class TqMaster
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2), FieldQuoted] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public int Id { get; set; }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Kind} {Name}|{Id}");
}

/// <summary>Detail record of the multi-line reader tests: lines that start with <c>D</c>.</summary>
[DelimitedRecord(",")]
public sealed class TqDetail
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2), FieldQuoted] public string Text { get; set; } = string.Empty;
    [FieldOrder(3)] public int Qty { get; set; }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Kind} {Text}|{Qty}");
}

/// <summary>An <see cref="INotifyRead{T}"/> record with a quoted field: parsed through <c>ParseInto</c>/<c>TryParseInto</c>.</summary>
[DelimitedRecord(",")]
public sealed class TqNotify : INotifyRead<TqNotify>
{
    [FieldHidden] private string _events = string.Empty;

    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted] public string Name { get; set; } = string.Empty;

    public void BeforeRead(BeforeReadEventArgs<TqNotify> e) => _events = "B";

    public void AfterRead(AfterReadEventArgs<TqNotify> e) => _events += ",A";

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Id}|{Name}|{_events}");
}
