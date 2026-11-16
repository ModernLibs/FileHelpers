// CA1031: the check reports any failure of a prototype (including an exception thrown by the engine or by the
// prototype itself) as exit code 1 instead of crashing with the runtime's own exit code.
#pragma warning disable CA1031

using ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;
using ModernLibs.FileHelpers.Benchmarks.Data;
#if !FILEHELPERS_BASELINE
using ModernLibs.FileHelpers.Parsing;
#endif

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Prototypes;

/// <summary>
/// <c>--check-prototypes</c>: the cross-path correctness checks of the component and prototype benchmarks (T10).
/// </summary>
/// <remarks>
/// The benchmarks run in-process, so a <c>[GlobalSetup]</c> that ran a second code path (the current one, to compare
/// the prototype with it) would leave that path's JIT profile, pooled buffers and garbage-collector state in the
/// measured process. Each benchmark's setup therefore checks only its own output against the dataset, and the
/// comparisons between paths live here: <see cref="Program"/> runs them and exits (0 when all agree, 1 otherwise)
/// before BenchmarkDotNet starts, like <c>--check-competitors</c>. A Step 0 round runs both checks before it measures.
/// </remarks>
internal static class PrototypeCheck
{
    private const int QuotedRows = 10_000;
    private const int PackageRows = 10_000;

    public static int Run()
    {
        string dir = Path.Combine(Path.GetTempPath(), "fh-prototype-check-" + Environment.ProcessId);
        Directory.CreateDirectory(dir);
        try
        {
            int rows = CheckR6(dir);
            Console.WriteLine($"// R6_PrecomputedEnds and R6_PrecomputedEnds_FastUnquote: {rows} records of the seeded quoted set equal R6_Current field by field.");
            int lines = CheckG2(dir);
            Console.WriteLine($"// G2_SingleSpan: WriteString of {PackageRows} records equals G2_Current ({lines} lines).");
            Console.WriteLine($"// Tokenizer_*: the four tokenizers give the same field ends on {CheckTokenizers(dir)} lines.");
            Console.WriteLine($"// Scan_SpanCreate: equals Scan_LoadUnsafe on {CheckScan(dir)} lines.");
#if !FILEHELPERS_BASELINE
            Console.WriteLine($"// Tokenizer_Runtime*, Parse_In and Parse_ByValue: {CheckRuntimeTokens(dir)} lines agree with the reference.");
            Console.WriteLine($"// TokenizerQuoted_*: every loop gives the reference entries on {CheckQuotedTokens(dir)} lines of the seeded quoted set.");
#endif
            Console.WriteLine($"// Unquote_Helper, UnquoteToString_Helper and CreateString_Helper: {CheckReadHelpers()} setups compared every string with the reference.");
            Console.WriteLine($"// Slice_Direct: equals Slice_MathMin (class and struct) on {CheckFixedSlices()} lines, and both throw on a line of the wrong width.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("// --check-prototypes failed: " + e);
            return 1;
        }
        finally
        {
            // A folder that cannot be deleted (a file still open) must not replace the result with an exception.
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"// --check-prototypes: could not delete {dir}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// The records of both R6 prototypes (gate and fast-unquote variants) equal R6_Current records, field by field,
    /// on the seeded quoted set.
    /// </summary>
    private static int CheckR6(string dir)
    {
        string path = DatasetGenerator.EnsureQuotedFieldsRandom(dir, QuotedRows);
        var current = R6UpperBoundBenchmarks.Collect(new FileHelperEngine<QuotedRecord>(), path);
        CompareR6("R6_PrecomputedEnds", current, new QuotedPrecomputedEndsEmittedUnquote(path), path);
        CompareR6("R6_PrecomputedEnds_FastUnquote", current, new QuotedPrecomputedEndsFastUnquote(path), path);
        return current.Count;
    }

    private static void CompareR6(string name, List<QuotedRecord> current, QuotedPrecomputedEndsDescriptor descriptor, string path)
    {
        var engine = PrototypeEngine.Create<QuotedRecord>(descriptor);
        descriptor.Rewind();
        var prototype = R6UpperBoundBenchmarks.Collect(engine, path);
        DataCheck.Equal(QuotedRows, descriptor.RowsParsed, name + " rows parsed");

        DataCheck.Equal(current.Count, prototype.Count, name + " record count");
        for (int i = 0; i < current.Count; i++)
        {
            QuotedRecord a = current[i], b = prototype[i];
            DataCheck.Equal(a.Id, b.Id, $"{name} row {i} Id");
            DataCheck.Equal(a.Name, b.Name, $"{name} row {i} Name");
            DataCheck.Equal(a.Description, b.Description, $"{name} row {i} Description");
            DataCheck.Equal(a.Notes, b.Notes, $"{name} row {i} Notes");
            DataCheck.Equal(a.Category, b.Category, $"{name} row {i} Category");
        }
    }

    /// <summary>G2_SingleSpan text equals G2_Current text for the same records.</summary>
    private static int CheckG2(string dir)
    {
        string path = DatasetGenerator.EnsurePackageAssets(dir, PackageRows);
        var (records, header) = G2PrototypeBenchmarks.Load(path);

        var current = new FileHelperEngine<PackageAsset> { HeaderText = header, NewLineForWrite = "\r\n" };
        var format = new PackageAssetSpanFormat();
        var prototype = PrototypeEngine.Create<PackageAsset>(format);
        prototype.HeaderText = header;
        prototype.NewLineForWrite = "\r\n";

        var expected = DataCheck.TextLines(current.WriteString(records));
        format.ResetCalls();
        var actual = DataCheck.TextLines(prototype.WriteString(records));
        DataCheck.Equal((long)records.Length, format.FormatCalls, "G2_SingleSpan records formatted by the prototype");
        DataCheck.Equal(expected.Length, actual.Length, "G2 written line count");
        for (int i = 0; i < expected.Length; i++)
        {
            if (!string.Equals(expected[i], actual[i], StringComparison.Ordinal))
                DataCheck.Equal(expected[i], actual[i], $"G2 written line {i}");
        }
        return expected.Length;
    }

    /// <summary>
    /// The four tokenizers on every line of the three datasets, then on seeded random lines with too few, the right
    /// number and too many fields: the same validity and, for valid lines, the same field ends.
    /// </summary>
    private static int CheckTokenizers(string dir)
    {
        int lines = 0;
        foreach (var dataset in Enum.GetValues<TokenizerDataset>())
        {
            int fieldCount = ComponentData.FieldCount(dataset);
            foreach (string line in ComponentData.Lines(dataset, dir))
            {
                CompareTokenizers(line, fieldCount, $"{dataset} line {lines}", mustBeValid: true);
                lines++;
            }
        }

        var random = new Random(410);
        for (int i = 0; i < 3_000; i++)
        {
            string line = RandomLine(random, ",a", commaShare: 0.15);
            int fields = line.AsSpan().Count(',') + 1;
            CompareTokenizers(line, fields, $"random line {i}", mustBeValid: true);
            CompareTokenizers(line, fields + 1, $"random line {i} (one field too few)", mustBeValid: false);
            if (fields > 1)
                CompareTokenizers(line, fields - 1, $"random line {i} (one field too many)", mustBeValid: false);
            lines++;
        }
        return lines;
    }

    private static void CompareTokenizers(string line, int fieldCount, string where, bool mustBeValid)
    {
        var expected = new int[fieldCount];
        int n = TokenizerBenchmarks.CountPlusIndexOf(line, expected, ',');
        DataCheck.Equal(mustBeValid, n == fieldCount, $"Tokenizer_CountPlusIndexOf validity, {where}");
        Compare("Tokenizer_SinglePassScalar", TokenizerBenchmarks.SinglePassScalar);
        Compare("Tokenizer_SinglePassVector", TokenizerBenchmarks.SinglePassVector);
        Compare("Tokenizer_SinglePassVector_AnyMatchFirst", TokenizerBenchmarks.SinglePassVectorAnyMatchFirst);

        void Compare(string name, TokenizerBenchmarks.Tokenizer tokenizer)
        {
            var ends = new int[fieldCount];
            int count = tokenizer(line, ends, ',');
            DataCheck.Equal(n == fieldCount, count == fieldCount, $"{name} validity, {where}");
            if (count != fieldCount)
                return;
            for (int k = 0; k < fieldCount; k++)
                DataCheck.Equal(expected[k], ends[k], $"{name} end of field {k}, {where}");
        }
    }

#if !FILEHELPERS_BASELINE
    /// <summary>
    /// The calls of the shipped plain tokenizer that <see cref="TokenizerBenchmarks"/> makes: the <c>bool vectors</c>
    /// overload both ways (T15) and the loop overload with every <see cref="TokenizerLoop"/> and load form (T21). A loop
    /// this machine does not accelerate runs in software here: slower, same result.
    /// </summary>
    private static readonly (string Name, Func<string, int[], int> Tokenize)[] s_runtimeTokenizers = RuntimeTokenizers();

    private static (string Name, Func<string, int[], int> Tokenize)[] RuntimeTokenizers()
    {
        var tokenizers = new List<(string, Func<string, int[], int>)>
        {
            ("vectors true", (line, ends) => RecordTokenizer.TokenizePlain(line, ',', ends, vectors: true)),
            ("vectors false", (line, ends) => RecordTokenizer.TokenizePlain(line, ',', ends, vectors: false)),
        };
        foreach (var loop in Enum.GetValues<TokenizerLoop>())
        {
            foreach (bool unsafeLoad in new[] { false, true })
                tokenizers.Add(($"{loop}, unsafeLoad {unsafeLoad}", (line, ends) => RecordTokenizer.TokenizePlain(line, ',', ends, loop, unsafeLoad)));
        }
        return tokenizers.ToArray();
    }

    /// <summary>
    /// The shipped tokenizer (T15) with every call of <see cref="RuntimeTokenizers"/> gives the ends of
    /// <see cref="TokenizerBenchmarks.CountPlusIndexOf"/> on every line of the four datasets, and the field count and
    /// ends of <see cref="string.Split(char)"/> on seeded random lines at three capacities;
    /// <see cref="TokensPassingBenchmarks"/> gives the engine's records on both interfaces.
    /// </summary>
    private static int CheckRuntimeTokens(string dir)
    {
        int lines = 0;
        foreach (var dataset in Enum.GetValues<TokenizerDataset>())
        {
            int fieldCount = ComponentData.FieldCount(dataset);
            foreach (string line in ComponentData.Lines(dataset, dir))
            {
                var expected = new int[fieldCount];
                DataCheck.Equal(fieldCount, TokenizerBenchmarks.CountPlusIndexOf(line, expected, ','), $"{dataset} line {lines} reference");
                foreach (var (name, tokenize) in s_runtimeTokenizers)
                {
                    var ends = new int[fieldCount + 1];
                    DataCheck.Equal(fieldCount, tokenize(line, ends), $"Tokenizer_Runtime ({name}) count, {dataset} line {lines}");
                    for (int k = 0; k < fieldCount; k++)
                        DataCheck.Equal(expected[k], ends[k], $"Tokenizer_Runtime ({name}) end of field {k}, {dataset} line {lines}");
                }
                lines++;
            }
        }

        var random = new Random(4915);
        for (int i = 0; i < 3_000; i++)
        {
            string line = RandomLine(random, ",a\u012C", commaShare: 0.15);
            string[] parts = line.Split(',');
            foreach (int capacity in new[] { parts.Length + 1, parts.Length, Math.Max(1, parts.Length / 2) })
            {
                int count = Math.Min(parts.Length, capacity);
                foreach (var (name, tokenize) in s_runtimeTokenizers)
                {
                    var ends = new int[capacity];
                    DataCheck.Equal(count, tokenize(line, ends), $"Tokenizer_Runtime ({name}) count, random line {i}, capacity {capacity}");
                    int end = -1;
                    for (int k = 0; k < count; k++)
                    {
                        end += parts[k].Length + 1;
                        DataCheck.Equal(end, ends[k], $"Tokenizer_Runtime ({name}) end of field {k}, random line {i}, capacity {capacity}");
                    }
                }
            }
            lines++;
        }

        string path = DatasetGenerator.EnsurePackageAssets(dir, PackageRows);
        var engine = new FileHelperEngine<PackageAsset>().ReadFile(path);
        var (byIn, byValue) = TokensPassingBenchmarks.Collect(DataCheck.DataLines(path, hasHeader: true));
        DataCheck.Equal(engine.Length, byIn.Count, "Parse_In record count");
        DataCheck.Equal(engine.Length, byValue.Count, "Parse_ByValue record count");
        var properties = typeof(PackageAsset).GetProperties();
        for (int i = 0; i < engine.Length; i++)
        {
            foreach (var property in properties)
            {
                object? expected = property.GetValue(engine[i]);
                DataCheck.Equal(expected, property.GetValue(byIn[i]), $"Parse_In row {i} {property.Name}");
                DataCheck.Equal(expected, property.GetValue(byValue[i]), $"Parse_ByValue row {i} {property.Name}");
            }
            lines++;
        }
        return lines;
    }

    /// <summary>
    /// The quoted tokenizer (T20) with the loops of <see cref="QuotedTokenizerBenchmarks"/> (T21) gives the entries of
    /// <see cref="QuotedTokenizerBenchmarks.ExpectedEntry"/> for every field of every line of the seeded quoted set.
    /// </summary>
    private static int CheckQuotedTokens(string dir)
    {
        int lines = 0;
        foreach (string line in QuotedTokenizerBenchmarks.Lines(dir))
        {
            foreach (var loop in new[] { TokenizerLoop.IndexOfHop, TokenizerLoop.Vector128, TokenizerLoop.Vector256 })
            {
                var buffer = new TokenBuffer(QuotedTokenizerBenchmarks.FieldCount);
                int count = RecordTokenizer.TokenizeQuoted(line, ',', '"', QuotedTokenizerBenchmarks.Quotes, buffer, loop, out _, out _);
                DataCheck.Equal(QuotedTokenizerBenchmarks.FieldCount, count, $"TokenizerQuoted_{loop} count, quoted line {lines}");
                for (int k = 0; k < count; k++)
                    DataCheck.Equal(QuotedTokenizerBenchmarks.ExpectedEntry(line, k), buffer.Ends[k], $"TokenizerQuoted_{loop} entry {k}, quoted line {lines}");
            }
            lines++;
        }
        return lines;
    }
#endif

    /// <summary>
    /// The shipped read helpers of <see cref="RecordParsing"/> give the reference strings: each benchmark setup checks
    /// every value, so running the setups for every parameter value is the check.
    /// </summary>
    private static int CheckReadHelpers()
    {
#if FILEHELPERS_BASELINE
        return 0; // the helpers are 4.10 API: a baseline build (4.9.1 package or source) has none to check
#else
        int setups = 0;
        foreach (var values in Enum.GetValues<UnquoteSet>())
        {
            var unquote = new UnquoteBenchmarks { Values = values };
            unquote.SetupHelper();
            unquote.SetupToStringHelper();
            setups += 2;
        }
        foreach (var dataset in Enum.GetValues<StringFieldDataset>())
        {
            new CreateStringBenchmarks { Dataset = dataset }.SetupHelper();
            setups++;
        }
        return setups;
#endif
    }

    /// <summary>
    /// Slice_Direct and Slice_MathMin give the same twelve values, as class and as struct, on every seeded line; both
    /// shapes throw on a line that is too short or too long.
    /// </summary>
    private static int CheckFixedSlices()
    {
        string[] lines = FixedSliceBenchmarks.MakeLines();
        foreach (var record in Enum.GetValues<FixedSliceRecord>())
        {
            for (int i = 0; i < lines.Length; i++)
            {
                int[] expected = FixedSliceBenchmarks.ParseMathMin(record, lines[i]);
                int[] actual = FixedSliceBenchmarks.ParseDirect(record, lines[i]);
                DataCheck.That(expected.AsSpan().SequenceEqual(actual), $"Slice_Direct ({record}) line {i} differs from Slice_MathMin");
            }

            foreach (string wrong in new[] { lines[0][..^1], lines[0] + "X" })
            {
                AssertThrows(() => FixedSliceBenchmarks.ParseMathMin(record, wrong), "Slice_MathMin", record, wrong.Length);
                AssertThrows(() => FixedSliceBenchmarks.ParseDirect(record, wrong), "Slice_Direct", record, wrong.Length);
            }
        }
        return lines.Length;
    }

    private static void AssertThrows(Action parse, string name, FixedSliceRecord record, int length)
    {
        bool threw = false;
        try
        {
            parse();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        DataCheck.That(threw, $"{name} ({record}) accepted a line of {length} characters");
    }

    /// <summary>Scan_SpanCreate and Scan_LoadUnsafe give the same result on every line of every dataset and on random lines.</summary>
    private static int CheckScan(string dir)
    {
        int lines = 0;
        var sources = new List<string>(DataCheck.DataLines(DatasetGenerator.EnsureQuotedFieldsRandom(dir, QuotedRows), hasHeader: true));
        foreach (var dataset in Enum.GetValues<TokenizerDataset>())
            sources.AddRange(ComponentData.Lines(dataset, dir));
        var random = new Random(410);
        for (int i = 0; i < 3_000; i++)
            sources.Add(RandomLine(random, ",\"a", commaShare: 0.3));

        foreach (string line in sources)
        {
            long expected = VectorLoopShapeBenchmarks.ScanLoadUnsafe(line, ',', '"');
            long actual = VectorLoopShapeBenchmarks.ScanSpanCreate(line, ',', '"');
            DataCheck.Equal(expected, actual, $"Scan_SpanCreate line {lines} ('{line}')");
            DataCheck.Equal(VectorLoopShapeBenchmarks.ScanReference(line, ',', '"'), expected, $"Scan_LoadUnsafe line {lines} ('{line}')");
            lines++;
        }
        return lines;
    }

    /// <summary>A line of 0 to 100 characters: <paramref name="alphabet"/>[0] with probability <paramref name="commaShare"/>, otherwise the other characters.</summary>
    private static string RandomLine(Random random, string alphabet, double commaShare)
    {
        var chars = new char[random.Next(0, 101)];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = random.NextDouble() < commaShare ? alphabet[0] : alphabet[random.Next(1, alphabet.Length)];
        return new string(chars);
    }
}
