using System.IO;
using TUnit.Core;
using ModernLibs.FileHelpers.Parsing;
using ModernLibs.FileHelpers.Converters;

// CA1707: Test method names use underscores intentionally.
// MA0051: FuzzAllTargets is intentionally comprehensive to verify all targets in one test.
#pragma warning disable CA1707, MA0051

namespace ModernLibs.FileHelpers.Tests.Fuzz;

[Category("Fuzz")]
public sealed class FuzzRunner
{
    private static readonly string s_corpusPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ModernLibs.FileHelpers.Fuzz.Tests", "FuzzCorpus");

    private static string[] GetCorpusFiles()
    {
        if (!Directory.Exists(s_corpusPath))
            return [];
        return Directory.GetFiles(s_corpusPath, "*.txt");
    }

    private static MemoryStream CreateStreamForFile(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        return new MemoryStream(bytes);
    }

    [Test]
    [DisplayName("Fuzz: SpanLineReader against seed corpus")]
    public async Task FuzzSpanLineReader_AgainstCorpus_NoExceptions()
    {
        // Arrange: load seed corpus files
        var files = GetCorpusFiles();

        // Act: run fuzz target logic against each file
        foreach (var file in files)
        {
            using var stream = CreateStreamForFile(file);
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var input = reader.ReadToEnd();

            if (input.Length == 0)
                continue;

            // Assert: no exceptions thrown
            await Assert.That(() =>
            {
                var lineReader = new SpanLineReader(input);
                int lineCount = 0;
                while (lineReader.TryReadLine(out var line))
                {
                    lineCount++;
                    _ = line.Length;
                    _ = lineReader.LineNumber;
                    if (lineCount > 10000)
                        break;
                }
            }).ThrowsNothing();
        }
    }

    [Test]
    [DisplayName("Fuzz: FixedLengthSlicer against seed corpus")]
    public async Task FuzzFixedLengthSlicer_AgainstCorpus_NoExceptions()
    {
        // Arrange: load seed corpus files
        var files = GetCorpusFiles();

        // Act: run fuzz target logic against each file
        foreach (var file in files)
        {
            using var stream = CreateStreamForFile(file);
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var input = reader.ReadToEnd();

            if (input.Length == 0)
                continue;

            // Assert: no exceptions thrown
            await Assert.That(() =>
            {
                var lineReader = new SpanLineReader(input);
                if (!lineReader.TryReadLine(out var line))
                    return;
                var slicer = new FixedLengthSlicer(line);
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
            }).ThrowsNothing();
        }
    }

    [Test]
    [DisplayName("Fuzz: Converters against seed corpus")]
    public async Task FuzzConverters_AgainstCorpus_NoExceptions()
    {
        // Arrange: load seed corpus files
        var files = GetCorpusFiles();

        // Act: run fuzz target logic against each file
        foreach (var file in files)
        {
            using var stream = CreateStreamForFile(file);
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var input = reader.ReadToEnd().Trim();

            if (input.Length == 0)
                continue;

            // Assert: no exceptions thrown
            await Assert.That(() =>
            {
                var span = input.AsSpan();
                _ = Int32Converter.Instance.TryParse(span, out _);
                _ = Int64Converter.Instance.TryParse(span, out _);
                _ = ByteConverter.Instance.TryParse(span, out _);
                _ = Int16Converter.Instance.TryParse(span, out _);
                _ = UInt32Converter.Instance.TryParse(span, out _);
                _ = UInt64Converter.Instance.TryParse(span, out _);
                _ = DoubleConverter.Instance.TryParse(span, out _);
                _ = DecimalConverter.Instance.TryParse(span, out _);
                _ = SingleConverter.Instance.TryParse(span, out _);
                _ = PercentDoubleConverter.Instance.TryParse(span, out _);
                _ = BoolConverter.Instance.TryParse(span, out _);
                _ = new DateTimeConverter("yyyy-MM-dd").TryParse(span, out _);
                _ = new DateOnlyConverter("yyyy-MM-dd").TryParse(span, out _);
                _ = new TimeOnlyConverter("HH:mm:ss").TryParse(span, out _);
                _ = StringConverter.Instance.TryParse(span, out _);
                _ = CharConverter.Instance.TryParse(span, out _);
                _ = GuidConverter.Instance.TryParse(span, out _);
            }).ThrowsNothing();
        }
    }

    [Test]
    [DisplayName("Fuzz: all targets against seed corpus")]
    public async Task FuzzAllTargets_AgainstCorpus_NoExceptions()
    {
        // Arrange: load seed corpus files
        var files = GetCorpusFiles();

        // Act & Assert: run all fuzz target logic against each file
        foreach (var file in files)
        {
            using var stream = CreateStreamForFile(file);
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var input = reader.ReadToEnd();
            var trimmedInput = input.Trim();

            // SpanLineReader target
            if (input.Length > 0)
            {
                await Assert.That(() =>
                {
                    var lineReader = new SpanLineReader(input);
                    int count = 0;
                    while (lineReader.TryReadLine(out var line)) { count++; if (count > 10000) break; }
                }).ThrowsNothing();
            }

            // FixedLengthSlicer target
            if (input.Length > 0)
            {
                await Assert.That(() =>
                {
                    var lineReader = new SpanLineReader(input);
                    if (!lineReader.TryReadLine(out var line)) return;
                    var slicer = new FixedLengthSlicer(line);
                    int[][] testCases = [[0, 5], [0, 10], [0, 100], [0, line.Length], [5, 10], [5, 5], [-1, 5], [0, 0], [line.Length, 5], [int.MaxValue, 5]];
                    foreach (var tc in testCases) { var result = slicer.Slice(tc[0], tc[1]); _ = result.Length; }
                }).ThrowsNothing();
            }

            // Converters target
            if (trimmedInput.Length > 0)
            {
                await Assert.That(() =>
                {
                    var span = trimmedInput.AsSpan();
                    _ = Int32Converter.Instance.TryParse(span, out _);
                    _ = DoubleConverter.Instance.TryParse(span, out _);
                    _ = BoolConverter.Instance.TryParse(span, out _);
                    _ = new DateTimeConverter("yyyy-MM-dd").TryParse(span, out _);
                    _ = StringConverter.Instance.TryParse(span, out _);
                }).ThrowsNothing();
            }
        }
    }
}
