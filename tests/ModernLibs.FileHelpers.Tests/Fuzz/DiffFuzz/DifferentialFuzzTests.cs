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

using ModernLibs.FileHelpers.Tests.Compat;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Fuzz.DiffFuzz;

/// <summary>
/// Differential fuzzing: the same generated input goes through two read paths, and <see cref="ReadComparer"/>
/// reports the first difference. Part 1 compares the 4.9.1 descriptor of each <see cref="Compat49Matrix"/> shape
/// with the descriptor the current generator makes for the same record.
/// </summary>
[Category("Fuzz")]
public sealed class DifferentialFuzzTests
{
    [Test]
    public async Task Generator_Is_Deterministic_And_Covers_The_Shapes()
    {
        string a = new LineGenerator(42).NextFile(2000, 5), b = new LineGenerator(42).NextFile(2000, 5);
        await Assert.That(a).IsEqualTo(b);
        foreach (string needle in new[] { "\"\"", ",\"", "\",", " \"", "\" ", "\r\n", "\u00e9", ",," })
            await Assert.That(a.Contains(needle, StringComparison.Ordinal)).IsTrue();
        await Assert.That(new LineGenerator(43).NextFile(2000, 5)).IsNotEqualTo(a);
    }

    [Test]
    public async Task Comparer_Reports_A_Difference()
    {
        string? diff = ReadComparer.Compare("x", s => "1", s => "2");
        await Assert.That(diff).IsNotNull();
        await Assert.That(ReadComparer.Compare("x", s => "1", s => "1")).IsNull();
    }

    [Test]
    [MethodDataSource(typeof(Compat49Matrix), nameof(Compat49Matrix.All))]
    public async Task Snapshot_4_9_1_And_Current_Descriptor_Agree_On_Random_Input(
        string name, Func<string, string> readOld, Func<string, string> readNew,
        Func<string, string> _, Func<string, string> __)
    {
        var generator = new LineGenerator(seed: 4910);
        for (int i = 0; i < 2000; i++)
            await Assert.That(ReadComparer.Compare(generator.NextRecord(Compat49Inputs.FieldCount(name)), readOld, readNew)).IsNull();
    }

    /// <summary>
    /// The agreement above means something only if the generated input reaches the descriptor code. Counts the
    /// <c>record:</c> and <c>error:</c> lines of the array read of the 4.9.1 descriptor and prints them. Every
    /// shape must reach the error path. Records are not asserted: a shape-agnostic generator rarely makes a line
    /// whose every field converts, and some shapes cannot get one from a single comma-delimited record
    /// (<c>[IgnoreFirst]</c> skips it as the header, another delimiter, <c>[FieldInNewLine]</c>, an exact fixed
    /// width). The printed counts show the reach of each run.
    /// </summary>
    [Test]
    [MethodDataSource(typeof(Compat49Matrix), nameof(Compat49Matrix.All))]
    public async Task Random_Input_Reaches_The_Error_Path(
        string name, Func<string, string> readOld, Func<string, string> _,
        Func<string, string> __, Func<string, string> ___)
    {
        var generator = new LineGenerator(seed: 4910);
        int records = 0, errors = 0, inputsWithRecords = 0;
        for (int i = 0; i < 2000; i++)
        {
            string text = readOld(generator.NextRecord(Compat49Inputs.FieldCount(name)));
            int r = Count(text, "\nrecord:") + (text.StartsWith("record:", StringComparison.Ordinal) ? 1 : 0);
            records += r;
            errors += Count(text, "\nerror:") + (text.StartsWith("error:", StringComparison.Ordinal) ? 1 : 0);
            if (r > 0) inputsWithRecords++;
        }

        Console.WriteLine($"{name}: inputs with a record {inputsWithRecords}/2000, records {records}, errors {errors}");
        await Assert.That(errors).IsGreaterThan(0);
    }

    /// <summary>
    /// The loop of <see cref="Snapshot_4_9_1_And_Current_Descriptor_Agree_On_Random_Input"/> must see a real
    /// descriptor difference: <c>[FieldQuoted]</c> with <c>QuoteMode.AlwaysQuoted</c> against
    /// <c>QuoteMode.OptionalForRead</c> read some generated lines differently.
    /// </summary>
    [Test]
    public async Task The_Loop_Finds_A_Real_Descriptor_Difference()
    {
        var shapes = Compat49Matrix.All().ToDictionary(c => c.Name, StringComparer.Ordinal);
        var always = shapes["Compat49QuoteAlways"].ReadNew;
        var optional = shapes["Compat49QuoteOptionalRead"].ReadNew;

        var generator = new LineGenerator(seed: 4910);
        string? first = null;
        for (int i = 0; i < 2000 && first is null; i++)
            first = ReadComparer.Compare(generator.NextRecord(Compat49Inputs.FieldCount("Compat49QuoteAlways")), always, optional);

        await Assert.That(first).IsNotNull();
        await Assert.That(first!).Contains("--- A (returned text) ---");
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        for (int at = text.IndexOf(value, StringComparison.Ordinal); at >= 0; at = text.IndexOf(value, at + 1, StringComparison.Ordinal))
            count++;
        return count;
    }

    [Test]
    public async Task Comparer_Message_Holds_The_Input_Both_Results_And_The_First_Difference()
    {
        string? diff = ReadComparer.Compare("a,\"b\r\nc\"\u00e9", s => "same\nleft\ntail", s => "same\nright\ntail");

        await Assert.That(diff).IsNotNull();
        await Assert.That(diff!).Contains("\"a,\\\"b\\r\\nc\\\"\\u00E9\"");   // the input as a C# literal
        await Assert.That(diff).Contains("left");
        await Assert.That(diff).Contains("right");
        await Assert.That(diff).Contains("line 2, column 1");
    }

    [Test]
    public async Task Comparer_Does_Not_Mistake_A_Text_For_An_Exception_Or_A_Null()
    {
        // A read that returns the text a thrown exception is shown as must not equal the read that threw.
        string? textVsThrow = ReadComparer.Compare(
            "x",
            s => "EX:System.InvalidOperationException:boom",
            s => throw new InvalidOperationException("boom"));
        await Assert.That(textVsThrow).IsNotNull();

        string? nullVsText = ReadComparer.Compare("x", s => null!, s => "<null>");
        await Assert.That(nullVsText).IsNotNull();

        string? emptyVsNull = ReadComparer.Compare("x", s => string.Empty, s => null!);
        await Assert.That(emptyVsNull).IsNotNull();
    }

    [Test]
    public async Task Comparer_Compares_Exception_Type_Message_And_Inner_Exception()
    {
        await Assert.That(ReadComparer.Compare(
            "x", s => throw new InvalidOperationException("boom"), s => throw new InvalidOperationException("boom"))).IsNull();
        await Assert.That(ReadComparer.Compare(
            "x", s => throw new InvalidOperationException("boom"), s => throw new NotSupportedException("boom"))).IsNotNull();
        await Assert.That(ReadComparer.Compare(
            "x", s => throw new InvalidOperationException("boom"), s => throw new InvalidOperationException("bang"))).IsNotNull();
        await Assert.That(ReadComparer.Compare(
            "x",
            s => throw new InvalidOperationException("boom", new FormatException("a")),
            s => throw new InvalidOperationException("boom", new FormatException("b")))).IsNotNull();
    }

    [Test]
    public async Task Comparer_Passes_The_Same_Input_To_Both_Reads()
    {
        string? seenA = null, seenB = null;
        _ = ReadComparer.Compare("in\r\nput", s => seenA = s, s => seenB = s);
        await Assert.That(seenA).IsEqualTo("in\r\nput");
        await Assert.That(seenB).IsEqualTo("in\r\nput");
    }

    [Test]
    public async Task Generator_Record_Mix_Follows_The_Weights()
    {
        // 10000 records of 4 fields: empty line 2 %, one field short 5 %, one extra 5 %. Only records with no
        // quote char split on the delimiter by counting it, so the check uses those.
        var generator = new LineGenerator(7);
        int empty = 0, three = 0, five = 0, unquoted = 0, withLineBreak = 0;
        for (int i = 0; i < 10000; i++)
        {
            string record = generator.NextRecord(4);
            if (record.Length == 0) { empty++; continue; }
            if (record.Contains('\n', StringComparison.Ordinal) || record.Contains('\r', StringComparison.Ordinal)) withLineBreak++;
            if (record.Contains('"', StringComparison.Ordinal)) continue;
            unquoted++;
            int fields = record.Count(c => c == ',') + 1;
            if (fields == 3) three++;
            else if (fields == 5) five++;
        }

        await Assert.That(empty).IsBetween(120, 280);         // 2 % of 10000 = 200 (+ 1-field "short" records of 0 fields: none for 4)
        await Assert.That(unquoted).IsGreaterThan(1000);      // a field has no quote char 61 % of the time: 0.61^4 = 14 %
        await Assert.That(three * 100 / unquoted).IsBetween(2, 9);   // about 5 %
        await Assert.That(five * 100 / unquoted).IsBetween(2, 9);    // about 5 %
        await Assert.That(withLineBreak).IsGreaterThan(200);  // 3 % of fields carry a line break inside quotes
    }

    [Test]
    public async Task Generator_Uses_The_Given_Delimiter_And_Quote()
    {
        string file = new LineGenerator(42, ';', '\'').NextFile(500, 4);
        await Assert.That(file.Contains(";;", StringComparison.Ordinal)).IsTrue();
        await Assert.That(file.Contains("''", StringComparison.Ordinal)).IsTrue();
        await Assert.That(file.Contains(";'", StringComparison.Ordinal)).IsTrue();
        await Assert.That(file.Contains('"', StringComparison.Ordinal)).IsFalse();
        await Assert.That(file.Contains(',', StringComparison.Ordinal)).IsFalse();
    }
}
