// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using System.Text;
using ModernLibs.FileHelpers.Tests.MasterDetail;
using ModernLibs.FileHelpers.Tests.MultiRecord;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

[DelimitedRecord(",")]
public sealed class PswLine
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldQuoted] public string Text { get; set; } = string.Empty;
}

/// <summary>
/// The pooled writer behind the <c>WriteString</c> methods returns exactly the text written, across chunk
/// boundaries, and the engines' <c>WriteString</c> output equals their <c>WriteStream</c> output to a
/// <see cref="StringWriter"/>.
/// </summary>
[Category("Write")]
public sealed class PooledStringWriterTests
{
    private static string Text(int length, int seed)
    {
        var random = new Random(seed);
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = (char)random.Next(' ', 'ż');
        return new string(chars);
    }

    [Test]
    [DisplayName("An empty writer gives an empty string")]
    public async Task Empty()
    {
        using var writer = new PooledStringWriter();
        await Assert.That(writer.ToString()).IsEqualTo(string.Empty);
    }

    [Test]
    [DisplayName("Every Write overload, across many chunk boundaries, gives the text a StringBuilder gives")]
    public async Task WriteOverloads_CrossChunks()
    {
        // Arrange
        var expected = new StringBuilder();
        using var writer = new PooledStringWriter();
        var random = new Random(7);

        // Act: single chars (crossing the first chunk), strings, arrays, spans, nulls and one large block
        for (int i = 0; i < 10_000; i++)
        {
            char c = (char)('a' + (i % 26));
            writer.Write(c);
            expected.Append(c);
        }

        for (int i = 0; i < 2_000; i++)
        {
            string piece = Text(random.Next(0, 300), i);
            switch (i % 4)
            {
                case 0: writer.Write(piece); break;
                case 1: writer.Write(piece.ToCharArray(), 0, piece.Length); break;
                case 2: writer.Write(piece.AsSpan()); break;
                default: writer.WriteLine(piece); piece += writer.NewLine; break;
            }

            expected.Append(piece);
        }

        writer.Write((string?)null);
        string large = Text(3_000_000, 99);
        writer.Write(large);
        expected.Append(large);
        writer.Write("end");
        expected.Append("end");

        // Assert: the same text, and ToString can be called again
        string first = writer.ToString();
        await Assert.That(first).IsEqualTo(expected.ToString());
        await Assert.That(writer.ToString()).IsEqualTo(first);
    }

    [Test]
    [DisplayName("A disposed writer refuses writes and ToString; disposing twice is allowed")]
    public async Task Disposed_Throws()
    {
        // Arrange
        var writer = new PooledStringWriter();
        writer.Write("abc");

        // Act
        writer.Dispose();
        writer.Dispose();

        // Assert
        await Assert.That(() => writer.ToString()).Throws<ObjectDisposedException>();
        await Assert.That(() => writer.Write('x')).Throws<ObjectDisposedException>();
        await Assert.That(() => writer.Write("x")).Throws<ObjectDisposedException>();
    }

    [Test]
    [DisplayName("FileHelperEngine.WriteString equals WriteStream to a StringWriter, with header, footer and 0, 1 or 20K records")]
    public async Task FileHelperEngine_WriteString_MatchesWriteStream()
    {
        foreach (int count in new[] { 0, 1, 20_000 })
        {
            // Arrange
            var engine = new FileHelperEngine<PswLine> { HeaderText = "id,text", FooterText = "end" };
            var records = Enumerable.Range(1, count).Select(i => new PswLine { Id = i, Text = Text(i % 120, i) + "\"," }).ToArray();
            using var reference = new StringWriter();
            engine.WriteStream(reference, records);

            // Act
            string actual = engine.WriteString(records);

            // Assert
            await Assert.That(actual).IsEqualTo(reference.ToString()).Because($"{count} records");
        }
    }

    [Test]
    [DisplayName("FileHelperEngine.WriteString allocates the result string and little else")]
    public async Task FileHelperEngine_WriteString_AllocatesTheResultOnce()
    {
        // Arrange: 20K records; warm up so the pool holds the chunks
        var engine = new FileHelperEngine<PswLine>();
        var records = Enumerable.Range(1, 20_000).Select(i => new PswLine { Id = i, Text = "text " + i }).ToArray();
        _ = engine.WriteString(records);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        string text = engine.WriteString(records);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert: a StringWriter allocated the text twice (builder chunks, then the result)
        long resultBytes = 2L * text.Length;
        await Assert.That(allocated).IsLessThan(resultBytes * 13 / 10).Because($"{allocated} B for a {resultBytes} B result");
    }

    [Test]
    [DisplayName("MultiRecordEngine and MasterDetailEngine WriteString equal their WriteStream to a StringWriter")]
    public async Task MultiAndMasterDetail_WriteString_MatchesWriteStream()
    {
        // Arrange
        var multi = new MultiRecordEngine(
            MultiRecordType.For<MultiHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<MultiDetail>(line => line.StartsWith("D|", StringComparison.Ordinal)));
        var mixed = multi.ReadString(string.Concat(Enumerable.Range(1, 5_000).Select(i => $"H|B{i:000}\nD|{i}|{i}.5\n")));
        var masterDetail = new MasterDetailEngine<MasterRecord, DetailRecord>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master : RecordAction.Detail);
        var groups = masterDetail.ReadString(string.Concat(Enumerable.Range(1, 5_000).Select(i => $"M|A{i}\nD|A{i}|{i}\nD|A{i}|{i + 1}\n")));
        using var multiReference = new StringWriter();
        multi.WriteStream(multiReference, mixed);
        using var masterDetailReference = new StringWriter();
        masterDetail.WriteStream(masterDetailReference, groups);

        // Act
        string multiText = multi.WriteString(mixed);
        string masterDetailText = masterDetail.WriteString(groups);

        // Assert
        await Assert.That(mixed.Length).IsEqualTo(10_000);
        await Assert.That(multiText).IsEqualTo(multiReference.ToString());
        await Assert.That(groups.Length).IsEqualTo(5_000);
        await Assert.That(masterDetailText).IsEqualTo(masterDetailReference.ToString());
    }

    [Test]
    [DisplayName("FileHelperAsyncEngine BeginWriteString / WriteNext / EndWriteString returns the records and the footer; after Close it returns an empty string")]
    public async Task AsyncEngine_EndWriteString()
    {
        // Arrange
        var engine = new FileHelperAsyncEngine<PswLine> { HeaderText = "id,text", FooterText = "end" };
        var records = Enumerable.Range(1, 3_000).Select(i => new PswLine { Id = i, Text = Text(i % 50, i) }).ToArray();
        string expected = new FileHelperEngine<PswLine> { HeaderText = "id,text", FooterText = "end" }.WriteString(records);

        // Act
        engine.BeginWriteString();
        foreach (var record in records)
            engine.WriteNext(record);
        string written = engine.EndWriteString();

        engine.BeginWriteString();
        engine.WriteNext(records[0]);
        engine.Close();
        string afterClose = engine.EndWriteString();

        engine.BeginWriteString();
        string empty = engine.EndWriteString();

        // Assert
        await Assert.That(written).IsEqualTo(expected);
        await Assert.That(afterClose).IsEqualTo(string.Empty);
        await Assert.That(empty).IsEqualTo(new FileHelperEngine<PswLine> { HeaderText = "id,text", FooterText = "end" }.WriteString([]));
    }
}
