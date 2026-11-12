// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.MultiRecord;

/// <summary>A header that skips itself for even batches and appends its record number when written.</summary>
[DelimitedRecord("|")]
public sealed class MwHeader : INotifyWrite<MwHeader>
{
    [FieldOrder(1)] public string Tag { get; set; } = "H";
    [FieldOrder(2)] public int Batch { get; set; }

    public void BeforeWrite(BeforeWriteEventArgs<MwHeader> e)
    {
        if (Batch % 2 == 0)
            e.SkipThisRecord = true;
    }

    public void AfterWrite(AfterWriteEventArgs<MwHeader> e) => e.RecordLine = e.RecordLine + "#" + e.RecordNumber;
}

[DelimitedRecord("|")]
public sealed class MwDetail
{
    [FieldOrder(1)] public string Tag { get; set; } = "D";
    [FieldOrder(2)] public int Id { get; set; }
    [FieldOrder(3)] public int Amount { get; set; }
}

[DelimitedRecord("|")]
public sealed class MwSplit
{
    [FieldOrder(1)] public string Tag { get; set; } = "S";
    [FieldOrder(2)] public int Id { get; set; }
    [FieldOrder(3)] [FieldInNewLine] public string Note { get; set; } = string.Empty;
}

/// <summary>A custom record type: the engine formats it through <see cref="IMultiRecordType.Format"/>.</summary>
public sealed class MwCustomType : IMultiRecordType
{
    public bool Matches(ReadOnlySpan<char> line) => line.StartsWith("C:", StringComparison.Ordinal);

    public object Parse(ReadOnlySpan<char> line) => line[2..].ToString();

    public bool CanFormat(object record) => record is string;

    public string Format(object record) => "C:" + record;
}

/// <summary>
/// <see cref="MultiRecordEngine"/> writes: <see cref="INotifyWrite{T}"/> skips and rewrites, the record number
/// each record sees, <see cref="MultiRecordEngine.NewLineForWrite"/> inside and after records, custom types.
/// </summary>
[Category("MultiRecord")]
public sealed class MultiRecordWriteTests
{
    private static MultiRecordEngine Engine() => new(
        MultiRecordType.For<MwHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
        MultiRecordType.For<MwDetail>(line => line.StartsWith("D|", StringComparison.Ordinal)),
        MultiRecordType.For<MwSplit>(line => line.StartsWith("S|", StringComparison.Ordinal)),
        new MwCustomType());

    private static object[] Records() =>
    [
        .. Enumerable.Range(1, 4).SelectMany(i => new object[] { new MwHeader { Batch = i }, new MwDetail { Id = i, Amount = i * 10 } }),
        "custom",
    ];

    [Test]
    [DisplayName("INotifyWrite: a skipped record leaves an empty line, a rewritten one is written as rewritten, and each record is number 1")]
    public async Task NotifyWrite_SkipAndRewrite()
    {
        // Arrange: the 4.8.0 output below was written with "\r\n"
        var engine = Engine();
        engine.NewLineForWrite = "\r\n";

        // Act
        string text = engine.WriteString(Records());

        // Assert: 4.8.0 output
        await Assert.That(text).IsEqualTo("H|1#1\r\nD|1|10\r\n\r\nD|2|20\r\nH|3#1\r\nD|3|30\r\n\r\nD|4|40\r\nC:custom\r\n");
    }

    [Test]
    [DisplayName("WriteString, WriteStream and WriteFile write the same text")]
    public async Task WriteOverloads_Agree()
    {
        // Arrange
        var engine = Engine();
        var records = Records();
        var path = Path.GetTempFileName();
        try
        {
            // Act
            string text = engine.WriteString(records);
            using var writer = new StringWriter();
            engine.WriteStream(writer, records);
            engine.WriteFile(path, records);

            // Assert
            await Assert.That(writer.ToString()).IsEqualTo(text);
            await Assert.That(await File.ReadAllTextAsync(path)).IsEqualTo(text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    [DisplayName("NewLineForWrite separates the lines of a [FieldInNewLine] record and ends every record")]
    public async Task NewLineForWrite_InsideAndAfterRecords()
    {
        // Arrange: the single-type engine is the reference for the split record
        var engine = Engine();
        engine.NewLineForWrite = "\n";
        var split = new MwSplit { Id = 7, Note = "note" };
        string splitText = new FileHelperEngine<MwSplit> { NewLineForWrite = "\n" }.WriteString([split]);

        // Act
        string text = engine.WriteString([new MwDetail { Id = 1, Amount = 2 }, split, "c"]);

        // Assert
        await Assert.That(splitText).IsEqualTo("S|7|\nnote\n");
        await Assert.That(text).IsEqualTo("D|1|2\n" + splitText + "C:c\n");
    }

    [Test]
    [DisplayName("Writing many records allocates far less than one string per record")]
    public async Task WriteStream_AllocatesLittlePerRecord()
    {
        // Arrange: 10K details written to a writer that keeps nothing
        var engine = Engine();
        var records = Enumerable.Range(1, 10_000).Select(i => (object)new MwDetail { Id = i, Amount = i }).ToArray();
        engine.WriteStream(TextWriter.Null, records);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        engine.WriteStream(TextWriter.Null, records);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert: 4.8.0 built a writer and two strings per record (about 400 B each)
        await Assert.That(allocated).IsLessThan(10_000 * 40).Because($"{allocated} B for 10K records");
    }
}
