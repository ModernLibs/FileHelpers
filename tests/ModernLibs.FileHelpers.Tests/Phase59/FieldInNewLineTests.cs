// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional; groups related tests.
#pragma warning disable MA0048
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1861: Prefer static readonly over constant array args.
#pragma warning disable CA1861

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase59;

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class MultiNewLineFixedRecord
{
    [FieldFixedLength(4)]
    public string Code { get; set; } = "";

    [FieldFixedLength(6)]
    [FieldInNewLine]
    public string Name { get; set; } = "";

    [FieldFixedLength(5)]
    [FieldInNewLine]
    public string Value { get; set; } = "";
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class TrimNewLineRecord
{
    [FieldFixedLength(6)]
    [FieldTrim(TrimMode.Both)]
    public string Code { get; set; } = "";

    [FieldFixedLength(8)]
    [FieldInNewLine]
    [FieldTrim(TrimMode.Right)]
    public string Description { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class MultiNewLineDelimitedRecord
{
    [FieldOrder(1)]
    public int Id { get; set; }

    [FieldOrder(2)]
    [FieldInNewLine]
    public string Description { get; set; } = "";

    [FieldOrder(3)]
    [FieldInNewLine]
    public string Notes { get; set; } = "";
}

[Category("Phase59")]
[Category("FieldInNewLine")]
public sealed class FieldInNewLineTests
{
    [Test]
    [DisplayName("Fixed-width: three physical lines join into one logical record")]
    public async Task FixedWidth_ThreeLines_JoinIntoOneRecord()
    {
        // Arrange: Three physical lines per logical record (Code=4, Name=6, Value=5)
        var engine = new FileHelperEngine<MultiNewLineFixedRecord>();
        var input = "CODE\r\nNAME00\r\nVALUE";

        // Act: Parse multi-line records
        var records = engine.ReadString(input);

        // Assert: One logical record parsed correctly
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Code).IsEqualTo("CODE");
        await Assert.That(records[0].Name).IsEqualTo("NAME00");
        await Assert.That(records[0].Value).IsEqualTo("VALUE");
    }

    [Test]
    [DisplayName("Fixed-width: two logical records from six physical lines")]
    public async Task FixedWidth_TwoRecords_SixLines()
    {
        // Arrange: Six physical lines = two logical records
        var engine = new FileHelperEngine<MultiNewLineFixedRecord>();
        var input = "ABCD\r\nEFGHIJ\r\nKLMNO\r\nWXYZ\r\nNAME00\r\nVAL99";

        // Act: Parse
        var records = engine.ReadString(input);

        // Assert: Two logical records parsed
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Code).IsEqualTo("ABCD");
        await Assert.That(records[0].Name).IsEqualTo("EFGHIJ");
        await Assert.That(records[0].Value).IsEqualTo("KLMNO");
        await Assert.That(records[1].Code).IsEqualTo("WXYZ");
        await Assert.That(records[1].Name).IsEqualTo("NAME00");
        await Assert.That(records[1].Value).IsEqualTo("VAL99");
    }

    [Test]
    [DisplayName("Fixed-width: WriteString writes all fields on single line")]
    public async Task FixedWidth_WriteString_SingleLine()
    {
        // Arrange: Create records (field widths: Code=4, Name=6, Value=5)
        var engine = new FileHelperEngine<MultiNewLineFixedRecord>();
        var records = new[]
        {
            new MultiNewLineFixedRecord { Code = "ABC", Name = "Test", Value = "100" },
        };

        // Act: Write
        var written = engine.WriteString(records);

        // Assert: All fields written on single line (FieldInNewLine is read-side only)
        // Values are padded/truncated to their fixed widths
        await Assert.That(written).Contains("ABC");
        await Assert.That(written).Contains("Test");
        await Assert.That(written).Contains("100");
        // Single line per record + newline
        await Assert.That(written.Split(new[] { "\r\n" }, StringSplitOptions.None).Length).IsEqualTo(2);
    }

    [Test]
    [DisplayName("Fixed-width: FieldInNewLine combined with FieldTrim")]
    public async Task FixedWidth_InNewLine_WithTrim()
    {
        // Arrange: Multi-line record with trim on both fields
        var engine = new FileHelperEngine<TrimNewLineRecord>();
        var input = "  ABC \r\n  Desc  ";

        // Act: Parse (Code trims both, Description trims right only)
        var records = engine.ReadString(input);

        // Assert: Trim applied after joining lines
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Code).IsEqualTo("ABC");
        // TrimMode.Right only trims trailing whitespace, not leading
        await Assert.That(records[0].Description).IsEqualTo("  Desc");
    }

    [Test]
    [DisplayName("Delimited: WriteString writes all fields on single line")]
    public async Task Delimited_WriteString_SingleLine()
    {
        // Arrange: Create delimited multi-line records
        var engine = new FileHelperEngine<MultiNewLineDelimitedRecord>();
        var records = new[]
        {
            new MultiNewLineDelimitedRecord { Id = 42, Description = "Test", Notes = "Notes here" },
        };

        // Act: Write
        var written = engine.WriteString(records);

        // Assert: All fields on single delimited line (FieldInNewLine is read-side only for fixed-width)
        await Assert.That(written).Contains("42");
        await Assert.That(written).Contains("Test");
        await Assert.That(written).Contains("Notes here");
    }

    [Test]
    [DisplayName("Fixed-width: LF-only line endings work with FieldInNewLine")]
    public async Task FixedWidth_LfOnlyLineEndings()
    {
        // Arrange: LF-only input (Unix-style)
        var engine = new FileHelperEngine<MultiNewLineFixedRecord>();
        var input = "ABCD\nEFGHIJ\nKLMNO";

        // Act: Parse
        var records = engine.ReadString(input);

        // Assert: Records parsed correctly with LF endings
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Code).IsEqualTo("ABCD");
        await Assert.That(records[0].Name).IsEqualTo("EFGHIJ");
    }

    [Test]
    [DisplayName("Fixed-width: EOF mid-record produces error with SaveAndContinue")]
    public async Task FixedWidth_EofMidRecord_ProducesError()
    {
        // Arrange: Incomplete record (missing last line)
        var engine = new FileHelperEngine<MultiNewLineFixedRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        var input = "ABCD\r\nEFGHIJ"; // Missing the Value line

        // Act: Parse
        var records = engine.ReadString(input);

        // Assert: Error captured, no records returned
        await Assert.That(records.Length).IsEqualTo(0);
        await Assert.That(engine.ErrorManager.HasErrors).IsTrue();
    }

    [Test]
    [DisplayName("Fixed-width: ReadFile with FieldInNewLine")]
    public async Task FixedWidth_ReadFile_InNewLine()
    {
        // Arrange: Temp file with multi-line records
        var engine = new FileHelperEngine<MultiNewLineFixedRecord>();
        var content = "ABCD\r\nEFGHIJ\r\nKLMNO\r\nWXYZ\r\nNAME00\r\nVAL99";
        var tempPath = Path.Combine(Path.GetTempPath(), $"finl_test_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempPath, content);

        try
        {
            // Act: Read from file
            var records = engine.ReadFile(tempPath);

            // Assert: Both records parsed
            await Assert.That(records.Length).IsEqualTo(2);
            await Assert.That(records[0].Code).IsEqualTo("ABCD");
            await Assert.That(records[1].Name).IsEqualTo("NAME00");
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
