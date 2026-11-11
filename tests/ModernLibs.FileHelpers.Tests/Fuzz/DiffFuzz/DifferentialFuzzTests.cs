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
    /// The one-record loop above reaches the slicing and error paths of every shape. Its reach into records is small
    /// and for some shapes zero (<c>[IgnoreFirst]</c> takes the only line as the header, another delimiter,
    /// <c>[FieldInNewLine]</c>, an exact fixed width); the shape-aware test below covers the record path. The
    /// <c>differential</c> target of the fuzz project prints the counts of both loops.
    /// </summary>
    [Test]
    [MethodDataSource(typeof(Compat49Matrix), nameof(Compat49Matrix.All))]
    public async Task Random_Input_Reaches_The_Error_Path(
        string name, Func<string, string> readOld, Func<string, string> _,
        Func<string, string> __, Func<string, string> ___)
    {
        var generator = new LineGenerator(seed: 4910);
        int fieldCount = Compat49Inputs.FieldCount(name);
        var result = DifferentialLoop.Run(2000, () => generator.NextRecord(fieldCount), readOld, readOld);

        await Assert.That(result.Difference).IsNull();
        await Assert.That(result.WithError).IsGreaterThan(0);
    }

    /// <summary>
    /// The 4.9.1 descriptor and the current one read the same shape-aware input alike (<see cref="ShapeInput"/>:
    /// valid rows of the shape around one valid row with one field replaced by a generated record). The input must
    /// reach the record path: the fuzzed row adds a record in some inputs and an error in others, against the
    /// valid rows alone.
    /// </summary>
    [Test]
    [MethodDataSource(typeof(Compat49Matrix), nameof(Compat49Matrix.All))]
    public async Task Snapshot_4_9_1_And_Current_Descriptor_Agree_On_Shape_Aware_Input(
        string name, Func<string, string> readOld, Func<string, string> readNew,
        Func<string, string> _, Func<string, string> __)
    {
        var generator = ShapeInput.CreateGenerator(name, seed: 4910);
        var result = DifferentialLoop.Run(2000, () => ShapeInput.Next(name, generator), readOld, readNew, ShapeInput.Baseline(name));

        await Assert.That(result.Difference).IsNull();
        await Assert.That(result.WithRecord).IsGreaterThan(0);
        await Assert.That(result.WithError).IsGreaterThan(0);
    }

    [Test]
    public async Task Shape_Input_Is_Deterministic_And_Keeps_The_Valid_Rows_Around_One_Fuzzed_Row()
    {
        foreach (string name in new[] { "Compat49Plain25", "Compat49Optional", "Compat49Struct", "Compat49FixedExact" })
        {
            var a = ShapeInput.CreateGenerator(name, 7);
            var b = ShapeInput.CreateGenerator(name, 7);
            string baseline = ShapeInput.Baseline(name);
            IReadOnlyList<string> rows = Compat49Inputs.ValidRows(name);
            string validBlock = string.Concat(rows.Select(r => r + "\r\n"));
            string header = Compat49Inputs.Header(name) is { } h ? h + "\r\n" : string.Empty;
            string footer = Compat49Inputs.Footer(name) is { } f ? f + "\r\n" : string.Empty;

            await Assert.That(baseline).IsEqualTo(header + validBlock + validBlock + footer);
            int changed = 0;
            for (int i = 0; i < 200; i++)
            {
                string input = ShapeInput.Next(name, a);
                await Assert.That(ShapeInput.Next(name, b)).IsEqualTo(input);
                await Assert.That(input.StartsWith(header + validBlock, StringComparison.Ordinal)).IsTrue();
                await Assert.That(input.EndsWith(validBlock + footer, StringComparison.Ordinal)).IsTrue();
                string fuzzed = input[(header + validBlock).Length..^(validBlock + footer).Length];
                await Assert.That(fuzzed.EndsWith("\r\n", StringComparison.Ordinal)).IsTrue();
                if (!rows.Contains(fuzzed[..^2], StringComparer.Ordinal))
                    changed++;
            }

            await Assert.That(changed).IsGreaterThan(100);    // most fuzzed rows differ from every valid row
        }
    }

    [Test]
    public async Task Shape_Input_Throws_For_An_Unknown_Shape_Name()
    {
        var generator = new LineGenerator(1);
        await Assert.That(() => ShapeInput.CreateGenerator("Compat49NoSuchShape", 1)).Throws<ArgumentException>()
            .WithMessageContaining("Compat49NoSuchShape", StringComparison.Ordinal);
        await Assert.That(() => ShapeInput.Baseline("Compat49NoSuchShape")).Throws<ArgumentException>()
            .WithMessageContaining("Compat49NoSuchShape", StringComparison.Ordinal);
        await Assert.That(() => ShapeInput.Next("Compat49NoSuchShape", generator)).Throws<ArgumentException>()
            .WithMessageContaining("Compat49NoSuchShape", StringComparison.Ordinal);
    }

    [Test]
    public async Task Shape_Input_Replaces_One_Field_Between_The_Shape_Separators()
    {
        // Struct: ';' fields, and the generator uses ';'. Fixed: the replaced slice is cut or padded to its width
        // two times out of three, so many fuzzed rows keep the 19 chars of a valid row.
        var structGenerator = ShapeInput.CreateGenerator("Compat49Struct", 11);
        int sameFieldCount = 0, fixedWidth = 0;
        var fixedGenerator = ShapeInput.CreateGenerator("Compat49FixedExact", 11);
        string fixedPrefix = string.Concat(Compat49Inputs.ValidRows("Compat49FixedExact").Select(r => r + "\r\n"));
        for (int i = 0; i < 300; i++)
        {
            string input = ShapeInput.Next("Compat49Struct", structGenerator);
            await Assert.That(input.Contains(',', StringComparison.Ordinal)).IsFalse();
            string row = input.Split("\r\n")[Compat49Inputs.ValidRows("Compat49Struct").Count];
            if (row.Count(c => c == ';') == 3)
                sameFieldCount++;

            string fixedInput = ShapeInput.Next("Compat49FixedExact", fixedGenerator);
            string fixedRow = fixedInput[fixedPrefix.Length..fixedInput.IndexOf("\r\n", fixedPrefix.Length, StringComparison.Ordinal)];
            if (fixedRow.Length == 19)
                fixedWidth++;
        }

        await Assert.That(sameFieldCount).IsGreaterThan(120);   // one of the 3 valid Struct rows is "   " (no ';')
        await Assert.That(fixedWidth).IsGreaterThan(140);
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

    [Test]
    public async Task Loop_Counts_Against_The_Baseline_And_Stops_At_The_First_Difference()
    {
        await Assert.That(DifferentialLoop.Count("record:a\nerror:1:x\nrecycled-record:a\nrecord:b\nthrown-throw:x\n"))
            .IsEqualTo((2, 1));

        string[] inputs = ["a", "b", "c", "d"];
        int at = 0;
        // A read: "record:" per char 'b' or 'd', "error:" for 'c'; baseline "a" has none. B differs at "d".
        string ReadA(string s) => s switch { "b" => "record:1\n", "c" => "error:1\n", "d" => "record:1\nrecord:2\n", _ => "header:\n" };
        string ReadB(string s) => string.Equals(s, "d", StringComparison.Ordinal) ? "different" : ReadA(s);

        var result = DifferentialLoop.Run(4, () => inputs[at++], ReadA, ReadB, baselineInput: "a");
        await Assert.That(result.Inputs).IsEqualTo(4);
        await Assert.That(result.Difference).IsNotNull();
        await Assert.That(result.WithRecord).IsEqualTo(1);    // "b" only: "d" is the difference and is not counted
        await Assert.That(result.WithError).IsEqualTo(1);
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
