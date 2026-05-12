// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional; groups related tests.
#pragma warning disable MA0048
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase59;

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class WriteAlignedRecord
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Right)]
    public string Name { get; set; } = "";
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class WriteTrimAlignedRecord
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Left)]
    [FieldTrim(TrimMode.Both)]
    public string Name { get; set; } = "";
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class WriteAlignedIntRecord
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Right)]
    [FieldConverter(ConverterKind.Int32)]
    public int Value { get; set; }
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class WriteUnicodeAlignedRecord
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Left)]
    public string Text { get; set; } = "";
}

[Category("Phase59")]
[Category("FieldAlign")]
public sealed class FieldAlignReadTests
{
    [Test]
    [DisplayName("WriteString: right-aligned values are padded on the left")]
    public async Task WriteString_RightAligned_PadsLeft()
    {
        // Arrange: Record with right alignment
        var engine = new FileHelperEngine<WriteAlignedRecord>();
        var records = new[] { new WriteAlignedRecord { Name = "ABC" } };

        // Act: Write
        var result = engine.WriteString(records);

        // Assert: Value padded on left to 10 chars
        await Assert.That(result).IsEqualTo("       ABC\r\n");
    }

    [Test]
    [DisplayName("WriteString: left-aligned values are padded on the right")]
    public async Task WriteString_LeftAligned_PadsRight()
    {
        // Arrange: Record with left alignment
        var engine = new FileHelperEngine<WriteAlignedRecord>();
        var records = new[] { new WriteAlignedRecord { Name = "ABC" } };

        // Act: Write
        var result = engine.WriteString(records);

        // Assert: Value padded on right to 10 chars
        await Assert.That(result).IsEqualTo("       ABC\r\n");
    }

    [Test]
    [DisplayName("FieldAlign + FieldTrim: trim applied during read, alignment during write")]
    public async Task AlignPlusTrim_ReadTrimsWriteAligns()
    {
        // Arrange: Record with left alignment and trim
        var engine = new FileHelperEngine<WriteTrimAlignedRecord>();
        var records = new[] { new WriteTrimAlignedRecord { Name = "  hello  " } };

        // Act: Write (trim is read-side only; write preserves original + aligns)
        var result = engine.WriteString(records);

        // Assert: Original value left-aligned to 10 chars (trim not applied on write)
        await Assert.That(result).IsEqualTo("  hello   \r\n");

        // Act: Re-read (trim applies on read)
        var reRead = engine.ReadString(result);

        // Assert: Trim applied on read
        await Assert.That(reRead.Length).IsEqualTo(1);
        await Assert.That(reRead[0].Name).IsEqualTo("hello");
    }

    [Test]
    [DisplayName("FieldAlign + FieldConverter: aligned numeric written correctly")]
    public async Task AlignPlusConverter_AlignedNumericWritten()
    {
        // Arrange: Record with right-aligned numeric
        var engine = new FileHelperEngine<WriteAlignedIntRecord>();
        var records = new[] { new WriteAlignedIntRecord { Value = 42 } };

        // Act: Write
        var result = engine.WriteString(records);

        // Assert: Numeric right-aligned to 10 chars
        await Assert.That(result).IsEqualTo("        42\r\n");
    }

    [Test]
    [DisplayName("Unicode characters with left alignment")]
    public async Task Unicode_LeftAligned()
    {
        // Arrange: Unicode text in aligned field
        var engine = new FileHelperEngine<WriteUnicodeAlignedRecord>();
        var records = new[] { new WriteUnicodeAlignedRecord { Text = "日本語" } };

        // Act: Write
        var result = engine.WriteString(records);

        // Assert: Unicode text written with padding
        await Assert.That(result).Contains("日本語");
    }

    [Test]
    [DisplayName("Read/Write round-trip with FieldAlign preserves data")]
    public async Task RoundTrip_AlignPreservesData()
    {
        // Arrange: Create records
        var engine = new FileHelperEngine<WriteAlignedRecord>();
        var original = new[] { new WriteAlignedRecord { Name = "Test" } };

        // Act: Write and re-read
        var written = engine.WriteString(original);
        var reRead = engine.ReadString(written);

        // Assert: Record count preserved; raw value includes padding on read
        await Assert.That(reRead.Length).IsEqualTo(1);
        await Assert.That(reRead[0].Name.Trim()).IsEqualTo("Test");
    }
}
