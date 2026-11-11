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

using System.Text;
using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

[Category("Engine")]
public sealed class StreamReadTests
{
    // CsvOrder: [IgnoreFirst(1)], so the first line is the header.
    private const string Csv = "id,name,amount\r\n1,a,1.5\r\n2,b,2.5\r\n";

    private static byte[] Encode(Encoding encoding, string text, bool withBom) =>
        withBom ? [.. encoding.GetPreamble(), .. encoding.GetBytes(text)] : encoding.GetBytes(text);

    /// <summary>A readable stream that is not seekable and throws on every length or position access.</summary>
    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _inner.Read(buffer);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static async Task AssertSame(CsvOrder[] actual, CsvOrder[] expected)
    {
        await Assert.That(actual.Length).IsEqualTo(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            await Assert.That(actual[i].OrderId).IsEqualTo(expected[i].OrderId);
            await Assert.That(actual[i].CustomerName).IsEqualTo(expected[i].CustomerName);
            await Assert.That(actual[i].Amount).IsEqualTo(expected[i].Amount);
        }
    }

    [Test]
    [Arguments("utf-8", true)]
    [Arguments("utf-8", false)]
    [Arguments("utf-16", true)]
    [Arguments("utf-16BE", true)]
    public async Task Reads_The_Same_Records_As_ReadString(string encodingName, bool withBom)
    {
        var engine = new FileHelperEngine<CsvOrder> { Encoding = new UTF8Encoding(false) };
        var bytes = Encode(Encoding.GetEncoding(encodingName), Csv, withBom);

        var records = engine.ReadStream(new MemoryStream(bytes));

        var twin = new FileHelperEngine<CsvOrder>();
        var expected = twin.ReadString(Csv);
        await Assert.That(records.Length).IsEqualTo(2);
        await AssertSame(records, expected);
        await Assert.That(engine.HeaderText).IsEqualTo(twin.HeaderText);
        await Assert.That(engine.HeaderText).IsNotNull();
    }

    [Test]
    public async Task Stream_Stays_Open_And_Is_At_Its_End()
    {
        var stream = new MemoryStream(Encode(Encoding.UTF8, Csv, withBom: false));

        new FileHelperEngine<CsvOrder>().ReadStream(stream);

        await Assert.That(stream.CanRead).IsTrue();
        await Assert.That(stream.Position).IsEqualTo(stream.Length);
    }

    [Test]
    public async Task Empty_Stream_Gives_An_Empty_Array()
        => await Assert.That(new FileHelperEngine<CsvOrder>().ReadStream(new MemoryStream()).Length).IsEqualTo(0);

    [Test]
    public async Task Reads_From_The_Current_Position()
    {
        const string Garbage = "garbage\r\n";
        var stream = new MemoryStream(Encode(Encoding.UTF8, Garbage + Csv, withBom: false))
        {
            Position = Garbage.Length,
        };

        var records = new FileHelperEngine<CsvOrder>().ReadStream(stream);

        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].CustomerName).IsEqualTo("a");
    }

    [Test]
    public async Task Byte_Order_Mark_Wins_Over_The_Engine_Encoding()
    {
        var engine = new FileHelperEngine<CsvOrder> { Encoding = Encoding.Latin1 };
        var bytes = Encode(Encoding.UTF8, "h\r\n1,é,1.5\r\n", withBom: true);

        var records = engine.ReadStream(new MemoryStream(bytes));

        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].CustomerName).IsEqualTo("é");
    }

    [Test]
    public async Task Engine_Encoding_Applies_Without_A_Byte_Order_Mark()
    {
        var engine = new FileHelperEngine<CsvOrder> { Encoding = Encoding.Latin1 };
        var bytes = Encoding.Latin1.GetBytes("h\r\n1,é,1.5\r\n");

        var records = engine.ReadStream(new MemoryStream(bytes));

        await Assert.That(records[0].CustomerName).IsEqualTo("é");
    }

    [Test]
    public async Task Non_Seekable_Stream_Works()
    {
        var stream = new NonSeekableStream(Encode(Encoding.UTF8, Csv, withBom: false));

        var records = new FileHelperEngine<CsvOrder>().ReadStream(stream);

        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(stream.CanRead).IsTrue();
    }

    [Test]
    public async Task MaxRecords_Stops_Early_But_Reads_To_The_End()
    {
        var stream = new MemoryStream(Encode(Encoding.UTF8, Csv, withBom: false));

        var records = new FileHelperEngine<CsvOrder>().ReadStream(stream, 1);

        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(stream.Position).IsEqualTo(stream.Length);
    }

    [Test]
    public async Task Null_Stream_Throws_ArgumentNullException()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        await Assert.That(() => engine.ReadStream((Stream)null!)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => engine.ReadStream((Stream)null!, 1)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Interface_Call_On_The_Engine_Reaches_The_Class_Method()
    {
        IFileHelperEngine<CsvOrder> engine = new FileHelperEngine<CsvOrder>();
        var stream = new MemoryStream(Encode(Encoding.UTF8, Csv, withBom: false));

        await Assert.That(engine.ReadStream(stream).Length).IsEqualTo(2);
        stream.Position = 0;
        await Assert.That(engine.ReadStream(stream, 1).Length).IsEqualTo(1);
    }

    [Test]
    public async Task Interface_Default_Works_For_A_Foreign_Implementation()
    {
        IFileHelperEngine<CsvOrder> fake = new ArrayOnlyEngine();
        var stream = new MemoryStream(Encode(Encoding.UTF8, "x", withBom: false));

        await Assert.That(fake.ReadStream(stream).Length).IsEqualTo(2);
        await Assert.That(fake.ReadStream(stream, 1).Length).IsEqualTo(1);
        await Assert.That(stream.CanRead).IsTrue();
        await Assert.That(() => fake.ReadStream((Stream)null!)).ThrowsExactly<ArgumentNullException>();
    }
}
