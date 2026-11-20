using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using SharpFuzz;
using ModernLibs.FileHelpers.Parsing;
using ModernLibs.FileHelpers.Converters;
using ModernLibs.FileHelpers.Tests.Compat;
using ModernLibs.FileHelpers.Tests.Engines.Fixtures;
using ModernLibs.FileHelpers.Tests.Fuzz.DiffFuzz;

namespace ModernLibs.FileHelpers.Fuzz.Tests;

public static class Program
{
    public static int Main(string[] args)
    {
        // Target "paths-switch" (Review Focus 8): the switches are set before any code reads them. RuntimeSwitches
        // reads them once, when the class is first used, so these are the first statements, and Main itself names no
        // library type: everything else runs in RunTarget, which the JIT compiles after these calls.
        if (args.Length > 0 && string.Equals(args[0], PathsSwitchTarget, StringComparison.OrdinalIgnoreCase))
        {
            AppContext.SetSwitch("ModernLibs.FileHelpers.DisableTokenizedParse", true);
            AppContext.SetSwitch("ModernLibs.FileHelpers.DisableVectorizedTokenizer", true);
            AppContext.SetSwitch("ModernLibs.FileHelpers.DisableRecordWriter", true);
        }

        return RunTarget(args);
    }

    private const string PathsSwitchTarget = "paths-switch";

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunTarget(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: ModernLibs.FileHelpers.Fuzz.Tests <target>");
            Console.WriteLine("Targets: delimited, spanline, fixedlength, converters, extended [iterations],");
            Console.WriteLine("  differential <iterations> [seed] [--shape NAME], paths <iterations> [seed] [--shape NAME],");
            Console.WriteLine("  paths-switch <iterations>, --list-shapes");
            return 0;
        }

        var target = args[0].ToLowerInvariant();
        if (string.Equals(target, "--list-shapes", StringComparison.Ordinal))
        {
            foreach (var shape in Compat49Matrix.All())
                Console.WriteLine(shape.Name);
            return 0;
        }

        if (string.Equals(target, "extended", StringComparison.Ordinal))
        {
            int iterations = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 10000;
            return ExtendedFuzzRunner.Run(iterations);
        }

        if (string.Equals(target, "differential", StringComparison.Ordinal) || string.Equals(target, "paths", StringComparison.Ordinal))
        {
            if (!TryParseLoopArguments(args, out int iterations, out int seed, out string? shape))
            {
                Console.Error.WriteLine($"Usage: ModernLibs.FileHelpers.Fuzz.Tests {target} <iterations (1 or more)> [seed] [--shape NAME (see --list-shapes)]");
                return 2;
            }

            return string.Equals(target, "paths", StringComparison.Ordinal) ? RunPaths(iterations, seed, shape) : RunDifferential(iterations, seed, shape);
        }

        if (string.Equals(target, PathsSwitchTarget, StringComparison.Ordinal))
        {
            if (args.Length != 2 || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations) || iterations < 1)
            {
                Console.Error.WriteLine("Usage: ModernLibs.FileHelpers.Fuzz.Tests paths-switch <iterations (1 or more)>");
                return 2;
            }

            return RunPathsSwitch(iterations);
        }

        Action<Stream> fuzzAction = target switch
        {
            "delimited" => FuzzQuoteBalanceScanner,
            "spanline" => FuzzSpanLineReader,
            "fixedlength" => FuzzFixedLengthSlicer,
            "converters" => FuzzConverters,
            _ => throw new ArgumentException($"Unknown target: {args[0]}", nameof(args))
        };

        Fuzzer.OutOfProcess.Run(fuzzAction);
        return 0;
    }

    /// <summary>The seed of <c>DifferentialFuzzTests</c>: its 2000 inputs per loop are the first ones of a run.</summary>
    private const int DifferentialSeed = 4910;

    /// <summary>
    /// For every shape of <see cref="Compat49Matrix"/> (or the one <paramref name="onlyShape"/>), two passes of <see cref="DifferentialLoop"/> with
    /// <paramref name="iterations"/> inputs each, the 4.9.1 descriptor (A) against the current one (B):
    /// <list type="bullet">
    /// <item><c>record</c>: one generated record per input, the loop of
    /// <c>DifferentialFuzzTests.Snapshot_4_9_1_And_Current_Descriptor_Agree_On_Random_Input</c>;</item>
    /// <item><c>shape</c>: <see cref="ShapeInput"/>, valid rows around one row with one generated field, the loop of
    /// <c>DifferentialFuzzTests.Snapshot_4_9_1_And_Current_Descriptor_Agree_On_Shape_Aware_Input</c>.</item>
    /// </list>
    /// Stops at the first difference (exit code 1). Prints, per shape and pass, the inputs that gave a record and an
    /// error (for <c>shape</c>: more than the valid rows alone).
    /// </summary>
    private static int RunDifferential(int iterations, int seed, string? onlyShape)
    {
        Console.WriteLine($"Differential fuzz: 4.9.1 descriptor against the current one, {iterations} inputs per shape and pass, seed {seed}{ShapeNote(onlyShape)}");

        foreach (var (name, readOld, readNew, _, _) in Compat49Matrix.All())
        {
            if (onlyShape is not null && !string.Equals(name, onlyShape, StringComparison.Ordinal))
                continue;

            var records = new LineGenerator(seed);
            int fieldCount = Compat49Inputs.FieldCount(name);
            var recordPass = DifferentialLoop.Run(iterations, () => records.NextRecord(fieldCount), readOld, readNew);
            if (!Report(name, "record", seed, recordPass))
                return 1;

            var shaped = ShapeInput.CreateGenerator(name, seed);
            var shapePass = DifferentialLoop.Run(iterations, () => ShapeInput.Next(name, shaped), readOld, readNew, ShapeInput.Baseline(name));
            if (!Report(name, "shape", seed, shapePass))
                return 1;
        }

        Console.WriteLine("Differential fuzz: no difference");
        return 0;
    }

    private static bool Report(string name, string pass, int seed, DifferentialLoop.Result result)
    {
        if (result.Difference is not null)
        {
            Console.WriteLine($"DIFFERENCE in {name}, pass {pass}, at input {result.Inputs} of seed {seed} (A: 4.9.1 descriptor, B: current descriptor)");
            Console.WriteLine(result.Difference);
            return false;
        }

        Console.WriteLine($"  {name} [{pass}]: {result.Inputs} equal; inputs with a record {result.WithRecord}, with an error {result.WithError}");
        return true;
    }

    private static string ShapeNote(string? onlyShape) => onlyShape is null ? string.Empty : ", shape " + onlyShape;

    /// <summary><c>&lt;iterations&gt; [seed] [--shape NAME]</c> after the target name; the shape must be one of the matrix.</summary>
    private static bool TryParseLoopArguments(string[] args, out int iterations, out int seed, out string? shape)
    {
        iterations = 0;
        seed = DifferentialSeed;
        shape = null;
        var positional = new List<string>();
        for (int i = 1; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--shape", StringComparison.Ordinal))
            {
                if (shape is not null || i + 1 >= args.Length)
                    return false;
                shape = args[++i];
            }
            else
            {
                positional.Add(args[i]);
            }
        }

        if (positional.Count is < 1 or > 2
            || !int.TryParse(positional[0], NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
            || iterations < 1
            || (positional.Count == 2 && !int.TryParse(positional[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out seed)))
        {
            return false;
        }

        string? name = shape;
        return name is null || Compat49Matrix.All().Any(c => string.Equals(c.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Part 2 of the differential fuzz (T18): for every shape of <see cref="Compat49Matrix.Paths"/> (or the one
    /// <paramref name="onlyShape"/>), <paramref name="iterations"/> <see cref="ShapeInput"/> inputs read by the current
    /// descriptor on the default engine (A: the tokenized path when the record type is eligible), the forced legacy
    /// path (B) and the scalar tokenizer (C), the loop of
    /// <c>DifferentialFuzzTests.Tokenized_And_Legacy_Paths_Agree_On_Shape_Aware_Input</c>. Stops at the first
    /// difference (exit code 1) and prints the input and both results. A run over every shape in which no shape takes
    /// the tokenized path fails too: it would compare the legacy path with itself.
    /// </summary>
    private static int RunPaths(int iterations, int seed, string? onlyShape)
    {
        Console.WriteLine($"Paths fuzz: default path (A) and scalar tokenizer (C) against the legacy path (B), {iterations} inputs per shape, seed {seed}{ShapeNote(onlyShape)}");

        var total = Stopwatch.StartNew();
        int tokenizedShapes = 0;
        foreach (var (name, readDefault, readScalar, readLegacy, tokenized) in Compat49Matrix.Paths())
        {
            if (onlyShape is not null && !string.Equals(name, onlyShape, StringComparison.Ordinal))
                continue;

            var watch = Stopwatch.StartNew();
            var result = DifferentialLoop.RunShape(name, iterations, seed, readDefault, readLegacy, readScalar);
            if (result.Difference is not null)
            {
                Console.WriteLine($"DIFFERENCE in {name} at input {result.Inputs} of seed {seed} (A: default path, tokenized {tokenized}; B: legacy path; C: scalar tokenizer)");
                Console.WriteLine(result.Difference);
                return 1;
            }

            if (tokenized)
                tokenizedShapes++;
            Console.WriteLine(
                $"  {name} (tokenized: {tokenized}): {result.Inputs} equal; inputs with a record {result.WithRecord}, with an error {result.WithError}; {watch.Elapsed.TotalSeconds:F1} s");
        }

        if (onlyShape is null && tokenizedShapes == 0)
        {
            Console.WriteLine("FAILED: no shape takes the tokenized path (is ModernLibs.FileHelpers.DisableTokenizedParse set?)");
            return 1;
        }

        Console.WriteLine($"Paths fuzz: no difference ({tokenizedShapes} tokenized shapes; {total.Elapsed.TotalSeconds:F1} s)");
        return 0;
    }

    /// <summary>
    /// Review Focus 8, in its own process: <see cref="Main"/> has set <c>DisableTokenizedParse</c>,
    /// <c>DisableVectorizedTokenizer</c> and <c>DisableRecordWriter</c> before any library code ran. Checks that
    /// <see cref="RuntimeSwitches"/> sees them, that <see cref="RecordTokenizer.PlainLoop"/> and
    /// <see cref="RecordTokenizer.QuotedLoop"/> are <see cref="TokenizerLoop.IndexOfHop"/> (T21), that no engine a public constructor or factory makes takes the
    /// tokenized path (plain, async, multi-record, master-detail and its async twin, for every shape), and reads
    /// <paramref name="iterations"/> shape-aware inputs per shape through each of them and through
    /// <c>ParallelReadFile</c> without an exception; the default engine must read them as the forced legacy path does.
    /// The engines over a descriptor with a record writer, made with <see cref="EnginePaths.FromSwitches"/>, must write
    /// through the legacy <c>Format</c> only (T22), and the engines the public constructors make over the generated
    /// descriptors must have chosen the legacy write path (T23). Exit code 1 on the first failed check.
    /// </summary>
    private static int RunPathsSwitch(int iterations)
    {
        Console.WriteLine($"Paths switch: DisableTokenizedParse, DisableVectorizedTokenizer and DisableRecordWriter set at the start of Main; {iterations} inputs per shape");

        var failures = new List<string>();
        if (!RuntimeSwitches.DisableTokenizedParse)
            failures.Add("RuntimeSwitches.DisableTokenizedParse is off");
        if (!RuntimeSwitches.DisableVectorizedTokenizer)
            failures.Add("RuntimeSwitches.DisableVectorizedTokenizer is off");
        if (!RuntimeSwitches.DisableRecordWriter)
            failures.Add("RuntimeSwitches.DisableRecordWriter is off");
        if (!EnginePaths.FromSwitches.LegacyParse || !EnginePaths.FromSwitches.ScalarTokenizer || !EnginePaths.FromSwitches.LegacyWriter)
            failures.Add($"EnginePaths.FromSwitches is {EnginePaths.FromSwitches}");
        // T21: the switch also turns the default loops off, so an engine that injects ScalarTokenizer: false (tests,
        // benchmarks) runs no vector code of the tokenizer either.
        if (RecordTokenizer.PlainLoop != TokenizerLoop.IndexOfHop)
            failures.Add($"RecordTokenizer.PlainLoop is {RecordTokenizer.PlainLoop}");
        if (RecordTokenizer.QuotedLoop != TokenizerLoop.IndexOfHop)
            failures.Add($"RecordTokenizer.QuotedLoop is {RecordTokenizer.QuotedLoop}");

        foreach (var (name, readDefault, readScalar, readLegacy, tokenized) in Compat49Matrix.Paths())
        {
            if (tokenized)
                failures.Add($"{name}: new FileHelperEngine<T>().Core.Tokenized is true");
            var result = DifferentialLoop.RunShape(name, iterations, DifferentialSeed, readDefault, readLegacy, readScalar);
            if (result.Difference is not null)
                failures.Add($"{name}: the default engine reads unlike the legacy path at input {result.Inputs}\n{result.Difference}");
        }

        foreach (string[] shapeFailures in Compat49Matrix.Twins(new SwitchedEngines(iterations)))
            failures.AddRange(shapeFailures);

        if (failures.Count > 0)
        {
            Console.WriteLine($"FAILED: {failures.Count} check(s)");
            foreach (string failure in failures)
                Console.WriteLine("  " + failure);
            return 1;
        }

        Console.WriteLine("Paths switch: every engine takes the legacy read and write paths and reads without an exception");
        return 0;
    }

    /// <summary>
    /// For one shape: the <c>Tokenized</c> flag of every engine kind made by a public constructor or factory, and
    /// the shape-aware inputs read through each of them (<see cref="ErrorMode.SaveAndContinue"/>) and through
    /// <c>ParallelReadFile</c> (the inputs one after the other, repeated past two chunks of 64K chars, against a
    /// sequential read of the same text). Returns the failed checks.
    /// </summary>
    private sealed class SwitchedEngines(int iterations) : Compat49Matrix.ITwinVisitor<string[]>
    {
        private const int ChunkSize = 65_536;

        private static RecordAction Select(ReadOnlySpan<char> line) =>
            !line.IsEmpty && line[0] == '1' ? RecordAction.Master : RecordAction.Detail;

        public string[] Visit<T>(string name)
        {
            var failures = new List<string>();
            void Require(bool condition, string what)
            {
                if (!condition)
                    failures.Add($"{name}: {what}");
            }

            var engine = new FileHelperEngine<T> { ErrorMode = ErrorMode.SaveAndContinue };
            using var asyncEngine = new FileHelperAsyncEngine<T> { ErrorMode = ErrorMode.SaveAndContinue };
            var entry = (MultiRecordType<T>)MultiRecordType.For<T>(static _ => true);
            var multi = new MultiRecordEngine(entry) { ErrorMode = ErrorMode.SaveAndContinue };
            var masterDetail = new MasterDetailEngine<T, T>(Select) { ErrorMode = ErrorMode.SaveAndContinue };
            var masterDetailAsync = new MasterDetailAsyncEngine<T, T>(Select) { ErrorMode = ErrorMode.SaveAndContinue };

            Require(!engine.Core.Tokenized, "FileHelperEngine Core.Tokenized is true");
            Require(!asyncEngine.Core.Tokenized, "FileHelperAsyncEngine Core.Tokenized is true");
            Require(!entry.Core.Tokenized, "MultiRecordType.For Core.Tokenized is true");
            Require(!masterDetail.MasterCore.Tokenized && !masterDetail.DetailCore.Tokenized, "MasterDetailEngine MasterCore/DetailCore.Tokenized is true");
            Require(!masterDetailAsync.MasterCore.Tokenized && !masterDetailAsync.DetailCore.Tokenized, "MasterDetailAsyncEngine MasterCore/DetailCore.Tokenized is true");

            var generator = ShapeInput.CreateGenerator(name, DifferentialSeed);
            var all = new StringBuilder();
            for (int i = 0; i < iterations; i++)
            {
                string input = ShapeInput.Next(name, generator);
                all.Append(input);
                Read(failures, name, "FileHelperEngine", input, () => engine.ReadString(input).Length);
                Read(failures, name, "FileHelperAsyncEngine", input, () =>
                {
                    asyncEngine.BeginReadString(input);
                    int count = asyncEngine.Count();
                    asyncEngine.Close();
                    return count;
                });
                Read(failures, name, "MultiRecordEngine", input, () => multi.ReadString(input).Length);
                Read(failures, name, "MasterDetailEngine", input, () => masterDetail.ReadString(input).Length);
                Read(failures, name, "MasterDetailAsyncEngine", input, () =>
                {
                    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
                    return masterDetailAsync.ReadAsync(stream).ToBlockingEnumerable().Count();
                });
            }

            masterDetailAsync.DisposeAsync().AsTask().GetAwaiter().GetResult();

            int chars = ReadParallel(failures, name, engine, all.ToString());
            int written = CheckLegacyWriter<T>(failures, name);
            Console.WriteLine($"  {name}: legacy path on 5 engine kinds; {iterations} inputs read through each and {chars} chars through ParallelReadFile; "
                + $"{written} records written through the legacy Format by 5 engine kinds");
            return failures.ToArray();
        }

        private static RecordAction Master(ReadOnlySpan<char> line) => RecordAction.Master;

        /// <summary>
        /// T22, T23: for the valid records of the shape (at least one), <see cref="CheckPublicEngines{T}"/>, then
        /// engines made with <see cref="EnginePaths.FromSwitches"/> over a <see cref="WriterCheckingDescriptor{T}"/>
        /// must call the legacy <c>Format</c> only. Returns the number of records the legacy <c>Format</c> wrote there.
        /// </summary>
        private static int CheckLegacyWriter<T>(List<string> failures, string name)
        {
            var records = new FileHelperEngine<T> { ErrorMode = ErrorMode.SaveAndContinue }.ReadString(Compat49Inputs.For(name).First());
            if (records.Length == 0)
            {
                failures.Add($"{name}: the first matrix input gave no record to write");
                return 0;
            }

            MasterDetails<T, T>[] groups = [new MasterDetails<T, T>(records[0], records[1..])];
            CheckPublicEngines(failures, name, records, groups);

            var descriptor = new WriterCheckingDescriptor<T>(FileHelperRegistry<T>.Descriptor);
            var paths = EnginePaths.FromSwitches;
            try
            {
                new FileHelperEngine<T>(descriptor, Encoding.UTF8, paths).WriteString(records);
                new FileHelperEngine<T>(descriptor, Encoding.UTF8, paths).WriteStreamAsync(new StringWriter(), records).GetAwaiter().GetResult();
                using (var asyncEngine = new FileHelperAsyncEngine<T>(descriptor, paths))
                    WriteNext(asyncEngine, records);
                new MultiRecordEngine(new MultiRecordType<T>(static _ => true, descriptor, paths)).WriteString(records.Cast<object>());
                new MasterDetailEngine<T, T>(Master, descriptor, descriptor, paths).WriteString(groups);
            }
            catch (Exception ex)
            {
                failures.Add($"{name}: a write threw {ex.GetType().Name}: {ex.Message}");
            }

            if (descriptor.WriterFormatCalls != 0 || descriptor.LegacyFormatCalls != 5 * records.Length)
            {
                failures.Add($"{name}: with DisableRecordWriter the writes called the record writer {descriptor.WriterFormatCalls} times "
                    + $"and the legacy Format {descriptor.LegacyFormatCalls} times for {records.Length} records on 5 engine kinds");
            }

            return descriptor.LegacyFormatCalls;
        }

        /// <summary>
        /// T23: the engines that the public constructors and factories make over the generated descriptor (which
        /// implements <see cref="IRecordWriterDescriptor{T}"/> since T23) chose the legacy write path
        /// (<see cref="WritePathProbe"/>), and each writes <paramref name="records"/> as the same engine kind forced onto
        /// the record writer does.
        /// </summary>
        private static void CheckPublicEngines<T>(List<string> failures, string name, T[] records, MasterDetails<T, T>[] groups)
        {
            var generated = FileHelperRegistry<T>.Descriptor;
            var recordWriterPaths = EnginePaths.FromSwitches with { LegacyWriter = false };
            if (generated is not IRecordWriterDescriptor<T>)
                failures.Add($"{name}: the generated descriptor does not implement IRecordWriterDescriptor<T>");

            var engine = new FileHelperEngine<T>();
            var entry = (MultiRecordType<T>)MultiRecordType.For<T>(static _ => true);
            var masterDetail = new MasterDetailEngine<T, T>(Master);
            using (var asyncEngine = new FileHelperAsyncEngine<T>())
            {
                if (WritePathProbe.UsesRecordWriter(engine) || WritePathProbe.UsesRecordWriter(asyncEngine)
                    || WritePathProbe.UsesRecordWriter(entry) || !WritePathProbe.UsesLegacyWriter(masterDetail))
                {
                    failures.Add($"{name}: with DisableRecordWriter an engine made by a public constructor chose the record writer");
                }

                void Same(string kind, Func<string> legacy, Func<string> recordWriter)
                {
                    try
                    {
                        string expected = recordWriter();
                        if (!string.Equals(legacy(), expected, StringComparison.Ordinal))
                            failures.Add($"{name}: {kind} with DisableRecordWriter writes unlike the record writer");
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{name}: {kind}: a write threw {ex.GetType().Name}: {ex.Message}");
                    }
                }

                Same("FileHelperEngine.WriteString", () => engine.WriteString(records),
                    () => new FileHelperEngine<T>(generated, Encoding.UTF8, recordWriterPaths).WriteString(records));
                Same("FileHelperEngine.WriteStreamAsync", () => WriteAsync(engine, records),
                    () => WriteAsync(new FileHelperEngine<T>(generated, Encoding.UTF8, recordWriterPaths), records));
                Same("FileHelperAsyncEngine.WriteNext", () => WriteNext(asyncEngine, records), () =>
                {
                    using var recordWriter = new FileHelperAsyncEngine<T>(generated, recordWriterPaths);
                    return WriteNext(recordWriter, records);
                });
                Same("MultiRecordEngine.WriteString", () => new MultiRecordEngine(entry).WriteString(records.Cast<object>()),
                    () => new MultiRecordEngine(new MultiRecordType<T>(static _ => true, generated, recordWriterPaths)).WriteString(records.Cast<object>()));
                Same("MasterDetailEngine.WriteString", () => masterDetail.WriteString(groups),
                    () => new MasterDetailEngine<T, T>(Master, generated, generated, recordWriterPaths).WriteString(groups));
            }
        }

        private static string WriteAsync<T>(FileHelperEngine<T> engine, T[] records)
        {
            var writer = new StringWriter();
            engine.WriteStreamAsync(writer, records).GetAwaiter().GetResult();
            return writer.ToString();
        }

        private static string WriteNext<T>(FileHelperAsyncEngine<T> engine, T[] records)
        {
            engine.BeginWriteString();
            engine.WriteNext(records);
            return engine.EndWriteString();
        }

        /// <summary><paramref name="text"/> repeated past two chunks, read by <c>ParallelReadFile</c> and sequentially; returns its length.</summary>
        private static int ReadParallel<T>(List<string> failures, string name, FileHelperEngine<T> engine, string text)
        {
            var file = new StringBuilder(text);
            while (file.Length <= 2 * ChunkSize)
                file.Append(text);
            string content = file.ToString();
            string path = Path.Combine(Path.GetTempPath(), "fh-paths-switch-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                File.WriteAllText(path, content, engine.Encoding);
                int sequential = engine.ReadString(content).Length;
                int sequentialErrors = engine.ErrorManager.ErrorCount;
                Read(failures, name, "ParallelReadFile", "<the inputs repeated>", () =>
                {
                    int parallel = engine.ParallelReadFile(path, ChunkSize).Length;
                    if (parallel != sequential || engine.ErrorManager.ErrorCount != sequentialErrors)
                    {
                        failures.Add($"{name}: ParallelReadFile read {parallel} records and {engine.ErrorManager.ErrorCount} errors, "
                            + $"the sequential read {sequential} and {sequentialErrors}");
                    }

                    return parallel;
                });
            }
            finally
            {
                File.Delete(path);
            }

            return content.Length;
        }

        private static void Read(List<string> failures, string name, string engine, string input, Func<int> read)
        {
            try
            {
                _ = read();
            }
            catch (Exception ex)
            {
                failures.Add($"{name}: {engine} threw {ex.GetType().Name}: {ex.Message} on input \"{input.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)}\"");
            }
        }
    }

    public static void FuzzQuoteBalanceScanner(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var input = reader.ReadToEnd();

        if (input.Length == 0)
            return;

        // Quote-aware multiline detection and field counting used by the engines
        var lineReader = new SpanLineReader(input);
        // Alternating per-field trim settings exercise both "blanks before quote" rules.
        string?[] trimStart = [" \t", null, " \t", null];
        const string quotes = "\"\"\0'"; // quoted, quoted, unquoted, apostrophe-quoted fields
        bool insideQuotes = false;
        int field = 0;
        while (lineReader.TryReadLine(out var line))
        {
            if (!insideQuotes)
                field = 0;
            insideQuotes = QuoteBalanceScanner.EndsInsideQuotes(line, insideQuotes, ",", quotes, trimStart, ref field);
            _ = QuoteBalanceScanner.CountFields(line, ",", quotes, trimStart);
            _ = QuoteBalanceScanner.CountFields(line, ";|", "''", [" \t"]);
        }
    }

    public static void FuzzSpanLineReader(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var input = reader.ReadToEnd();

        if (input.Length == 0)
            return;

        var lineReader = new SpanLineReader(input);
        int lineCount = 0;
        while (lineReader.TryReadLine(out var line))
        {
            lineCount++;
            _ = line.Length;
            _ = lineReader.LineNumber;

            // Don't let fuzzing run forever on huge inputs
            if (lineCount > 10000)
                break;
        }
    }

    public static void FuzzFixedLengthSlicer(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var input = reader.ReadToEnd();

        if (input.Length == 0)
            return;

        // Use first line only for FixedLengthSlicer
        var lineReader = new SpanLineReader(input);
        if (!lineReader.TryReadLine(out var line))
            return;

        var slicer = new FixedLengthSlicer(line);

        // Test various offset/width combinations
        int[][] testCases =
        [
            [0, 5], [0, 10], [0, 100], [0, line.Length],
            [5, 10], [5, 5], [5, line.Length],
            [line.Length - 1, 1], [line.Length - 1, 5],
            [-1, 5], [0, 0], [0, -1], [-5, -5],
            [line.Length, 5], [line.Length + 1, 5],
            [int.MaxValue, 5], [0, int.MaxValue],
        ];

        foreach (var tc in testCases)
        {
            var result = slicer.Slice(tc[0], tc[1]);
            _ = result.Length;
            _ = result.IsEmpty;
        }
    }

    public static void FuzzConverters(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var input = reader.ReadToEnd().Trim();

        if (input.Length == 0)
            return;

        var span = input.AsSpan();

        // Integer converters — never throw, return bool
        _ = Int32Converter.Instance.TryParse(span, out _);
        _ = Int64Converter.Instance.TryParse(span, out _);
        _ = ByteConverter.Instance.TryParse(span, out _);
        _ = Int16Converter.Instance.TryParse(span, out _);
        _ = UInt32Converter.Instance.TryParse(span, out _);
        _ = UInt64Converter.Instance.TryParse(span, out _);

        // Float converters — never throw, return bool
        _ = DoubleConverter.Instance.TryParse(span, out _);
        _ = DecimalConverter.Instance.TryParse(span, out _);
        _ = SingleConverter.Instance.TryParse(span, out _);
        _ = PercentDoubleConverter.Instance.TryParse(span, out _);

        // Bool converter — never throws
        _ = BoolConverter.Instance.TryParse(span, out _);

        // DateTime converter with a flexible format — never throws
        var dateTimeConverter = new DateTimeConverter("yyyy-MM-dd");
        _ = dateTimeConverter.TryParse(span, out _);

        // DateOnly converter
        var dateOnlyConverter = new DateOnlyConverter("yyyy-MM-dd");
        _ = dateOnlyConverter.TryParse(span, out _);

        // TimeOnly converter
        var timeOnlyConverter = new TimeOnlyConverter("HH:mm:ss");
        _ = timeOnlyConverter.TryParse(span, out _);

        // String converter — always succeeds
        _ = StringConverter.Instance.TryParse(span, out _);

        // Char converter — only succeeds for single char, never throws
        _ = CharConverter.Instance.TryParse(span, out _);

        // Guid converter
        _ = GuidConverter.Instance.TryParse(span, out _);
    }
}
