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

[DelimitedRecord(",")]
[IgnoreFirst(1)]
[IgnoreLast(2)]
public sealed class SarMultiline
{
    [FieldOrder(1)] public int Id { get; set; }

    [FieldOrder(2)]
    [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.AllowForRead)]
    public string Text { get; set; } = string.Empty;
}

[DelimitedRecord(",")]
[IgnoreLast(1)]
public sealed class SarNewLine
{
    [FieldOrder(1)] public int Id { get; set; }

    [FieldOrder(2)]
    [FieldInNewLine]
    public string Text { get; set; } = string.Empty;
}

[DelimitedRecord(",")]
public record struct SarStruct
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Text { get; set; }
}

/// <summary>24 bytes: not a power of two, so the chunk cap is rounded down to one.</summary>
[DelimitedRecord(",")]
public record struct SarStruct24
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Text { get; set; }
    [FieldOrder(3)] public string More { get; set; }
}

/// <summary>A reader that gives <c>text</c> up to <c>faultAt</c> characters and then throws <see cref="IOException"/>.</summary>
internal sealed class SarFaultingReader(string text, int faultAt) : TextReader
{
    private int _position;

    public override int Peek() => _position < text.Length ? text[_position] : -1;

    public override int Read() => Read(new char[1], 0, 1) == 0 ? -1 : text[_position - 1];

    public override int Read(char[] buffer, int index, int count) => Read(buffer.AsSpan(index, count));

    public override int Read(Span<char> buffer)
    {
        if (_position >= faultAt)
            throw new IOException("reader fault");
        int n = Math.Min(Math.Min(buffer.Length, text.Length - _position), faultAt - _position);
        text.AsSpan(_position, n).CopyTo(buffer);
        _position += n;
        return n;
    }
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

    private static Task AssertSameAsReadString<T>(Func<FileHelperEngine<T>> create, Func<T, string> show)
        where T : class
        => AssertSameAsReadString(create, show, Inputs(), MaxRecordValues);

    private static async Task AssertSameAsReadString<T>(Func<FileHelperEngine<T>> create, Func<T, string> show, IEnumerable<string> inputs, int[] maxRecordValues)
        where T : class
    {
        int n = 0;
        foreach (string input in inputs)
        {
            foreach (int maxRecords in maxRecordValues)
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
    [DisplayName("A quoted field that spans lines, also in the last data record, reads from file and reader as from ReadString")]
    public async Task MultilineQuoted_MatchesReadString()
    {
        // Arrange: the last data record (or the one at the cap) continues on the next lines; the footer follows it
        string[] inputs =
        [
            "id,text\r\n1,\"a\r\nb\"\r\n2,\"c\r\nd\r\ne\"\r\nf1\r\nf2\r\n",
            "id,text\r\n1,\"a\r\nb\"\r\n2,\"open\r\nf1\r\nf2",
            "id,text\n1,plain\n2,\"x\ny\"\n3,\"z\"\"q\nw\"\nf1\nf2\n",
            "id,text\r\n1,\"one\r\n\"\r\nf1\r\nf2\r\n",
        ];

        // Act / Assert
        await AssertSameAsReadString(() => new FileHelperEngine<SarMultiline>(), r => $"{r.Id}:{r.Text}", inputs, [0, 1, 2, 3]);
    }

    [Test]
    [DisplayName("A record with a FieldInNewLine field and a footer line reads from file and reader as from ReadString")]
    public async Task FieldInNewLine_MatchesReadString()
    {
        // Arrange: two physical lines per record, one footer line; one input ends inside a record
        string[] inputs =
        [
            "1\r\na\r\n2\r\nb\r\nfooter\r\n",
            "1\na\n2\nb\n3\nfooter",
            "1\r\na\r\nx\r\nb\r\n3\r\nc\r\nfooter\r\n",
            "1\r\na\r\n2\r\nfooter\r\n",
        ];

        // Act / Assert
        await AssertSameAsReadString(() => new FileHelperEngine<SarNewLine>(), r => $"{r.Id}:{r.Text}", inputs, [0, 1, 2, 3]);
    }

    [Test]
    [DisplayName("With ThrowException, file and reader reads throw what ReadString throws and leave the same counters")]
    public async Task ThrowException_MatchesReadString()
    {
        // Arrange: a bad record (line 3), a short record (line 3) and a framed input with a bad record after the header
        (string Input, bool Framed)[] cases =
        [
            ("1,a\r\n2,b\r\nx,bad\r\n4,d\r\n", false),
            ("1,a\n2,b\n3\n4,d\n", false),
            ("id,text\r\n1,a\r\ny,bad\r\n3,c\r\nf1\r\nf2\r\n", true),
        ];

        foreach (var (input, framed) in cases)
        {
            foreach (int maxRecords in new[] { 0, 5 })
            {
                string label = $"'{input}', maxRecords {maxRecords}";
                var path = Path.GetTempFileName();
                try
                {
                    await File.WriteAllTextAsync(path, input);
                    using var reader = new StringReader(input);

                    // Act
                    var (expected, file, stream) = framed
                        ? (Throwing<SarFramed>(e => e.ReadString(input, maxRecords)),
                           Throwing<SarFramed>(e => maxRecords == 0 ? e.ReadFile(path) : e.ReadFile(path, maxRecords)),
                           Throwing<SarFramed>(e => maxRecords == 0 ? e.ReadStream(reader) : e.ReadStream(reader, maxRecords)))
                        : (Throwing<SarPlain>(e => e.ReadString(input, maxRecords)),
                           Throwing<SarPlain>(e => maxRecords == 0 ? e.ReadFile(path) : e.ReadFile(path, maxRecords)),
                           Throwing<SarPlain>(e => maxRecords == 0 ? e.ReadStream(reader) : e.ReadStream(reader, maxRecords)));

                    // Assert: the same exception and counters; the reader is at its end and the file is closed
                    await Assert.That(expected).DoesNotStartWith("no exception").Because(label);
                    await Assert.That(file).IsEqualTo(expected).Because("ReadFile, " + label);
                    await Assert.That(stream).IsEqualTo(expected).Because("ReadStream, " + label);
                    await Assert.That(reader.Peek()).IsEqualTo(-1).Because("reader position, " + label);
                }
                finally
                {
                    File.Delete(path);
                }

                await Assert.That(File.Exists(path)).IsFalse().Because("file deleted after the throw, " + label);
            }
        }
    }

    private static string Throwing<T>(Func<FileHelperEngine<T>, T[]> read)
        where T : class
    {
        var engine = new FileHelperEngine<T> { ErrorMode = ErrorMode.ThrowException };
        try
        {
            var records = read(engine);
            return $"no exception, {records.Length} records";
        }
        catch (Exception ex)
        {
            return $"{ex.GetType().Name}: {ex.Message} (LineNumber {engine.LineNumber}, TotalRecords {engine.TotalRecords})";
        }
    }

    [Test]
    [DisplayName("A record error thrown by ReadStream is not replaced by a fault of the reader while it is read to its end")]
    public async Task RecordError_WinsOverReaderFaultWhileDraining()
    {
        // Arrange: the bad record is in the first 16K-character block; the reader faults after it
        string input = "x,bad\r\n" + string.Concat(Enumerable.Range(1, 5_000).Select(i => $"{i},t\r\n"));
        var engine = new FileHelperEngine<SarPlain>();
        using var reader = new SarFaultingReader(input, faultAt: 20_000);

        // Act / Assert
        await Assert.That(() => engine.ReadStream(reader)).Throws<ConvertException>();
    }

    [Test]
    [DisplayName("A fault of the reader after all records were read propagates from ReadStream")]
    public async Task ReaderFault_AfterRecords_Propagates()
    {
        // Arrange: the cap stops the read inside the first block; draining the rest of the reader faults
        string input = string.Concat(Enumerable.Range(1, 5_000).Select(i => $"{i},t\r\n"));
        var engine = new FileHelperEngine<SarPlain>();
        using var reader = new SarFaultingReader(input, faultAt: 20_000);

        // Act / Assert
        await Assert.That(() => engine.ReadStream(reader, 10)).Throws<IOException>();
    }

    [Test]
    [DisplayName("Callback reads of a string (with and without IgnoreLast) give the records, footer and counters of the array read")]
    public async Task StringCallbackReads_MatchArrayRead()
    {
        int n = 0;
        foreach (string input in Inputs())
        {
            foreach (int maxRecords in MaxRecordValues)
            {
                n++;
                foreach (bool framed in new[] { false, true })
                {
                    string label = $"input {n}, maxRecords {maxRecords}, framed {framed}";

                    // Arrange / Act: the array read is the reference
                    (Outcome Array, Outcome Callback, Outcome Span) Run<T>(Func<T, string> show)
                        where T : class
                    {
                        var arrayEngine = new FileHelperEngine<T> { ErrorMode = ErrorMode.SaveAndContinue };
                        var array = Capture(arrayEngine, arrayEngine.ReadString(input, maxRecords), show);
                        var callbackEngine = new FileHelperEngine<T> { ErrorMode = ErrorMode.SaveAndContinue };
                        var collected = new List<T>();
                        callbackEngine.ReadString(input, collected.Add, maxRecords);
                        var callback = Capture(callbackEngine, collected.ToArray(), show);
                        var spanEngine = new FileHelperEngine<T> { ErrorMode = ErrorMode.SaveAndContinue };
                        var spanCollected = new List<string>();
                        spanEngine.ReadSpan(input.AsSpan(), (ref T r) => spanCollected.Add(show(r)), maxRecords);
                        var span = Capture(spanEngine, [], show) with { Records = string.Join("|", spanCollected) };
                        return (array, callback, span);
                    }

                    var (expected, callbackOutcome, spanOutcome) = framed
                        ? Run<SarFramed>(r => $"{r.Id}:{r.Text}")
                        : Run<SarPlain>(r => $"{r.Id}:{r.Text}");

                    // Assert
                    await Assert.That(callbackOutcome).IsEqualTo(expected).Because("Action callback, " + label);
                    await Assert.That(spanOutcome).IsEqualTo(expected).Because("span callback, " + label);
                }
            }
        }
    }

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
    [DisplayName("ReadStream of struct and class records gives the ReadString records at every chunk edge and past the pooled-bytes limit")]
    public async Task RecordCounts_AtChunkEdges_MatchReadString()
    {
        // Arrange: counts around the first chunk (64), the largest chunk (4096 structs of 16 bytes, 8192 references)
        // and past the 1 MB the sink gives back to the pool (65,536 structs, 131,072 references)
        int[] counts = [0, 1, 63, 64, 65, 4_095, 4_096, 4_097, 8_191, 8_192, 8_193, 70_000, 140_000];
        int[] caps = [0, 64, 4_097, 100_000];
        var structEngine = new FileHelperEngine<SarStruct>();
        var struct24Engine = new FileHelperEngine<SarStruct24>();
        var classEngine = new FileHelperEngine<SarPlain>();

        foreach (int count in counts)
        {
            string input = string.Concat(Enumerable.Range(1, count).Select(i => $"{i},t{i}\n"));
            string input24 = string.Concat(Enumerable.Range(1, count).Select(i => $"{i},t{i},m{i}\n"));
            foreach (int maxRecords in caps)
            {
                string label = $"{count} records, maxRecords {maxRecords}";

                // Act
                var expectedStructs = structEngine.ReadString(input, maxRecords);
                var structs = structEngine.ReadStream(new StringReader(input), maxRecords);
                var expectedStructs24 = struct24Engine.ReadString(input24, maxRecords);
                var structs24 = struct24Engine.ReadStream(new StringReader(input24), maxRecords);
                var expectedClasses = classEngine.ReadString(input, maxRecords).Select(r => $"{r.Id}:{r.Text}").ToArray();
                var classes = classEngine.ReadStream(new StringReader(input), maxRecords).Select(r => $"{r.Id}:{r.Text}").ToArray();

                // Assert
                await Assert.That(structs.Length).IsEqualTo(maxRecords > 0 ? Math.Min(count, maxRecords) : count).Because(label);
                await Assert.That(structs.SequenceEqual(expectedStructs)).IsTrue().Because("structs, " + label);
                await Assert.That(structs24.SequenceEqual(expectedStructs24)).IsTrue().Because("24-byte structs, " + label);
                await Assert.That(classes.SequenceEqual(expectedClasses)).IsTrue().Because("classes, " + label);
            }
        }
    }

    [Test]
    [DisplayName("After ReadStream of class records, the arrays it gave back to the pool hold no records")]
    public async Task ReadStream_PooledChunks_AreCleared()
    {
        // Arrange: more records than the largest chunk (8,192 references), so several chunk sizes go back
        string input = string.Concat(Enumerable.Range(1, 20_000).Select(i => $"{i},t{i}\n"));
        var engine = new FileHelperEngine<SarPlain>();

        // Act: read, then rent every chunk size on the same thread (the pool hands out this thread's arrays first)
        var records = engine.ReadStream(new StringReader(input));
        int leaked = 0;
        var rented = new List<SarPlain[]>();
        for (int size = 64; size <= 8_192; size *= 2)
        {
            for (int i = 0; i < 4; i++)
            {
                var array = System.Buffers.ArrayPool<SarPlain>.Shared.Rent(size);
                rented.Add(array);
                leaked += array.Count(r => r is not null);
            }
        }

        foreach (var array in rented)
            System.Buffers.ArrayPool<SarPlain>.Shared.Return(array, clearArray: true);

        // Assert
        await Assert.That(records.Length).IsEqualTo(20_000);
        await Assert.That(leaked).IsEqualTo(0);
    }

    [Test]
    [DisplayName("A ReadStream that fails after 10K records leaves the engine able to read the same input again")]
    public async Task ReadStream_AfterReaderFault_NextReadMatchesReadString()
    {
        // Arrange: the reader faults after about 12K records (the 16K-character fills, the chunks past the first ones)
        string input = string.Concat(Enumerable.Range(1, 30_000).Select(i => $"{i},t{i}\n"));
        var engine = new FileHelperEngine<SarStruct>();
        var expected = new FileHelperEngine<SarStruct>().ReadString(input);

        // Act
        Exception? fault = null;
        try
        {
            engine.ReadStream(new SarFaultingReader(input, faultAt: 120_000));
        }
        catch (IOException ex)
        {
            fault = ex;
        }

        var again = engine.ReadStream(new StringReader(input));

        // Assert
        await Assert.That(fault).IsNotNull();
        await Assert.That(again.SequenceEqual(expected)).IsTrue();
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
