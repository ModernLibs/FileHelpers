// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional; groups related alignment tests.
#pragma warning disable MA0048
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Attributes;

// ---- Test record types for fixed-length alignment tests ----

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class LeftAlignedRecord
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Left)]
    public string Name { get; set; } = "";
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class RightAlignedRecord
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Right)]
    public string Name { get; set; } = "";
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class CenterAlignedRecord
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Center)]
    public string Name { get; set; } = "";
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class CustomPadLeftRecord
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Left, '*')]
    public string Name { get; set; } = "";
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class CustomPadRightRecord
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Right, '*')]
    public string Name { get; set; } = "";
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class CustomPadCenterRecord
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Center, '*')]
    public string Name { get; set; } = "";
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class MixedTypesAlignedRecord
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Right)]
    public int Number { get; set; }

    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Left)]
    public string Text { get; set; } = "";

    [FieldFixedLength(12)]
    [FieldAlign(AlignMode.Right)]
    public decimal Amount { get; set; }
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class NullValueAlignedRecord
{
    [FieldFixedLength(8)]
    [FieldAlign(AlignMode.Left)]
    public string? Name { get; set; }
}

/// <summary>
/// Acceptance tests for [FieldAlign] on fixed-length records.
/// These tests expect alignment to be applied during Format operations.
/// </summary>
[Category("FieldAlign")]
public sealed class FieldAlignTests
{
    /// <summary>Left alignment pads after value with spaces.</summary>
    [Test]
    [DisplayName("WriteString with Left alignment pads value on the right with spaces")]
    public async Task WriteString_LeftAlignment_PadsOnRight()
    {
        // Arrange: Create engine and record with 3-char value in 10-char field
        var engine = new FileHelperEngine<LeftAlignedRecord>();
        var records = new[] { new LeftAlignedRecord { Name = "ABC" } };

        // Act: Format record to fixed-length string
        var result = engine.WriteString(records);

        // Assert: Value at start, 7 space padding after (ABC + 7 spaces = 10 chars)
        await Assert.That(result).IsEqualTo("ABC       \r\n");
    }

    /// <summary>Right alignment pads before value with spaces.</summary>
    [Test]
    [DisplayName("WriteString with Right alignment pads value on the left with spaces")]
    public async Task WriteString_RightAlignment_PadsOnLeft()
    {
        // Arrange: Create engine and record with 3-char value in 10-char field
        var engine = new FileHelperEngine<RightAlignedRecord>();
        var records = new[] { new RightAlignedRecord { Name = "ABC" } };

        // Act: Format record to fixed-length string
        var result = engine.WriteString(records);

        // Assert: 7 space padding before, value at end (7 spaces + ABC = 10 chars)
        await Assert.That(result).IsEqualTo("       ABC\r\n");
    }

    /// <summary>Center alignment distributes padding on both sides.</summary>
    [Test]
    [DisplayName("WriteString with Center alignment distributes padding on both sides")]
    public async Task WriteString_CenterAlignment_PadsBothSides()
    {
        // Arrange: Create engine and record with 3-char value in 10-char field
        var engine = new FileHelperEngine<CenterAlignedRecord>();
        var records = new[] { new CenterAlignedRecord { Name = "ABC" } };

        // Act: Format record to fixed-length string
        var result = engine.WriteString(records);

        // Assert: 4 left padding, value, 3 right padding (4 + 3 + 3 = 10 chars)
        // Extra padding goes to left side when odd
        await Assert.That(result).IsEqualTo("   ABC    \r\n");
    }

    /// <summary>Left alignment with custom pad character.</summary>
    [Test]
    [DisplayName("WriteString with Left alignment uses custom pad character")]
    public async Task WriteString_LeftAlignment_CustomPadChar()
    {
        // Arrange: Create engine and record with custom '*' pad char
        var engine = new FileHelperEngine<CustomPadLeftRecord>();
        var records = new[] { new CustomPadLeftRecord { Name = "ABC" } };

        // Act: Format record to fixed-length string
        var result = engine.WriteString(records);

        // Assert: Value at start, 7 '*' padding after
        await Assert.That(result).IsEqualTo("ABC*******\r\n");
    }

    /// <summary>Right alignment with custom pad character.</summary>
    [Test]
    [DisplayName("WriteString with Right alignment uses custom pad character")]
    public async Task WriteString_RightAlignment_CustomPadChar()
    {
        // Arrange: Create engine and record with custom '*' pad char
        var engine = new FileHelperEngine<CustomPadRightRecord>();
        var records = new[] { new CustomPadRightRecord { Name = "ABC" } };

        // Act: Format record to fixed-length string
        var result = engine.WriteString(records);

        // Assert: 7 '*' padding before, value at end
        await Assert.That(result).IsEqualTo("*******ABC\r\n");
    }

    /// <summary>Center alignment with custom pad character.</summary>
    [Test]
    [DisplayName("WriteString with Center alignment uses custom pad character")]
    public async Task WriteString_CenterAlignment_CustomPadChar()
    {
        // Arrange: Create engine and record with custom '*' pad char
        var engine = new FileHelperEngine<CustomPadCenterRecord>();
        var records = new[] { new CustomPadCenterRecord { Name = "ABC" } };

        // Act: Format record to fixed-length string
        var result = engine.WriteString(records);

        // Assert: 4 left '*' padding, value, 3 right '*' padding
        await Assert.That(result).IsEqualTo("***ABC****\r\n");
    }

    /// <summary>Value longer than width truncates (existing behavior).</summary>
    [Test]
    [DisplayName("WriteString truncates value longer than field width")]
    public async Task WriteString_LongValue_Truncates()
    {
        // Arrange: Create engine and record with 15-char value in 10-char field
        var engine = new FileHelperEngine<LeftAlignedRecord>();
        var records = new[] { new LeftAlignedRecord { Name = "ABCDEFGHIJ12345" } };

        // Act: Format record to fixed-length string
        var result = engine.WriteString(records);

        // Assert: Only first 10 chars written, no padding needed
        await Assert.That(result).IsEqualTo("ABCDEFGHIJ\r\n");
    }

    /// <summary>Empty string results in all padding.</summary>
    [Test]
    [DisplayName("WriteString with empty value produces all padding")]
    public async Task WriteString_EmptyValue_AllPadding()
    {
        // Arrange: Create engine and record with empty value
        var engine = new FileHelperEngine<LeftAlignedRecord>();
        var records = new[] { new LeftAlignedRecord { Name = "" } };

        // Act: Format record to fixed-length string
        var result = engine.WriteString(records);

        // Assert: All 10 chars are spaces (or pad char)
        await Assert.That(result).IsEqualTo("          \r\n");
    }

    /// <summary>Null string results in all padding.</summary>
    [Test]
    [DisplayName("WriteString with null value produces all padding")]
    public async Task WriteString_NullValue_AllPadding()
    {
        // Arrange: Create engine and record with null value
        var engine = new FileHelperEngine<NullValueAlignedRecord>();
        var records = new[] { new NullValueAlignedRecord { Name = null } };

        // Act: Format record to fixed-length string
        var result = engine.WriteString(records);

        // Assert: All 8 chars are spaces
        await Assert.That(result).IsEqualTo("        \r\n");
    }

    /// <summary>Numeric types with alignment.</summary>
    [Test]
    [DisplayName("WriteString applies alignment to int, string, and decimal fields")]
    public async Task WriteString_MixedTypes_AlignmentApplied()
    {
        // Arrange: Create engine with mixed-type aligned record
        var engine = new FileHelperEngine<MixedTypesAlignedRecord>();
        var records = new[]
        {
            new MixedTypesAlignedRecord { Number = 42, Text = "Hello", Amount = 123.45m }
        };

        // Act: Format record to fixed-length string
        var result = engine.WriteString(records);

        // Assert: Right-aligned int ("        42"), Left-aligned string ("Hello     "),
        // Right-aligned decimal ("      123.45")
        await Assert.That(result).IsEqualTo("        42Hello           123.45\r\n");
    }

    /// <summary>Exact-length mode verifies total record length.</summary>
    [Test]
    [DisplayName("WriteString produces exact field width for each aligned field")]
    public async Task WriteString_ExactLength_VerifiesWidth()
    {
        // Arrange: Create engine and multiple records
        var engine = new FileHelperEngine<MixedTypesAlignedRecord>();
        var records = new[]
        {
            new MixedTypesAlignedRecord { Number = 1, Text = "A", Amount = 1.0m }
        };

        // Act: Format record
        var result = engine.WriteString(records);

        // Remove CRLF to check just the data
        var dataLine = result.Replace("\r\n", "");

        // Assert: Total width = 10 + 10 + 12 = 32 chars
        await Assert.That(dataLine.Length).IsEqualTo(32);
    }
}
