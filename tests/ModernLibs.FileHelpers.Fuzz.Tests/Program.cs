using System;
using System.Globalization;
using System.IO;
using System.Text;
using SharpFuzz;
using ModernLibs.FileHelpers.Parsing;
using ModernLibs.FileHelpers.Converters;
using ModernLibs.FileHelpers.Tests.Compat;
using ModernLibs.FileHelpers.Tests.Fuzz.DiffFuzz;

namespace ModernLibs.FileHelpers.Fuzz.Tests;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: ModernLibs.FileHelpers.Fuzz.Tests <target>");
            Console.WriteLine("Targets: delimited, spanline, fixedlength, converters, extended [iterations], differential <iterations> [seed]");
            return 0;
        }

        var target = args[0].ToLowerInvariant();
        if (string.Equals(target, "extended", StringComparison.Ordinal))
        {
            int iterations = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 10000;
            return ExtendedFuzzRunner.Run(iterations);
        }

        if (string.Equals(target, "differential", StringComparison.Ordinal))
        {
            if (args.Length < 2)
                throw new ArgumentException("Usage: differential <iterations> [seed]", nameof(args));
            int iterations = int.Parse(args[1], CultureInfo.InvariantCulture);
            int seed = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : DifferentialSeed;
            return RunDifferential(iterations, seed);
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

    /// <summary>The seed of <c>DifferentialFuzzTests</c>: its 2000 records are the first ones of a run.</summary>
    private const int DifferentialSeed = 4910;

    /// <summary>
    /// For every shape of <see cref="Compat49Matrix"/>: reads <paramref name="iterations"/> generated records with
    /// the 4.9.1 descriptor and with the current one, and stops at the first difference (exit code 1). The loop is
    /// the one of <c>DifferentialFuzzTests.Snapshot_4_9_1_And_Current_Descriptor_Agree_On_Random_Input</c>.
    /// </summary>
    private static int RunDifferential(int iterations, int seed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        Console.WriteLine($"Differential fuzz: 4.9.1 descriptor against the current one, {iterations} records per shape, seed {seed}");

        foreach (var (name, readOld, readNew, _, _) in Compat49Matrix.All())
        {
            var generator = new LineGenerator(seed);
            int fieldCount = Compat49Inputs.FieldCount(name);
            int withRecord = 0, withError = 0;
            string lastOld = string.Empty;
            string ReadOld(string input) => lastOld = readOld(input);

            for (int i = 0; i < iterations; i++)
            {
                lastOld = string.Empty;                           // stays empty when the old read throws
                string? difference = ReadComparer.Compare(generator.NextRecord(fieldCount), ReadOld, readNew);
                if (difference is not null)
                {
                    Console.WriteLine($"DIFFERENCE in {name} at record {i + 1} of seed {seed} (A: 4.9.1 descriptor, B: current descriptor)");
                    Console.WriteLine(difference);
                    return 1;
                }

                if (lastOld.StartsWith("record:", StringComparison.Ordinal) || lastOld.Contains("\nrecord:", StringComparison.Ordinal))
                    withRecord++;
                if (lastOld.StartsWith("error:", StringComparison.Ordinal) || lastOld.Contains("\nerror:", StringComparison.Ordinal))
                    withError++;
            }

            Console.WriteLine($"  {name}: {iterations} equal; inputs with a record {withRecord}, with an error {withError}");
        }

        Console.WriteLine("Differential fuzz: no difference");
        return 0;
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
