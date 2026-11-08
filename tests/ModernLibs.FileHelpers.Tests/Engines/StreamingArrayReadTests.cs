// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using System.Text;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

[DelimitedRecord(",")]
public sealed class SarPlain
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Text { get; set; } = string.Empty;
}

[DelimitedRecord(",")]
[IgnoreFirst(1)]
[IgnoreLast(2)]
public sealed class SarFramed
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Text { get; set; } = string.Empty;
}

/// <summary>
/// <c>ReadFile(path)</c>, <c>ReadFile(path, maxRecords)</c>, <c>ReadStream(reader)</c> and
/// <c>ReadStream(reader, maxRecords)</c> give what <c>ReadString</c> gives for the same text: records, errors,
/// <c>HeaderText</c>, <c>FooterText</c>, <c>LineNumber</c> and <c>TotalRecords</c>, also when reading stops early.
/// </summary>
[Category("ReadCore")]
public sealed class StreamingArrayReadTests
{
    private sealed record Outcome(string Records, string Errors, string? Header, string? Footer, int LineNumber, int TotalRecords);

    private static Outcome Capture<T>(FileHelperEngine<T> engine, T[] records, Func<T, string> show)
        where T : class
        => new(
            string.Join("|", records.Select(show)),
            string.Join("|", engine.ErrorManager.Errors.Select(e => $"{e.LineNumber}:{e.RecordString}:{e.ExceptionInfo?.GetType().Name}")),
            engine.HeaderText,
            engine.FooterText,
            engine.LineNumber,
            engine.TotalRecords);

    /// <summary>Inputs: empty, header only, no final terminator, mixed terminators, bad records, blank lines.</summary>
    private static IEnumerable<string> Inputs()
    {
        yield return string.Empty;
        yield return "id,text";
        yield return "id,text\r\n";
        yield return "id,text\r\n1,a\r\n2,b\r\nfooter 1\r\nfooter 2";
        yield return "id,text\n1,a\n2,b\nfooter 1\nfooter 2\n";
        yield return "id,text\r1,a\r2,b\rf1\rf2\r";
        yield return "id,text\r\n1,a\r\nx,bad\r\n\r\n3,c\r\n4\r\n5,e\r\nf1\r\nf2\r\n";
        yield return "id,text\r\n1,a";
        var large = new StringBuilder("id,text\r\n");
        for (int i = 1; i <= 5_000; i++)
            large.Append(i % 97 == 0 ? "bad" : i.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",t").Append(i).Append(i % 3 == 0 ? "\n" : "\r\n");
        large.Append("total,5000\r\nend");
        yield return large.ToString();
    }

    private static readonly int[] MaxRecordValues = [0, 1, 2, 5, 4_000, 100_000];

    private static async Task AssertSameAsReadString<T>(Func<FileHelperEngine<T>> create, Func<T, string> show)
        where T : class
    {
        int n = 0;
        foreach (string input in Inputs())
        {
            foreach (int maxRecords in MaxRecordValues)
            {
                foreach (var mode in new[] { ErrorMode.SaveAndContinue, ErrorMode.IgnoreAndContinue })
                {
                    n++;
                    string label = $"input {n}, maxRecords {maxRecords}, {mode}";

                    // Arrange: the reference is the in-memory read
                    var reference = create();
                    reference.ErrorMode = mode;
                    var expected = Capture(reference, reference.ReadString(input, maxRecords), show);
                    var path = Path.GetTempFileName();
                    try
                    {
                        await File.WriteAllTextAsync(path, input);

                        // Act: the file and reader overloads, with and without a cap
                        var file = create();
                        file.ErrorMode = mode;
                        var fileRecords = maxRecords == 0 ? file.ReadFile(path) : file.ReadFile(path, maxRecords);
                        var stream = create();
                        stream.ErrorMode = mode;
                        using var reader = new StringReader(input);
                        var streamRecords = maxRecords == 0 ? stream.ReadStream(reader) : stream.ReadStream(reader, maxRecords);

                        // Assert: the same outcome, and the reader is read to its end (as by ReadToEnd)
                        await Assert.That(Capture(file, fileRecords, show)).IsEqualTo(expected).Because("ReadFile, " + label);
                        await Assert.That(Capture(stream, streamRecords, show)).IsEqualTo(expected).Because("ReadStream, " + label);
                        await Assert.That(reader.Peek()).IsEqualTo(-1).Because("reader position, " + label);
                    }
                    finally
                    {
                        File.Delete(path);
                    }
                }
            }
        }
    }

    [Test]
    [DisplayName("Without header or footer lines, file and reader reads match ReadString for every input and cap")]
    public async Task Plain_MatchesReadString() =>
        await AssertSameAsReadString(() => new FileHelperEngine<SarPlain>(), r => $"{r.Id}:{r.Text}");

    [Test]
    [DisplayName("With IgnoreFirst(1) and IgnoreLast(2), file and reader reads match ReadString, FooterText included after a cap")]
    public async Task Framed_MatchesReadString() =>
        await AssertSameAsReadString(() => new FileHelperEngine<SarFramed>(), r => $"{r.Id}:{r.Text}");

    [Test]
    [DisplayName("StopReading from BeforeReadRecord on record 3 leaves the same counters, header and footer as ReadString")]
    public async Task StopReading_MatchesReadString()
    {
        // Arrange: a framed file (header line, two footer lines); reading stops at the third record (line 4)
        string input = Inputs().Last();
        static Outcome Run(Func<FileHelperEngine<SarFramed>, SarFramed[]> read)
        {
            var engine = new FileHelperEngine<SarFramed>();
            engine.BeforeReadRecord += (_, e) =>
            {
                if (e.LineNumber == 4)
                    e.StopReading = true;
            };
            return Capture(engine, read(engine), r => $"{r.Id}:{r.Text}");
        }

        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, input);

            // Act
            var expected = Run(engine => engine.ReadString(input));
            var file = Run(engine => engine.ReadFile(path));
            var stream = Run(engine => engine.ReadStream(new StringReader(input)));

            // Assert
            await Assert.That(expected.TotalRecords).IsEqualTo(2);
            await Assert.That(expected.Footer).IsEqualTo("total,5000\r\nend");
            await Assert.That(file).IsEqualTo(expected);
            await Assert.That(stream).IsEqualTo(expected);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [DisplayName("A file that does not decode with a throwing encoding raises the decoder exception from ReadFile")]
    public async Task DecodingError_Propagates()
    {
        // Arrange: valid records, then an invalid UTF-8 byte at the end
        var path = Path.GetTempFileName();
        try
        {
            var bytes = Encoding.UTF8.GetBytes("1,a\r\n2,b\r\n3,c").Concat(new byte[] { 0xFF, 0x0D, 0x0A }).ToArray();
            await File.WriteAllBytesAsync(path, bytes);
            var engine = new FileHelperEngine<SarPlain> { Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true) };

            // Act / Assert
            await Assert.That(() => engine.ReadFile(path)).Throws<DecoderFallbackException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [DisplayName("ReadFile allocates about what ReadString allocates for the same text, not the file text on top")]
    public async Task ReadFile_AllocatesLikeReadString()
    {
        // Arrange: 20K records; warm up both reads
        var content = string.Concat(Enumerable.Range(1, 20_000).Select(i => $"{i},text {i}\r\n"));
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, content);
            var engine = new FileHelperEngine<SarPlain>();
            _ = engine.ReadString(content);
            _ = engine.ReadFile(path);

            // Act
            long before = GC.GetAllocatedBytesForCurrentThread();
            var fromString = engine.ReadString(content);
            long stringBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            var fromFile = engine.ReadFile(path);
            long fileBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert: 4.8.0 also allocated the whole file as one string (2 bytes per char)
            await Assert.That(fromFile.Length).IsEqualTo(fromString.Length);
            await Assert.That(fileBytes).IsLessThan(stringBytes * 12 / 10).Because($"ReadFile {fileBytes} B, ReadString {stringBytes} B");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
