// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple fixture types live with the behavior tests that use them.
#pragma warning disable MA0048

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase20;

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class ExactLengthRecord
{
    [FieldFixedLength(3)]
    public string Code { get; set; } = "";

    [FieldFixedLength(2)]
    public string Suffix { get; set; } = "";
}

[FixedLengthRecord(FixedMode.AllowLessChars)]
internal sealed class AllowLessCharsRecord
{
    [FieldFixedLength(3)]
    public string Code { get; set; } = "";

    [FieldFixedLength(2)]
    public string Suffix { get; set; } = "";
}

[FixedLengthRecord(FixedMode.AllowMoreChars)]
internal sealed class AllowMoreCharsRecord
{
    [FieldFixedLength(3)]
    public string Code { get; set; } = "";

    [FieldFixedLength(2)]
    public string Suffix { get; set; } = "";
}

[FixedLengthRecord(FixedMode.AllowVariableLength)]
internal sealed class AllowVariableLengthRecord
{
    [FieldFixedLength(3)]
    public string Code { get; set; } = "";

    [FieldFixedLength(2)]
    public string Suffix { get; set; } = "";
}

[FixedLengthRecord]
internal sealed class OverflowDiscardRecord
{
    [FieldFixedLength(3, OverflowMode = OverflowMode.DiscardEnd)]
    public string Code { get; set; } = "";
}

[FixedLengthRecord]
internal sealed class OverflowErrorRecord
{
    [FieldFixedLength(3, OverflowMode = OverflowMode.Error)]
    public string Code { get; set; } = "";
}

[Category("Phase20")]
[Category("FixedWidth")]
public sealed class FixedWidthContractTests
{
    [Test]
    [DisplayName("FixedMode ExactLength accepts only declared total width")]
    public async Task ReadString_ExactLength_RequiresDeclaredWidth()
    {
        // Arrange: create an exact fixed-width engine with a 5-character schema.
        var engine = new FileHelperEngine<ExactLengthRecord>();

        // Act: read one correctly sized record.
        var records = engine.ReadString("ABC12");

        // Assert: exact width parses while shorter and longer records fail.
        await Assert.That(records[0].Code).IsEqualTo("ABC");
        await Assert.That(records[0].Suffix).IsEqualTo("12");
        await Assert.That(() => engine.ReadString("ABC1")).ThrowsException();
        await Assert.That(() => engine.ReadString("ABC123")).ThrowsException();
    }

    [Test]
    [DisplayName("FixedMode AllowLessChars accepts short records and rejects long records")]
    public async Task ReadString_AllowLessChars_AcceptsShortOnly()
    {
        // Arrange: create an allow-less fixed-width engine with a 5-character schema.
        var engine = new FileHelperEngine<AllowLessCharsRecord>();

        // Act: read a short record.
        var records = engine.ReadString("ABC1");

        // Assert: available content is parsed and surplus content is rejected.
        await Assert.That(records[0].Code).IsEqualTo("ABC");
        await Assert.That(records[0].Suffix).IsEqualTo("1");
        await Assert.That(() => engine.ReadString("ABC123")).ThrowsException();
    }

    [Test]
    [DisplayName("FixedMode AllowMoreChars accepts long records and rejects short records")]
    public async Task ReadString_AllowMoreChars_AcceptsLongOnly()
    {
        // Arrange: create an allow-more fixed-width engine with a 5-character schema.
        var engine = new FileHelperEngine<AllowMoreCharsRecord>();

        // Act: read a long record.
        var records = engine.ReadString("ABC123");

        // Assert: declared fields parse and short records fail.
        await Assert.That(records[0].Code).IsEqualTo("ABC");
        await Assert.That(records[0].Suffix).IsEqualTo("12");
        await Assert.That(() => engine.ReadString("ABC1")).ThrowsException();
    }

    [Test]
    [DisplayName("FixedMode AllowVariableLength accepts short and long records")]
    public async Task ReadString_AllowVariableLength_AcceptsShortAndLong()
    {
        // Arrange: create a variable-length fixed-width engine.
        var engine = new FileHelperEngine<AllowVariableLengthRecord>();

        // Act: read short and long records.
        var shortRecords = engine.ReadString("ABC1");
        var longRecords = engine.ReadString("ABC123");

        // Assert: both variants parse using declared field slices where present.
        await Assert.That(shortRecords[0].Suffix).IsEqualTo("1");
        await Assert.That(longRecords[0].Suffix).IsEqualTo("12");
    }

    [Test]
    [DisplayName("OverflowMode DiscardEnd truncates overflowing fixed-width values")]
    public async Task WriteString_OverflowDiscardEnd_TruncatesValue()
    {
        // Arrange: create a record whose value exceeds the fixed width.
        var engine = new FileHelperEngine<OverflowDiscardRecord>();
        var records = new[] { new OverflowDiscardRecord { Code = "ABCDE" } };

        // Act: write the record.
        string result = engine.WriteString(records);

        // Assert: the value is truncated to the declared width.
        await Assert.That(result).IsEqualTo("ABC\r\n");
    }

    [Test]
    [DisplayName("OverflowMode Error rejects overflowing fixed-width values")]
    public async Task WriteString_OverflowError_Throws()
    {
        // Arrange: create a record whose value exceeds the fixed width.
        var engine = new FileHelperEngine<OverflowErrorRecord>();
        var records = new[] { new OverflowErrorRecord { Code = "ABCDE" } };

        // Act & Assert: write fails instead of silently truncating.
        await Assert.That(() => engine.WriteString(records)).ThrowsException();
    }
}
