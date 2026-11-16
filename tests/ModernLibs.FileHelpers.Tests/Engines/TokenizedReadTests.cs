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
// MLFH001: Compat49Plain25 is a 4.9.1 snapshot type without record attributes on purpose (see Generated49).
#pragma warning disable MLFH001

using System.Globalization;
using System.Reflection;
using System.Text;
using ModernLibs.FileHelpers.Tests.Compat;
using ModernLibs.FileHelpers.Tests.Engines.Fixtures;
using ModernLibs.FileHelpers.Tests.Fuzz.DiffFuzz;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// The tokenized read path of 4.10 (G1, design 4.5) on the runtime side: which record types it takes, that it calls
/// <see cref="ITokenizedDescriptor{T}"/> with the right tokens, and that every engine reads the same records and
/// errors on it as on the legacy path (<see cref="EnginePaths.LegacyParse"/>). The tokenized descriptors are test
/// fixtures here (<see cref="TokenCheckingDescriptor{T}"/>, <see cref="TokPlainDescriptor"/>); the generator emits
/// the interface from T17 on.
/// </summary>
[Category("Engines")]
public sealed class TokenizedReadTests
{
    private static readonly EnginePaths TokensPath = new(LegacyParse: false, ScalarTokenizer: false, LegacyWriter: false);
    private static readonly EnginePaths LegacyPath = new(LegacyParse: true, ScalarTokenizer: false, LegacyWriter: false);
    private static readonly EnginePaths ScalarPath = new(LegacyParse: false, ScalarTokenizer: true, LegacyWriter: false);

    private static FileHelperEngine<T> Engine<T>(IRecordDescriptor<T> descriptor, EnginePaths paths) => new(descriptor, Encoding.UTF8, paths);

    /// <summary><paramref name="count"/> rows of <see cref="TokPlain"/> with 3, 4 and 5 fields, each followed by CRLF.</summary>
    private static string Rows(int count)
    {
        var text = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            text.Append(CultureInfo.InvariantCulture, $"{i},N{i},{i}.5");
            if (i % 3 != 0)
                text.Append(CultureInfo.InvariantCulture, $",note{i}");
            if (i % 3 == 2)
                text.Append(CultureInfo.InvariantCulture, $",{i % 100}");
            text.Append("\r\n");
        }

        return text.ToString();
    }

    private static string Show(TokPlain r) =>
        string.Create(CultureInfo.InvariantCulture, $"{r.Id}|{r.Name}|{r.Amount}|{r.Note ?? "<null>"}|{(r.Rank is { } rank ? rank.ToString(CultureInfo.InvariantCulture) : "<null>")}");

    private static string Show(TokStrings r) => $"{r.A}|{r.B}|{r.C ?? "<null>"}|{r.D ?? "<null>"}|{r.E ?? "<null>"}";

    private static string Show(TokMaster r) => string.Create(CultureInfo.InvariantCulture, $"M {r.Kind}|{r.Id}|{r.Name}");

    private static string Show(TokDetail r) => string.Create(CultureInfo.InvariantCulture, $"D {r.Kind}|{r.Qty}|{r.Text ?? "<null>"}");

    /// <summary>The records and the errors of an array read with <see cref="ErrorMode.SaveAndContinue"/>.</summary>
    private static string Text<T>(FileHelperEngine<T> engine, string input, Func<T, string> show)
    {
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        var text = new StringBuilder();
        foreach (var record in engine.ReadString(input))
            text.Append("record:").Append(show(record)).Append('\n');
        foreach (var error in engine.ErrorManager.Errors)
        {
            text.Append(CultureInfo.InvariantCulture, $"error:{error.LineNumber}:{error.ExceptionInfo?.GetType().Name}:{error.ExceptionInfo?.Message}:{error.RecordString}")
                .Append('\n');
        }

        return text.ToString();
    }

    // ---- Compat49 shapes ---------------------------------------------------------------------------------------

    /// <summary>
    /// The <see cref="Compat49Matrix"/> shapes the tokenized path of 4.10.0 takes: one-char delimiter, one line per
    /// record, no quoted field, no <c>[FieldDelimiter]</c> (decided from the current descriptor of each shape).
    /// </summary>
    public static IEnumerable<string> PlainShapes() =>
        Compat49Matrix.All().Select(c => c.Name).Where(name => (bool)Invoke(nameof(IsPlain), name)!).ToList();

    private static object? Invoke(string method, string shape, params object?[] arguments)
    {
        var twin = typeof(Compat49Matrix).Assembly.GetType(typeof(Compat49Matrix).Namespace + "." + shape + "Twin", throwOnError: true)!;
        var generic = typeof(TokenizedReadTests).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!;
        return generic.MakeGenericMethod(twin).Invoke(null, arguments);
    }

    private static bool IsPlain<T>()
    {
        var descriptor = FileHelperRegistry<T>.Descriptor;
        var options = descriptor.Options;
        if (options.Delimiter is not { Length: 1 } || options.LineCountPerLogicalRecord > 1 || options.QuoteChar != '\0')
            return false;
        foreach (ref readonly var field in descriptor.Fields)
        {
            if (field.QuoteChar != '\0' || field.Delimiter is { Length: > 0 } || field.IsInNewLine)
                return false;
        }

        return true;
    }

    /// <summary>
    /// <see cref="DifferentialLoop"/> over <see cref="ShapeInput"/> of <paramref name="name"/>: A reads with the checking
    /// descriptor on <paramref name="paths"/>, B with the same descriptor on the legacy path.
    /// </summary>
    private static (DifferentialLoop.Result Result, bool Tokenized, int TryParseCalls) RunShape<T>(string name, EnginePaths paths)
    {
        var checking = TokenChecking.For<T>(FileHelperRegistry<T>.Descriptor.Options.Delimiter![0]);
        bool tokenized = new RecordReadCore<T>(checking, paths).Tokenized;
        var generator = ShapeInput.CreateGenerator(name, seed: 4910);
        var result = DifferentialLoop.Run(
            2000,
            () => ShapeInput.Next(name, generator),
            input => Compat49Matrix.Read(input, () => Engine(checking, paths)),
            input => Compat49Matrix.Read(input, () => Engine(checking, LegacyPath)),
            ShapeInput.Baseline(name));
        return (result, tokenized, checking.TryParseCalls);
    }

    [Test]
    [MethodDataSource(nameof(PlainShapes))]
    public async Task Tokenized_And_Legacy_Agree_On_Shape_Input(string name)
    {
        await Assert.That(PlainShapes()).Contains("Compat49Plain25");

        var (result, tokenized, tryParseCalls) = ((DifferentialLoop.Result, bool, int))Invoke(nameof(RunShape), name, name, TokensPath)!;
        await Assert.That(tokenized).IsTrue();
        await Assert.That(result.Difference).IsNull();
        await Assert.That(result.WithRecord).IsGreaterThan(0);
        await Assert.That(result.WithError).IsGreaterThan(0);
        await Assert.That(tryParseCalls).IsGreaterThan(0);
    }

    [Test]
    public async Task ScalarTokenizer_Option_Reads_The_Same_Records()
    {
        var (result, tokenized, tryParseCalls) = RunShape<Compat49Plain25Twin>("Compat49Plain25", ScalarPath);
        await Assert.That(tokenized).IsTrue();
        await Assert.That(result.Difference).IsNull();
        await Assert.That(tryParseCalls).IsGreaterThan(0);

        var descriptor = new TokPlainDescriptor();
        string input = Rows(300);
        await Assert.That(Text(Engine(descriptor, ScalarPath), input, Show)).IsEqualTo(Text(Engine(descriptor, LegacyPath), input, Show));
        await Assert.That(descriptor.TryParseCalls).IsEqualTo(300);
    }

    [Test]
    public async Task Old_Descriptor_Takes_The_Old_Path()
    {
        const string name = "Compat49Plain25";
        var old = FileHelperRegistry<Compat49Plain25>.Descriptor;
        var core = new RecordReadCore<Compat49Plain25>(old, TokensPath);
        await Assert.That(core.Tokenized).IsFalse();
        await Assert.That(core.CreateTokenBuffer()).IsNull();

        int index = 0;
        foreach (string input in Compat49Inputs.For(name))
        {
            string read = Compat49Matrix.Read(input, () => Engine(old, TokensPath));
            await Assert.That(read).IsEqualTo(Compat49Golden.Expected(name, index, "read"))
                .Because($"{name} input {index}: the 4.9.1 descriptor on the 4.10 default path against Golden49");
            index++;
        }

        await Assert.That(index).IsGreaterThan(0);
    }

    // ---- Calls and options -------------------------------------------------------------------------------------

    private static string ParseMessage<T>(RecordReadCore<T> core, string record)
    {
        try
        {
            core.Parse(record, recycle: false, default, core.CreateTokenBuffer());
            return "<no exception>";
        }
        catch (FileHelpersException ex)
        {
            return ex.Message;
        }
    }

    [Test]
    [Arguments("a,b")]
    [Arguments("1,A,2,x,3,extra")]
    public async Task Field_Count_Errors_Are_The_Legacy_Text(string input)
    {
        var descriptor = new TokPlainDescriptor();
        string Read(EnginePaths paths) => string.Join("\n", Engine(descriptor, paths).ReadString(input).Select(Show));

        await Assert.That(ReadComparer.Compare(input, _ => Read(TokensPath), _ => Read(LegacyPath))).IsNull();
        await Assert.That(ReadComparer.Compare(input, _ => Read(TokensPath), _ => string.Join("\n", new FileHelperEngine<TokPlain>().ReadString(input).Select(Show)))).IsNull();

        var core = new RecordReadCore<TokPlain>(descriptor, TokensPath);
        await Assert.That(core.Tokenized).IsTrue();
        await Assert.That(ParseMessage(core, input)).StartsWith("Expected 3 to 5 fields but found");
        await Assert.That(descriptor.TryParseCalls).IsEqualTo(0);
    }

    [Test]
    public async Task LegacyParse_Option_Never_Calls_TryParse()
    {
        var checking = TokenChecking.For<TokPlain>(',');
        var core = new RecordReadCore<TokPlain>(checking, LegacyPath);
        await Assert.That(core.Tokenized).IsFalse();
        await Assert.That(core.CreateTokenBuffer()).IsNull();

        var rows = Engine(checking, LegacyPath).ReadString(Rows(100));
        await Assert.That(rows.Length).IsEqualTo(100);
        await Assert.That(checking.TryParseCalls).IsEqualTo(0);
        await Assert.That(checking.ParseCalls).IsEqualTo(100);
    }

    [Test]
    public async Task Default_Path_Calls_TryParse_Once_Per_Record()
    {
        var descriptor = new TokPlainDescriptor();
        string input = Rows(1000);
        string tokenized = Text(Engine(descriptor, TokensPath), input, Show);

        await Assert.That(descriptor.TryParseCalls).IsEqualTo(1000);
        await Assert.That(descriptor.ParseCalls).IsEqualTo(0);
        await Assert.That(tokenized).IsEqualTo(Text(new FileHelperEngine<TokPlain>(), input, Show));
    }

    private static bool Tokenized<T>(char delimiter)
    {
        var core = new RecordReadCore<T>(TokenChecking.For<T>(delimiter), TokensPath);
        return core.Tokenized || core.CreateTokenBuffer() is not null;
    }

    [Test]
    public async Task Ineligible_Layouts_Are_Not_Tokenized()
    {
        await Assert.That(Tokenized<TokPlain>(',')).IsTrue();
        await Assert.That(Tokenized<TokStrings>(',')).IsTrue();

        await Assert.That(Tokenized<TokQuoted>(',')).IsFalse();
        await Assert.That(Tokenized<TokTwoCharDelimiter>(':')).IsFalse();
        await Assert.That(Tokenized<TokFieldDelimiter>(',')).IsFalse();
        await Assert.That(Tokenized<TokNewLine>(',')).IsFalse();
        await Assert.That(Tokenized<TokFixed>(',')).IsFalse();

        // The registered (4.10 generated, not yet tokenized) descriptor.
        var core = new RecordReadCore<TokPlain>(FileHelperRegistry<TokPlain>.Descriptor, TokensPath);
        await Assert.That(core.Tokenized).IsFalse();
    }

    [Test]
    public async Task BeforeReadRecord_Replacement_Is_Tokenized_Again()
    {
        var checking = TokenChecking.For<TokPlain>(',');
        var engine = Engine(checking, TokensPath);
        engine.BeforeReadRecord += (_, e) =>
        {
            if (string.Equals(e.RecordLine, "1,A,2", StringComparison.Ordinal))
                e.RecordLine = "3,B,4";
        };

        var rows = engine.ReadString("1,A,2\r\n");
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Id).IsEqualTo(3);
        await Assert.That(rows[0].Name).IsEqualTo("B");
        await Assert.That(rows[0].Amount).IsEqualTo(4m);
        await Assert.That(checking.TryParseCalls).IsEqualTo(1);
        await Assert.That(checking.ParseCalls).IsEqualTo(0);
    }

    /// <summary>
    /// Review Focus 4: a recycling callback read where the second record has fewer optional fields than the first.
    /// The second callback sees the values of the legacy path, not those of the first record.
    /// </summary>
    [Test]
    public async Task Recycling_Read_Overwrites_Every_Member()
    {
        const string input = "1,A,2.5,note,7\r\n2,B,3.5\r\n";
        static (List<string> Seen, int TryParseIntoCalls) Read(EnginePaths paths)
        {
            var checking = (RecyclableTokenCheckingDescriptor<TokPlain>)TokenChecking.For<TokPlain>(',');
            var engine = Engine(checking, paths);
            engine.RecycleRecords = true;
            var seen = new List<string>();
            TokPlain? first = null;
            engine.ReadString(input, r =>
            {
                seen.Add(Show(r));
                first ??= r;
                if (!ReferenceEquals(first, r))
                    seen.Add("<not recycled>");
            });
            return (seen, checking.TryParseIntoCalls);
        }

        var (tokenized, tryParseIntoCalls) = Read(TokensPath);
        var (legacy, _) = Read(LegacyPath);
        await Assert.That(tokenized).IsEquivalentTo(legacy);
        await Assert.That(tokenized.Count).IsEqualTo(2);
        await Assert.That(tokenized[1]).IsEqualTo("2|B|3.5||<null>");     // the legacy values: no "note", no 7 from the first record
        await Assert.That(tryParseIntoCalls).IsGreaterThan(0);
    }

    [Test]
    public async Task Recycle_Falls_Back_When_TryParseInto_Returns_False()
    {
        string input = Rows(50);
        var registry = (IRecyclableDescriptor<TokPlain>)FileHelperRegistry<TokPlain>.Descriptor;
        var descriptor = new NoTryParseIntoDescriptor<TokPlain>(registry, ',');

        static List<string> Read(IRecordDescriptor<TokPlain> descriptor, EnginePaths paths, string input)
        {
            var engine = Engine(descriptor, paths);
            engine.RecycleRecords = true;
            var seen = new List<string>();
            engine.ReadString(input, r => seen.Add(Show(r)));
            return seen;
        }

        var tokenized = Read(descriptor, TokensPath, input);
        await Assert.That(tokenized).IsEquivalentTo(Read(registry, LegacyPath, input));
        await Assert.That(tokenized.Count).IsEqualTo(50);
        await Assert.That(descriptor.TryParseCalls).IsEqualTo(1);       // the first record: no recycled instance yet
        await Assert.That(descriptor.ParseIntoCalls).IsEqualTo(49);     // every later one: TryParseInto said "not handled"
        await Assert.That(descriptor.ParseCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Parallel_Read_Shares_One_Core_And_Reads_The_Same_Records()
    {
        const int rows = 50_000;
        var checking = TokenChecking.For<TokPlain>(',');
        var engine = Engine(checking, TokensPath);
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Rows(rows));
            var parallel = engine.ParallelReadFile(path, chunkSize: 65_536).Select(Show).ToList();
            var sequential = engine.ReadFile(path).Select(Show).ToList();

            await Assert.That(engine.Core.Tokenized).IsTrue();
            await Assert.That(parallel.Count).IsEqualTo(rows);
            await Assert.That(parallel).IsEquivalentTo(sequential);
            await Assert.That(checking.TryParseCalls).IsEqualTo(2 * rows);
            await Assert.That(checking.ParseCalls).IsEqualTo(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// F14: at T15 no generated descriptor implements <see cref="ITokenizedDescriptor{T}"/>, so both paths are the
    /// legacy one here; the test pins the internal entries. T17 adds the <c>Tokenized == true</c> assertions.
    /// </summary>
    [Test]
    public async Task MultiRecord_And_MasterDetail_Read_The_Same_Records_On_Both_Paths()
    {
        const string input = "M,1,first\r\nD,2,a\r\nD,3\r\nM,4,second\r\nD,5,b\r\nM,x,bad\r\nD,6\r\n";

        static string ReadMulti(EnginePaths paths)
        {
            var master = MultiRecordType.For<TokMaster>(line => !line.IsEmpty && line[0] == 'M', paths);
            var detail = MultiRecordType.For<TokDetail>(line => !line.IsEmpty && line[0] == 'D', paths);
            var engine = new MultiRecordEngine(master, detail) { ErrorMode = ErrorMode.SaveAndContinue };
            var text = new StringBuilder();
            text.Append(((MultiRecordType<TokMaster>)master).Core.Tokenized).Append(((MultiRecordType<TokDetail>)detail).Core.Tokenized).Append('\n');
            foreach (object record in engine.ReadString(input))
                text.Append(record switch { TokMaster m => Show(m), TokDetail d => Show(d), _ => "?" }).Append('\n');
            foreach (var error in engine.ErrorManager.Errors)
                text.Append(CultureInfo.InvariantCulture, $"error:{error.LineNumber}:{error.ExceptionInfo?.Message}").Append('\n');
            return text.ToString();
        }

        static string ReadMasterDetail(EnginePaths paths)
        {
            var engine = new MasterDetailEngine<TokMaster, TokDetail>(
                line => !line.IsEmpty && line[0] == 'M' ? RecordAction.Master : RecordAction.Detail, paths)
            {
                ErrorMode = ErrorMode.SaveAndContinue,
            };
            var text = new StringBuilder();
            text.Append(engine.MasterCore.Tokenized).Append(engine.DetailCore.Tokenized).Append('\n');
            foreach (var group in engine.ReadString(input))
            {
                text.Append(Show(group.Master)).Append('\n');
                foreach (var detail in group.Details)
                    text.Append("  ").Append(Show(detail)).Append('\n');
            }

            foreach (var error in engine.ErrorManager.Errors)
                text.Append(CultureInfo.InvariantCulture, $"error:{error.LineNumber}:{error.ExceptionInfo?.Message}").Append('\n');
            return text.ToString();
        }

        string multi = ReadMulti(TokensPath);
        await Assert.That(multi).IsEqualTo(ReadMulti(LegacyPath));
        await Assert.That(multi).Contains("M M|4|second");
        await Assert.That(multi).Contains("error:6:");

        string masterDetail = ReadMasterDetail(TokensPath);
        await Assert.That(masterDetail).IsEqualTo(ReadMasterDetail(LegacyPath));
        await Assert.That(masterDetail).Contains("  D D|5|b");

        await using var asyncEngine = new MasterDetailAsyncEngine<TokMaster, TokDetail>(_ => RecordAction.Master, LegacyPath);
        await Assert.That(asyncEngine.MasterCore.Tokenized).IsFalse();
        await Assert.That(asyncEngine.DetailCore.Tokenized).IsFalse();
    }

    [Test]
    [Arguments(",,,,")]
    [Arguments("")]
    public async Task Record_With_Only_Delimiters_And_Empty_Record(string record)
    {
        string input = record + "\r\n";

        var strings = TokenChecking.For<TokStrings>(',');
        await Assert.That(Text(Engine(strings, TokensPath), input, Show)).IsEqualTo(Text(Engine(strings, LegacyPath), input, Show));

        var plain = TokenChecking.For<TokPlain>(',');
        await Assert.That(Text(Engine(plain, TokensPath), input, Show)).IsEqualTo(Text(Engine(plain, LegacyPath), input, Show));

        // ",,,," has 5 fields: tokenized (and checked); "" has 1, below the 2 (3) mandatory fields: the legacy error.
        int expected = record.Length == 0 ? 0 : 1;
        await Assert.That(strings.TryParseCalls).IsEqualTo(expected);
        await Assert.That(plain.TryParseCalls).IsEqualTo(expected);
    }
}
