// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using System.Globalization;
using System.Text;
using ModernLibs.FileHelpers.Tests.Engines.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// P5 (T24, design 5.1): <c>WriteString</c> of a 4.10 descriptor formats each record straight into the chunk of the
/// <see cref="PooledStringWriter"/>. Every write here is compared with the same write through
/// <see cref="EnginePaths.LegacyWriter"/>. The 4.10 descriptors are the hand-written <see cref="DirectWriterDescriptor{T}"/>
/// (ruling F29); <see cref="PooledStringWriter.RecordsFormattedInPlace"/> is read from a <c>WriteStream</c> into a
/// <see cref="PooledStringWriter"/>, which takes the path of <c>WriteString</c>.
/// </summary>
[Category("Engines")]
public sealed class WriteStringChunkTests
{
    /// <summary>The length of the first chunk of <see cref="PooledStringWriter"/>.</summary>
    private const int FirstChunk = 4 * 1024;

    private static readonly EnginePaths NewPaths = new(LegacyParse: false, ScalarTokenizer: false, LegacyWriter: false);
    private static readonly EnginePaths LegacyWriterPaths = new(LegacyParse: false, ScalarTokenizer: false, LegacyWriter: true);

    /// <summary>
    /// Records of 300 down to 1 chars, then 1 up to 300, after a header that leaves 0 to 302 chars free in the first
    /// chunk: the first record crosses the chunk end at every offset, its terminator included, and the text uses at
    /// least three chunks.
    /// </summary>
    [Test]
    public async Task Records_Crossing_A_Chunk_End_At_Every_Offset_Give_The_Same_String()
    {
        var descriptor = DirectFormats.Line();
        ChunkLine[] records = [.. Enumerable.Range(1, 300).Reverse().Concat(Enumerable.Range(1, 300)).Select(n => Line(n, n))];
        int newLine = Engine(descriptor, NewPaths).NewLineForWrite.Length;

        for (int free = 0; free <= 302; free++)
        {
            // WriteFrame adds the newline: the header fills the first chunk up to `free` chars.
            string header = new('h', FirstChunk - free - newLine);
            string expected = Engine(descriptor, LegacyWriterPaths, header).WriteString(records);
            var engine = Engine(descriptor, NewPaths, header);
            var (actual, inPlace) = WriteInPlace(engine, records);

            await Assert.That(actual).IsEqualTo(expected).Because($"{free} chars free");
            await Assert.That(inPlace).IsEqualTo(records.Length).Because($"{free} chars free");
            await Assert.That(engine.WriteString(records)).IsEqualTo(expected).Because($"WriteString, {free} chars free");
            await Assert.That(expected.Length).IsGreaterThan(FirstChunk + (2 * FirstChunk));
        }
    }

    [Test]
    public async Task A_Record_Longer_Than_The_Largest_Chunk_Is_Written_Whole()
    {
        var descriptor = DirectFormats.Line();
        ChunkLine[] records = [Line(10, 1), Line(1_200_000, 2), Line(5, 3)];

        string expected = Engine(descriptor, LegacyWriterPaths).WriteString(records);
        var engine = Engine(descriptor, NewPaths);
        var (actual, inPlace) = WriteInPlace(engine, records);

        await Assert.That(actual.Length).IsEqualTo(10 + 1_200_000 + 5 + (3 * engine.NewLineForWrite.Length));
        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(inPlace).IsEqualTo(3);
        await Assert.That(Engine(descriptor, NewPaths).WriteString(records)).IsEqualTo(expected);
    }

    [Test]
    public async Task With_AfterWriteRecord_No_Record_Is_Formatted_In_Place()
    {
        var descriptor = DirectFormats.Line();
        ChunkLine[] records = [.. Enumerable.Range(1, 300).Select(n => Line(n, n))];

        static FileHelperEngine<ChunkLine> Replacing(FileHelperEngine<ChunkLine> engine)
        {
            engine.AfterWriteRecord += (_, e) => e.RecordLine = e.RecordNumber % 2 == 0 ? "<" + e.RecordLine + ">" : e.RecordLine;
            return engine;
        }

        string expected = Replacing(Engine(descriptor, LegacyWriterPaths)).WriteString(records);
        var (actual, inPlace) = WriteInPlace(Replacing(Engine(descriptor, NewPaths)), records);

        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(actual).Contains("<");
        await Assert.That(inPlace).IsEqualTo(0);
        await Assert.That(descriptor.WriterFormatCalls).IsEqualTo(records.Length);
    }

    /// <summary>A <c>BeforeWriteRecord</c> handler that adds <c>AfterWriteRecord</c> at record 150: the records from there on raise it.</summary>
    [Test]
    public async Task AfterWriteRecord_Added_During_The_Write_Is_Raised_From_Then_On()
    {
        var descriptor = DirectFormats.Line();
        ChunkLine[] records = [.. Enumerable.Range(1, 300).Select(n => Line(n, n))];

        static FileHelperEngine<ChunkLine> Subscribing(FileHelperEngine<ChunkLine> engine)
        {
            engine.BeforeWriteRecord += (_, e) =>
            {
                if (e.RecordNumber == 150)
                    engine.AfterWriteRecord += (_, a) => a.RecordLine = "<" + a.RecordLine + ">";
            };
            return engine;
        }

        string expected = Subscribing(Engine(descriptor, LegacyWriterPaths)).WriteString(records);
        var (actual, inPlace) = WriteInPlace(Subscribing(Engine(descriptor, NewPaths)), records);

        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(actual.Count(c => c == '<')).IsEqualTo(151);
        await Assert.That(inPlace).IsEqualTo(149);
    }

    [Test]
    public async Task BeforeWriteRecord_Skip_Leaves_No_Partial_Record()
    {
        var descriptor = DirectFormats.Line();
        ChunkLine[] records = [.. Enumerable.Range(1, 300).Select(n => Line(n * 7, n))];

        static FileHelperEngine<ChunkLine> Skipping(FileHelperEngine<ChunkLine> engine)
        {
            // RecordNumber counts the written records, so the skip goes by the record: every third length.
            engine.BeforeWriteRecord += (_, e) => e.SkipThisRecord = e.Record.Text.Length % 21 == 0;
            return engine;
        }

        string expected = Skipping(Engine(descriptor, LegacyWriterPaths)).WriteString(records);
        var engine = Skipping(Engine(descriptor, NewPaths));
        var (actual, inPlace) = WriteInPlace(engine, records);

        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(inPlace).IsEqualTo(200);
        await Assert.That(engine.TotalRecords).IsEqualTo(200);
        await Assert.That(engine.WriteString(records)).IsEqualTo(expected);
    }

    /// <summary>
    /// Freeze note for T24: a record whose <c>Format</c> throws after its text has grown into a new chunk leaves
    /// nothing in the string, and the writer goes on after it.
    /// </summary>
    [Test]
    public async Task A_Record_That_Throws_Leaves_No_Partial_Record()
    {
        var plain = DirectFormats.Line();
        var throwing = new DirectWriterDescriptor<ChunkLine>(FileHelperRegistry<ChunkLine>.Descriptor, static (ChunkLine record, ref RecordWriter writer) =>
        {
            writer.Write(record.Text);
            if (record.Text.StartsWith('!'))
                throw new FormatException("bad record");
        });

        ChunkLine[] good = [.. Enumerable.Range(1, 60).Select(n => Line(n, n))];
        ChunkLine bad = new() { Text = "!" + new string('x', 3 * FirstChunk) };
        ChunkLine after = Line(20, 99);

        using var writer = new PooledStringWriter();
        var engine = Engine(throwing, NewPaths);
        await Assert.That(() => engine.WriteStream(writer, [.. good, bad, after])).ThrowsExactly<FormatException>();
        await Assert.That(writer.ToString()).IsEqualTo(Engine(plain, LegacyWriterPaths).WriteString(good));

        engine.WriteStream(writer, [after]);
        await Assert.That(writer.ToString()).IsEqualTo(Engine(plain, LegacyWriterPaths).WriteString([.. good, after]));
        await Assert.That(writer.RecordsFormattedInPlace).IsEqualTo(good.Length + 1);
    }

    [Test]
    public async Task MultiRecord_And_MasterDetail_WriteString_Give_The_Same_String()
    {
        var line = DirectFormats.Line();
        var pair = DirectFormats.Pair();
        object[] mixed = [.. Enumerable.Range(1, 600).Select(n => n % 3 == 0 ? (object)Pair(n) : Line(n % 300, n))];
        MasterDetails<ChunkPair, ChunkLine>[] groups =
            [.. Enumerable.Range(1, 60).Select(n => new MasterDetails<ChunkPair, ChunkLine>(Pair(n), [.. Enumerable.Range(1, n % 7).Select(d => Line(d * n, d))]))];

        string MultiRecord(EnginePaths paths) =>
            new MultiRecordEngine(
                new MultiRecordType<ChunkLine>(static _ => true, line, paths),
                new MultiRecordType<ChunkPair>(static _ => false, pair, paths)).WriteString(mixed);

        string MasterDetail(EnginePaths paths) =>
            new MasterDetailEngine<ChunkPair, ChunkLine>(static _ => RecordAction.Master, pair, line, paths).WriteString(groups);

        string multiExpected = MultiRecord(LegacyWriterPaths);
        string masterExpected = MasterDetail(LegacyWriterPaths);
        await Assert.That(line.WriterFormatCalls + pair.WriterFormatCalls).IsEqualTo(0);

        await Assert.That(MultiRecord(NewPaths)).IsEqualTo(multiExpected);
        await Assert.That(MasterDetail(NewPaths)).IsEqualTo(masterExpected);
        await Assert.That(multiExpected.Length).IsGreaterThan(3 * FirstChunk);
        await Assert.That(masterExpected.Length).IsGreaterThan(3 * FirstChunk);
        await Assert.That(line.WriterFormatCalls + pair.WriterFormatCalls).IsGreaterThan(0);

        // MasterDetailEngine.WriteString hands its PooledStringWriter to the record engines: every record goes in place.
        using var writer = new PooledStringWriter();
        new MasterDetailEngine<ChunkPair, ChunkLine>(static _ => RecordAction.Master, pair, line, NewPaths).WriteStream(writer, groups);
        await Assert.That(writer.ToString()).IsEqualTo(masterExpected);
        await Assert.That(writer.RecordsFormattedInPlace).IsEqualTo(groups.Sum(g => 1 + g.Details.Count));
    }

    /// <summary>
    /// A record that uses <see cref="RecordWriter.Quote"/>, <see cref="RecordWriter.Pad"/> and
    /// <see cref="RecordWriter.WriteNewLine"/> with the newline <c>"|\n"</c>, after a header that leaves 0 to 80 chars
    /// free: the chunk end falls inside the quoted field, the padded field and the line breaks, so each of them moves
    /// the record to a new chunk. The text equals the hand-built one and the T22 path through
    /// <see cref="RentedBufferWriter"/>.
    /// </summary>
    [Test]
    public async Task Quote_Pad_And_NewLine_Across_A_Chunk_End_Give_The_Same_String()
    {
        const string newLine = "|\n";
        var descriptor = new DirectWriterDescriptor<ChunkLine>(FileHelperRegistry<ChunkLine>.Descriptor, static (ChunkLine record, ref RecordWriter writer) =>
        {
            int start = writer.Position;
            writer.Write(record.Text);
            writer.Quote(start, '"', always: true, multilineWrite: false, ",", "Text");
            writer.Write(',');
            int padStart = writer.Position;
            writer.Write(record.Text.Length.ToString(CultureInfo.InvariantCulture));
            writer.Pad(padStart, 8, 2, '0', overflowError: false, "Length");
            writer.WriteNewLine();
            writer.Write("end");
        });

        ChunkLine[] records = [.. Enumerable.Range(1, 40).Select(n => new ChunkLine { Text = Line(n, n).Text.Replace('e', '"') })];
        string body = string.Concat(records.Select(r =>
            "\"" + r.Text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"," + r.Text.Length.ToString("D8", CultureInfo.InvariantCulture) + newLine + "end" + newLine));

        for (int free = 0; free <= 80; free++)
        {
            string header = new('h', FirstChunk - free - newLine.Length);
            var engine = new FileHelperEngine<ChunkLine>(descriptor, Encoding.UTF8, NewPaths) { HeaderText = header, NewLineForWrite = newLine };
            var (actual, inPlace) = WriteInPlace(engine, records);

            using var t22Path = new StringWriter();
            engine.WriteStream(t22Path, records);

            await Assert.That(actual).IsEqualTo(header + newLine + body).Because($"{free} chars free");
            await Assert.That(t22Path.ToString()).IsEqualTo(actual).Because($"{free} chars free");
            await Assert.That(inPlace).IsEqualTo(records.Length);
        }
    }

    [Test]
    public async Task WriteString_Allocates_No_Per_Record_Buffer()
    {
        var descriptor = DirectFormats.Pair();
        ChunkPair[] records = [.. Enumerable.Range(1, 10_000).Select(Pair)];
        var engine = Engine(descriptor, NewPaths);
        var legacyEngine = Engine(descriptor, LegacyWriterPaths);

        // Warm up: the pool then holds the chunks.
        string text = engine.WriteString(records);
        await Assert.That(legacyEngine.WriteString(records)).IsEqualTo(text);

        int length = text.Length;
        long result = AllocationProbe.MinAllocated(() => _ = new string('x', length));
        long allocated = AllocationProbe.MinAllocated(() => _ = engine.WriteString(records)) - result;
        long legacyAllocated = AllocationProbe.MinAllocated(() => _ = legacyEngine.WriteString(records)) - result;

        // The real guard: any per-record allocation is at least 24 B x 10,000 rows.
        await Assert.That(allocated).IsLessThan(64 * 1024).Because($"{allocated} B; LegacyWriter {legacyAllocated} B");

        // The brief's second bound. Today the difference is one object: the RentedBufferWriter the legacy loop creates
        // per call and the in-place loop does not (40 B). A legacy loop that creates it lazily fails this assert.
        await Assert.That(allocated).IsLessThan(legacyAllocated).Because($"{allocated} B; LegacyWriter {legacyAllocated} B");

        using var writer = new PooledStringWriter();
        engine.WriteStream(writer, records);
        await Assert.That(writer.RecordsFormattedInPlace).IsEqualTo(records.Length);
    }

    private static FileHelperEngine<T> Engine<T>(IRecordDescriptor<T> descriptor, EnginePaths paths, string header = "") =>
        new(descriptor, Encoding.UTF8, paths) { HeaderText = header };

    /// <summary>The text and the in-place count of a <c>WriteStream</c> into a <see cref="PooledStringWriter"/>.</summary>
    private static (string Text, int InPlace) WriteInPlace<T>(FileHelperEngine<T> engine, T[] records)
    {
        using var writer = new PooledStringWriter();
        engine.WriteStream(writer, records);
        return (writer.ToString(), writer.RecordsFormattedInPlace);
    }

    /// <summary>A <see cref="ChunkLine"/> of <paramref name="length"/> letters.</summary>
    private static ChunkLine Line(int length, int seed)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = (char)('a' + ((seed + i) % 26));
        return new ChunkLine { Text = new string(chars) };
    }

    private static ChunkPair Pair(int id) => new() { Id = id, Name = "name " + id };
}
