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
// MA0048: the record types of these tests are declared next to them.
#pragma warning disable MA0048

using System.Globalization;
using System.Text;
using ModernLibs.FileHelpers.Converters;
using ModernLibs.FileHelpers.Parsing;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// The generated <see cref="ITokenizedDescriptor{T}"/> bodies (T17) at run time: the tokenized and the legacy body
/// read the same values and throw the same errors, with tokens the runtime builds or tokens written by hand (the
/// quoted bodies run here before T20 turns the quoted path on in the runtime).
/// </summary>
[Category("Engines")]
public sealed class TokenizedGeneratedReadTests
{
    private const int Quoted = TokenBuffer.QuotedBit;
    private const int Doubled = TokenBuffer.DoubledBit;

    private static readonly EnginePaths TokensPath = new(LegacyParse: false, ScalarTokenizer: false, LegacyWriter: false);
    private static readonly EnginePaths LegacyPath = new(LegacyParse: true, ScalarTokenizer: false, LegacyWriter: false);

    private static ITokenizedDescriptor<T> Tokenized<T>() => (ITokenizedDescriptor<T>)FileHelperRegistry<T>.Descriptor;

    private static FileHelperEngine<T> Engine<T>(EnginePaths paths) => new(FileHelperRegistry<T>.Descriptor, Encoding.UTF8, paths);

    /// <summary>Tokens of <paramref name="record"/> with the end entries <paramref name="ends"/>; the scratch holds the record.</summary>
    private static RecordTokens Tokens(TokenBuffer buffer, ReadOnlySpan<char> record, int[] ends, char quote = '\0')
    {
        ends.CopyTo(buffer.Ends, 0);
        buffer.EnsureScratch(record.Length);
        return buffer.Tokens(record, ends.Length, delimiterLength: 1, quote);
    }

    /// <summary>Tokens of a record without quoted fields, as the runtime finds them.</summary>
    private static RecordTokens PlainTokens(TokenBuffer buffer, ReadOnlySpan<char> record, char delimiter = ',')
    {
        int count = RecordTokenizer.TokenizePlain(record, delimiter, buffer.Ends, vectors: true);
        return buffer.Tokens(record, count, delimiterLength: 1);
    }

    private static string Describe(Exception? ex) =>
        ex is null ? "<no exception>" : $"{ex.GetType().Name}|{ex.Message}|{(ex as FileHelpersException)?.ColumnNumber}|{(ex as FileHelpersException)?.LineNumber}";

    private static string Catch(Action action)
    {
        try
        {
            action();
            return Describe(null);
        }
        catch (Exception ex)
        {
            return Describe(ex);
        }
    }

    /// <summary>The records and the errors of an array read with <see cref="ErrorMode.SaveAndContinue"/>.</summary>
    private static string Text<T>(EnginePaths paths, string input, Func<T, string> show)
    {
        var engine = Engine<T>(paths);
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        var text = new StringBuilder();
        foreach (var record in engine.ReadString(input))
            text.Append("record:").Append(show(record)).Append('\n');
        foreach (var error in engine.ErrorManager.Errors)
            text.Append(CultureInfo.InvariantCulture, $"error:{error.LineNumber}:{error.ColumnIndex}:{Describe(error.ExceptionInfo)}\n");
        return text.ToString();
    }

    [Test]
    public async Task Emitted_TryParse_Reads_Quoted_Tokens()
    {
        // "a ""b"" c",2 : field 0 ends at 11 (quoted, doubled quotes), field 1 at 13.
        const string record = "\"a \"\"b\"\" c\",2";
        static (bool Ok, string? A, int B) ReadDoubled()
        {
            var tokens = Tokens(new TokenBuffer(2), record, [11 | Quoted | Doubled, 13], '"');
            bool ok = Tokenized<TgQuoted>().TryParse(record, in tokens, out var result);
            return (ok, result?.A, result?.B ?? -1);
        }

        static (bool Ok, string? A, int B) Unquoted<T>(Func<T, (string, int)> values)
            where T : class
        {
            var tokens = Tokens(new TokenBuffer(2), "x,2", [1, 3]);
            bool ok = Tokenized<T>().TryParse("x,2", in tokens, out var result);
            return ok ? (true, values(result!).Item1, values(result!).Item2) : (false, null, -1);
        }

        var doubled = ReadDoubled();
        await Assert.That(doubled.Ok).IsTrue();
        await Assert.That(doubled.A).IsEqualTo("a \"b\" c");
        await Assert.That(doubled.B).IsEqualTo(2);

        // OptionalForRead takes the unquoted value; AlwaysQuoted gives the record back.
        var optional = Unquoted<TgQuoted>(r => (r.A, r.B));
        await Assert.That(optional).IsEqualTo((true, "x", 2));
        var always = Unquoted<TgAlwaysQuoted>(r => (r.A, r.B));
        await Assert.That(always.Ok).IsFalse();

        // The legacy body reads the same values.
        var legacy = FileHelperRegistry<TgQuoted>.Descriptor.Parse(record, 1);
        await Assert.That(legacy.A).IsEqualTo(doubled.A);
        await Assert.That(legacy.B).IsEqualTo(doubled.B);
    }

    /// <summary>Review Focus 1: an empty field and a missing field report the same error on both paths.</summary>
    [Test]
    [Arguments("1,,3,")]
    [Arguments("1,2")]
    [Arguments("1,2,3,")]
    [Arguments(",2,3")]
    [Arguments("1,2,x,4")]
    public async Task Empty_And_Missing_Fields_Give_The_Same_Error_Column(string input)
    {
        string Read(EnginePaths paths) => Catch(() => Engine<TgFour>(paths).ReadString(input + "\r\n"));

        await Assert.That(new RecordReadCore<TgFour>(FileHelperRegistry<TgFour>.Descriptor, TokensPath).Tokenized).IsTrue();
        string tokenized = Read(TokensPath);
        await Assert.That(tokenized).IsEqualTo(Read(LegacyPath));
        await Assert.That(tokenized).IsNotEqualTo("<no exception>");
    }

    /// <summary>Review Focus 7: <c>TryParse</c> gives the record back before any converter runs.</summary>
    [Test]
    [NotInParallel(nameof(CountingConverter))]
    public async Task Custom_Converter_Runs_Once_When_The_Record_Is_Given_Back()
    {
        static (bool Ok, int Calls, string? A, string? B) Run(string record, int[] ends)
        {
            CountingConverter.Calls = 0;
            var tokens = Tokens(new TokenBuffer(2), record, ends, '"');
            bool ok = Tokenized<TgCounting>().TryParse(record, in tokens, out var result);
            return (ok, CountingConverter.Calls, result?.A, result?.B);
        }

        var unquoted = Run("a,b", [1, 3]);
        await Assert.That(unquoted.Ok).IsFalse();
        await Assert.That(unquoted.Calls).IsEqualTo(0);

        var quoted = Run("a,\"b\"", [1, 5 | Quoted]);
        await Assert.That(quoted).IsEqualTo((true, 1, "A", "b"));

        // An engine read: one converter call per record.
        CountingConverter.Calls = 0;
        var rows = Engine<TgCounting>(TokensPath).ReadString("a,\"b\"\r\nc,\"d\"\r\ne,\"f\"\r\n");
        await Assert.That(rows.Length).IsEqualTo(3);
        await Assert.That(CountingConverter.Calls).IsEqualTo(3);
    }

    [Test]
    [Arguments("1,abc")]
    [Arguments("abc,1")]
    [Arguments("1, ")]
    [Arguments("99999999999,1")]
    public async Task Conversion_Error_Is_The_Same_In_Both_Bodies(string record)
    {
        string Parse() => Catch(() => FileHelperRegistry<TgTwoInts>.Descriptor.Parse(record, 1));
        string TryParse() => Catch(() =>
        {
            var tokens = PlainTokens(new TokenBuffer(2), record);
            _ = Tokenized<TgTwoInts>().TryParse(record, in tokens, out _);
        });

        string legacy = Parse();
        await Assert.That(TryParse()).IsEqualTo(legacy);
        await Assert.That(legacy).IsNotEqualTo("<no exception>");
    }

    [Test]
    [Arguments("*-abc-*,x,7 \r\n")]
    [Arguments("abc,x,7\r\n")]
    [Arguments(" -a- ,x, 7\r\n")]
    [Arguments("abc, ,7\r\n")]
    [Arguments("abc,,7\r\n")]
    [Arguments("abc,x,\r\n")]
    [Arguments("abc,x,y\r\n")]
    [Arguments("**,x,1\r\n")]
    public async Task TrimGivenChars_And_FieldNotEmpty_Behave_The_Same_On_Both_Paths(string input)
    {
        static string Show(TgTrim r) => string.Create(CultureInfo.InvariantCulture, $"{r.A}|{r.B}|{r.C}");

        await Assert.That(new RecordReadCore<TgTrim>(FileHelperRegistry<TgTrim>.Descriptor, TokensPath).Tokenized).IsTrue();
        await Assert.That(Text<TgTrim>(TokensPath, input, Show)).IsEqualTo(Text<TgTrim>(LegacyPath, input, Show));
    }

    /// <summary>F06: a 3-field record into an instance that held a 5-field record overwrites every member.</summary>
    [Test]
    public async Task Emitted_TryParseInto_Overwrites_Every_Member()
    {
        const string five = "1,A,2.5,note,7";
        const string three = "2,B,3.5";

        static string Show(TgRecycle r) =>
            string.Create(CultureInfo.InvariantCulture, $"{r.Id}|{r.Name}|{r.Amount}|{r.Note ?? "<null>"}|{(r.Rank is { } rank ? rank.ToString(CultureInfo.InvariantCulture) : "<null>")}");

        static (bool Ok5, string After5, bool Ok3, string After3) ReadTokenized()
        {
            var descriptor = Tokenized<TgRecycle>();
            var buffer = new TokenBuffer(5);
            var target = new TgRecycle();
            var tokens = PlainTokens(buffer, five);
            bool ok5 = descriptor.TryParseInto(five, in tokens, target);
            string after5 = Show(target);
            tokens = PlainTokens(buffer, three);
            bool ok3 = descriptor.TryParseInto(three, in tokens, target);
            return (ok5, after5, ok3, Show(target));
        }

        var recyclable = (IRecyclableDescriptor<TgRecycle>)FileHelperRegistry<TgRecycle>.Descriptor;
        var legacy = new TgRecycle();
        recyclable.ParseInto(five, 1, legacy);
        string legacy5 = Show(legacy);
        recyclable.ParseInto(three, 1, legacy);

        var tokenized = ReadTokenized();
        await Assert.That(tokenized.Ok5).IsTrue();
        await Assert.That(tokenized.Ok3).IsTrue();
        await Assert.That(tokenized.After5).IsEqualTo(legacy5);
        await Assert.That(tokenized.After3).IsEqualTo(Show(legacy));
        await Assert.That(tokenized.After3).IsEqualTo("2|B|3.5||<null>");

        // The same two rows through a recycling callback read.
        static List<string> Read(EnginePaths paths)
        {
            var engine = Engine<TgRecycle>(paths);
            engine.RecycleRecords = true;
            var seen = new List<string>();
            engine.ReadString(five + "\r\n" + three + "\r\n", r => seen.Add(Show(r)));
            return seen;
        }

        var read = Read(TokensPath);
        await Assert.That(read).IsEquivalentTo(Read(LegacyPath));
        await Assert.That(read.Count).IsEqualTo(2);
        await Assert.That(read[1]).IsEqualTo("2|B|3.5||<null>");
    }

    [Test]
    public async Task Generated_Descriptors_Implement_The_Interface_Where_Eligible()
    {
        await Assert.That(FileHelperRegistry<TgRecycle>.Descriptor is ITokenizedDescriptor<TgRecycle>).IsTrue();
        await Assert.That(FileHelperRegistry<TgQuoted>.Descriptor is ITokenizedDescriptor<TgQuoted>).IsTrue();
        await Assert.That(FileHelperRegistry<TgFixed>.Descriptor is ITokenizedDescriptor<TgFixed>).IsFalse();

        // T15 runtime: quoted record types stay on the legacy path until T20.
        await Assert.That(new RecordReadCore<TgQuoted>(FileHelperRegistry<TgQuoted>.Descriptor, TokensPath).Tokenized).IsFalse();
        await Assert.That(new RecordReadCore<TgRecycle>(FileHelperRegistry<TgRecycle>.Descriptor, TokensPath).Tokenized).IsTrue();
    }
}

[DelimitedRecord(",")]
public sealed class TgQuoted
{
    [FieldQuoted] public string A { get; set; } = string.Empty;
    public int B { get; set; }
}

[DelimitedRecord(",")]
public sealed class TgAlwaysQuoted
{
    [FieldQuoted(QuoteMode.AlwaysQuoted)] public string A { get; set; } = string.Empty;
    public int B { get; set; }
}

[DelimitedRecord(",")]
public sealed class TgFour
{
    public int A { get; set; }
    public int B { get; set; }
    public int C { get; set; }
    [FieldOptional] public int D { get; set; }
}

[DelimitedRecord(",")]
public sealed class TgTwoInts
{
    public int A { get; set; }
    public int B { get; set; }
}

/// <summary>Counts its conversions and upper-cases the value.</summary>
public sealed class CountingConverter : ConverterBase
{
    private static int s_calls;

    public static int Calls
    {
        get => Volatile.Read(ref s_calls);
        set => Volatile.Write(ref s_calls, value);
    }

    public override object? StringToField(string text)
    {
        Interlocked.Increment(ref s_calls);
        return text.ToUpperInvariant();
    }
}

[DelimitedRecord(",")]
public sealed class TgCounting
{
    [FieldConverter(typeof(CountingConverter))] public string A { get; set; } = string.Empty;
    [FieldQuoted(QuoteMode.AlwaysQuoted)] public string B { get; set; } = string.Empty;
}

[DelimitedRecord(",")]
public sealed class TgTrim
{
    [FieldTrim(TrimMode.Both, '*', '-', TrimGivenChars = true)] public string A { get; set; } = string.Empty;
    [FieldNotEmpty] public string B { get; set; } = string.Empty;
    [FieldTrim(TrimMode.Both)] public int C { get; set; }
}

[DelimitedRecord(",")]
public sealed class TgRecycle
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    [FieldOptional] public string? Note { get; set; }
    [FieldOptional] public int? Rank { get; set; }
}

[FixedLengthRecord]
public sealed class TgFixed
{
    [FieldFixedLength(3)] public string A { get; set; } = string.Empty;
}
