// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional; groups related engine read stubs.
#pragma warning disable MA0048
// CA1849: Synchronous file I/O is appropriate in tests (small fixtures).
#pragma warning disable CA1849
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// GREEN acceptance tests for <see cref="FileHelperEngine{T}"/> read operations
/// (ENGI-01, ENGI-02, ENGI-03).
/// </summary>
[Category("Phase07")]
[Category("Engine")]
public sealed class EngineReadTests
{
    /// <summary>ENGI-03: ReadString parses a delimited record from an in-memory string.</summary>
    [Test]
    public async Task ReadString_DelimitedRecord_ParsesCorrectly()
    {
        // CsvOrder has [IgnoreFirst(1)] — first line is treated as a header.
        var engine = new FileHelperEngine<CsvOrder>();
        var input = "Header,Header,Header\r\n1,Alice,100.50\r\n2,Bob,200.00";
        var records = engine.ReadString(input);

        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].OrderId).IsEqualTo(1);
        await Assert.That(records[0].CustomerName).IsEqualTo("Alice");
        await Assert.That(records[0].Amount).IsEqualTo(100.50m);
        await Assert.That(records[1].OrderId).IsEqualTo(2);
        await Assert.That(records[1].CustomerName).IsEqualTo("Bob");
        await Assert.That(records[1].Amount).IsEqualTo(200.00m);
    }

    /// <summary>ENGI-03 + D-11: ReadString with maxRecords stops after N valid records.</summary>
    [Test]
    public async Task ReadString_WithMaxRecords_StopsEarly()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        // Header + 5 data rows; ask for max 2.
        var input = "Header,Header,Header\r\n1,A,1.00\r\n2,B,2.00\r\n3,C,3.00\r\n4,D,4.00\r\n5,E,5.00";
        var records = engine.ReadString(input, maxRecords: 2);

        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].OrderId).IsEqualTo(1);
        await Assert.That(records[1].OrderId).IsEqualTo(2);
    }

    /// <summary>D-08 + D-10: [IgnoreFirst] skips header lines and populates HeaderText.</summary>
    [Test]
    public async Task ReadString_WithIgnoreFirst_SkipsHeader()
    {
        var engine = new FileHelperEngine<CsvOrder>();
        var input = "HEADER_LINE\r\n1,Alice,100.50\r\n2,Bob,200.00";
        var records = engine.ReadString(input);

        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(engine.HeaderText).IsEqualTo("HEADER_LINE");
    }

    /// <summary>D-08 + D-10: [IgnoreLast] strips footer lines and populates FooterText.</summary>
    [Test]
    public async Task ReadString_WithIgnoreLast_CapturesFooter()
    {
        var engine = new FileHelperEngine<FooteredRecord>();
        var input = "1,Alice\r\n2,Bob\r\nFOOTER_LINE";
        var records = engine.ReadString(input);

        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Id).IsEqualTo(1);
        await Assert.That(records[1].Id).IsEqualTo(2);
        await Assert.That(engine.FooterText).IsEqualTo("FOOTER_LINE");
    }

    /// <summary>D-08: [IgnoreEmptyLines] causes blank input lines to be skipped.</summary>
    [Test]
    public async Task ReadString_WithIgnoreEmptyLines_SkipsEmpty()
    {
        var engine = new FileHelperEngine<CommentedRecord>();
        var input = "1,first\r\n\r\n2,second\r\n\r\n3,third";
        var records = engine.ReadString(input);

        await Assert.That(records.Length).IsEqualTo(3);
        await Assert.That(records[0].Id).IsEqualTo(1);
        await Assert.That(records[1].Id).IsEqualTo(2);
        await Assert.That(records[2].Id).IsEqualTo(3);
    }

    /// <summary>D-08: [IgnoreCommentedLines('#')] skips comment lines during parse.</summary>
    [Test]
    public async Task ReadString_WithCommentMarker_SkipsCommented()
    {
        var engine = new FileHelperEngine<CommentedRecord>();
        var input = "# top comment\r\n1,alpha\r\n# mid comment\r\n2,beta\r\n3,gamma";
        var records = engine.ReadString(input);

        await Assert.That(records.Length).IsEqualTo(3);
        await Assert.That(records[0].Value).IsEqualTo("alpha");
        await Assert.That(records[1].Value).IsEqualTo("beta");
        await Assert.That(records[2].Value).IsEqualTo("gamma");
    }

    /// <summary>ENGI-01: ReadFile(path) reads all records from disk into a T[] array.</summary>
    [Test]
    public async Task ReadFile_DelimitedRecord_ReturnsArray()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"fh_read_{Guid.NewGuid():N}.csv");
        try
        {
            await File.WriteAllTextAsync(tempPath, "HEADER\r\n10,Xavier,50.25\r\n20,Yvonne,75.75");
            var engine = new FileHelperEngine<CsvOrder>();
            var records = engine.ReadFile(tempPath);

            await Assert.That(records.Length).IsEqualTo(2);
            await Assert.That(records[0].OrderId).IsEqualTo(10);
            await Assert.That(records[0].CustomerName).IsEqualTo("Xavier");
            await Assert.That(records[1].OrderId).IsEqualTo(20);
            await Assert.That(records[1].Amount).IsEqualTo(75.75m);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    /// <summary>ENGI-01: ReadFile(path) works for a [FixedLengthRecord] type.</summary>
    [Test]
    public async Task ReadFile_FixedLength_ReturnsArray()
    {
        // FixedEmployee: Id=6, Name=20, Salary=10 — total 36 chars.
        // "     1" + "Alice               " + "   1500.50"
        var line1 = "     1" + "Alice".PadRight(20) + "1500.50".PadLeft(10);
        var line2 = "     2" + "Bob".PadRight(20) + "2500.75".PadLeft(10);
        var content = line1 + "\r\n" + line2;

        var tempPath = Path.Combine(Path.GetTempPath(), $"fh_read_fixed_{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(tempPath, content);
            var engine = new FileHelperEngine<FixedEmployee>();
            var records = engine.ReadFile(tempPath);

            await Assert.That(records.Length).IsEqualTo(2);
            await Assert.That(records[0].Id).IsEqualTo(1);
            await Assert.That(records[0].Name.Trim()).IsEqualTo("Alice");
            await Assert.That(records[0].Salary).IsEqualTo(1500.50m);
            await Assert.That(records[1].Id).IsEqualTo(2);
            await Assert.That(records[1].Salary).IsEqualTo(2500.75m);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    /// <summary>ENGI-02: ReadStream(TextReader) parses records from a live reader.</summary>
    [Test]
    public async Task ReadStream_FromTextReader_ParsesCorrectly()
    {
        var input = "HEADER\r\n100,StreamRecord,999.99";
        using var reader = new StringReader(input);
        var engine = new FileHelperEngine<CsvOrder>();
        var records = engine.ReadStream(reader);

        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].OrderId).IsEqualTo(100);
        await Assert.That(records[0].CustomerName).IsEqualTo("StreamRecord");
        await Assert.That(records[0].Amount).IsEqualTo(999.99m);
    }

    /// <summary>ENGI-03: ReadString parses a fixed-length record from an in-memory string.</summary>
    [Test]
    public async Task ReadString_FixedLengthRecord_ParsesCorrectly()
    {
        var line1 = "    42" + "FixedName".PadRight(20) + "500.00".PadLeft(10);
        var line2 = "    43" + "Another".PadRight(20) + "600.00".PadLeft(10);
        var input = line1 + "\r\n" + line2;

        var engine = new FileHelperEngine<FixedEmployee>();
        var records = engine.ReadString(input);

        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Id).IsEqualTo(42);
        await Assert.That(records[0].Name.Trim()).IsEqualTo("FixedName");
        await Assert.That(records[0].Salary).IsEqualTo(500.00m);
        await Assert.That(records[1].Id).IsEqualTo(43);
    }
}
