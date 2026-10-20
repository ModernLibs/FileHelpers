// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional; groups related tests.
#pragma warning disable MA0048
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Attributes;

// ---- Test record types ----

[DelimitedRecord(",")]
internal sealed class NoTrimDelimitedRecord
{
    [FieldOrder(1)]
    [FieldTrim(TrimMode.None)]
    public string Name { get; set; } = "";

    [FieldOrder(2)]
    [FieldTrim(TrimMode.None)]
    public string Value { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TrimBothDelimitedRecord
{
    [FieldOrder(1)]
    [FieldTrim(TrimMode.Both)]
    public string Name { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TrimNotEmptyRecord
{
    [FieldOrder(1)]
    [FieldTrim(TrimMode.Both)]
    [FieldNotEmpty]
    public string Name { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TrimQuotedRecord
{
    [FieldOrder(1)]
    [FieldQuoted]
    [FieldTrim(TrimMode.Both)]
    public string Name { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class TrimNumericRecord
{
    [FieldOrder(1)]
    [FieldTrim(TrimMode.Both)]
    [FieldConverter(ConverterKind.Int32)]
    public int Value { get; set; }
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class TrimWriteFixedRecord
{
    [FieldFixedLength(10)]
    [FieldTrim(TrimMode.Both)]
    public string Name { get; set; } = "";
}

[DelimitedRecord(",")]
internal sealed class EmptyTrimRecord
{
    [FieldOrder(1)]
    [FieldTrim(TrimMode.Both)]
    public string Name { get; set; } = "";
}

/// <summary>FieldTrim interaction tests across delimited records and feature combinations.</summary>
[Category("FieldTrim")]
public sealed class FieldTrimInteractionTests
{
    /// <summary>TrimMode.None preserves whitespace on delimited records.</summary>
    [Test]
    [DisplayName("Delimited: TrimMode.None preserves leading and trailing whitespace")]
    public async Task Delimited_TrimModeNone_PreservesWhitespace()
    {
        // Arrange: Input with whitespace around values
        var engine = new FileHelperEngine<NoTrimDelimitedRecord>();
        var input = "  hello  ,  world  ";

        // Act: Parse
        var records = engine.ReadString(input);

        // Assert: Whitespace preserved
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Name).IsEqualTo("  hello  ");
        await Assert.That(records[0].Value).IsEqualTo("  world  ");
    }

    /// <summary>Delimited engine with TrimMode.Both.</summary>
    [Test]
    [DisplayName("Delimited: TrimMode.Both trims leading and trailing whitespace")]
    public async Task Delimited_TrimModeBoth_TrimsBoth()
    {
        // Arrange: Input with whitespace
        var engine = new FileHelperEngine<TrimBothDelimitedRecord>();
        var input = "  hello world  ";

        // Act: Parse
        var records = engine.ReadString(input);

        // Assert: Whitespace trimmed
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Name).IsEqualTo("hello world");
    }

    /// <summary>FieldTrim + FieldNotEmpty interaction.</summary>
    [Test]
    [DisplayName("FieldTrim + FieldNotEmpty: trimmed empty string triggers error")]
    public async Task TrimPlusNotEmpty_TrimsThenValidates()
    {
        // Arrange: Whitespace-only input that becomes empty after trim
        var engine = new FileHelperEngine<TrimNotEmptyRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        var input = "   ";

        // Act: Parse
        var records = engine.ReadString(input);

        // Assert: Error captured because trimmed value is empty
        await Assert.That(records.Length).IsEqualTo(0);
        await Assert.That(engine.ErrorManager.HasErrors).IsTrue();
    }

    /// <summary>FieldTrim + FieldQuoted interaction.</summary>
    [Test]
    [DisplayName("FieldTrim + FieldQuoted: trim applied after quote removal")]
    public async Task TrimPlusQuoted_AfterQuoteRemoval()
    {
        // Arrange: Quoted field with internal whitespace
        var engine = new FileHelperEngine<TrimQuotedRecord>();
        var input = "\"  hello  \"";

        // Act: Parse
        var records = engine.ReadString(input);

        // Assert: Quotes removed, then trim applied
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Name).IsEqualTo("hello");
    }

    /// <summary>FieldTrim on numeric field (trim before conversion).</summary>
    [Test]
    [DisplayName("FieldTrim on numeric field: trims before conversion")]
    public async Task TrimOnNumeric_TrimsBeforeConvert()
    {
        // Arrange: Numeric value with surrounding whitespace
        var engine = new FileHelperEngine<TrimNumericRecord>();
        var input = "  42  ";

        // Act: Parse
        var records = engine.ReadString(input);

        // Assert: Trimmed then parsed successfully
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Value).IsEqualTo(42);
    }

    /// <summary>WriteString with FieldTrim on fixed-length records.</summary>
    [Test]
    [DisplayName("WriteString: FieldTrim is read-side only, write preserves original value")]
    public async Task WriteString_TrimIsReadSideOnly()
    {
        // Arrange: Record with whitespace in value
        var engine = new FileHelperEngine<TrimWriteFixedRecord>();
        var records = new[] { new TrimWriteFixedRecord { Name = "  hello  " } };

        // Act: Write
        var result = engine.WriteString(records);

        // Assert: FieldTrim is read-side only; write preserves original value padded to fixed width
        await Assert.That(result).IsEqualTo("  hello   \r\n");
    }

    /// <summary>Empty input produces zero records.</summary>
    [Test]
    [DisplayName("Empty input produces zero records")]
    public async Task EmptyInput_ProducesZeroRecords()
    {
        // Arrange: Empty input
        var engine = new FileHelperEngine<EmptyTrimRecord>();
        var input = "";

        // Act: Parse
        var records = engine.ReadString(input);

        // Assert: No records parsed from empty input
        await Assert.That(records.Length).IsEqualTo(0);
    }

    /// <summary>All-whitespace input with FieldTrim.</summary>
    [Test]
    [DisplayName("All-whitespace input with FieldTrim produces trimmed empty string")]
    public async Task WhitespaceInput_WithTrim_ProducesEmpty()
    {
        // Arrange: Whitespace-only field
        var engine = new FileHelperEngine<EmptyTrimRecord>();
        var input = "     ";

        // Act: Parse
        var records = engine.ReadString(input);

        // Assert: Trimmed to empty string
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].Name).IsEqualTo("");
    }
}
