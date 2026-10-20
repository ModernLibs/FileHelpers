// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple fixture types live with the behavior tests that use them.
#pragma warning disable MA0048
// CS0649: Public fields assigned by source-generated descriptor.
#pragma warning disable CS0649

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.FixedLength;

#region Fixtures

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class FixedTrimPublicRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(10)]
    [FieldTrim(TrimMode.Both)]
    public string Name = string.Empty;
}

[FixedLengthRecord(FixedMode.AllowLessChars)]
internal sealed class FixedOptionalTailRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(2)]
    public string Head = string.Empty;

    [FieldOrder(2)]
    [FieldFixedLength(4)]
    [FieldOptional]
    public string? Tail;
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class FixedInNewLineRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(2)]
    public string Code = string.Empty;

    [FieldOrder(2)]
    [FieldFixedLength(4)]
    [FieldInNewLine]
    public string Value = string.Empty;
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class FixedNullSentinelRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(4)]
    public int Id;

    [FieldOrder(2)]
    [FieldFixedLength(4)]
    [FieldNullValue(null)]
    public string? Note;
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class FixedNotEmptyRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(6)]
    [FieldNotEmpty]
    public string Code = string.Empty;
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class FixedDiscardedEngineRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(3)]
    public int Id;

    [FieldOrder(2)]
    [FieldFixedLength(4)]
    [FieldValueDiscarded]
    public string? Skip;

    [FieldOrder(3)]
    [FieldFixedLength(5)]
    public int Value;
}

#endregion

/// <summary>
/// Fixed-width attribute matrix through <see cref="FileHelperEngine{T}"/> public API.
/// </summary>
[Category("FixedWidth")]
public sealed class FixedWidthAttributePublicApiTests
{
    [Test]
    [DisplayName("ReadString fixed-width FieldTrim trims spaces inside declared width")]
    public async Task ReadString_FixedWidth_FieldTrim_TrimsName()
    {
        // Arrange: 10-char field with internal name padded by spaces (trim both).
        var engine = new FileHelperEngine<FixedTrimPublicRecord>();
        var line = "  Alice   "; // 10 chars

        // Act
        var rows = engine.ReadString(line);

        // Assert
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Name).IsEqualTo("Alice");
    }

    [Test]
    [DisplayName("WriteString then ReadString round-trips fixed-width FieldTrim record")]
    public async Task WriteString_ReadString_FixedWidth_FieldTrim_RoundTrip()
    {
        // Arrange
        var engine = new FileHelperEngine<FixedTrimPublicRecord>();
        var original = new FixedTrimPublicRecord { Name = "Bob" };

        // Act
        var text = engine.WriteString([original]);
        var round = engine.ReadString(text);

        // Assert
        await Assert.That(round.Length).IsEqualTo(1);
        await Assert.That(round[0].Name.Trim()).IsEqualTo("Bob");
    }

    [Test]
    [DisplayName("ReadString fixed-width FieldOptional treats whitespace-only tail as absent value")]
    public async Task ReadString_FixedWidth_FieldOptional_TailBlankWhenWhitespace()
    {
        // Arrange: 2 + 4 = 6 chars; optional tail all spaces — may surface as null or whitespace-only span.
        var engine = new FileHelperEngine<FixedOptionalTailRecord>();
        var line = "AB    ";

        // Act
        var rows = engine.ReadString(line);

        // Assert
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Head).IsEqualTo("AB");
        await Assert.That(rows[0].Tail is null || string.IsNullOrWhiteSpace(rows[0].Tail)).IsTrue();
    }

    [Test]
    [DisplayName("ReadString fixed-width FieldOptional allows omitted trailing field width")]
    public async Task ReadString_FixedWidth_FieldOptional_TailOmitted()
    {
        // Arrange: only the required head field is present; AllowLessChars permits the optional tail to be omitted.
        var engine = new FileHelperEngine<FixedOptionalTailRecord>();
        var line = "CD";

        // Act
        var rows = engine.ReadString(line);

        // Assert
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Head).IsEqualTo("CD");
        await Assert.That(string.IsNullOrEmpty(rows[0].Tail)).IsTrue();
    }

    [Test]
    [DisplayName("ReadString fixed-width FieldInNewLine joins two physical lines via engine")]
    public async Task ReadString_FixedWidth_FieldInNewLine_TwoLines()
    {
        // Arrange: first line 2 chars, second line 4 chars (see FixedInNewLineRecord).
        var engine = new FileHelperEngine<FixedInNewLineRecord>();
        var input = "aa\r\nbbbb";

        // Act
        var rows = engine.ReadString(input);

        // Assert
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Code).IsEqualTo("aa");
        await Assert.That(rows[0].Value).IsEqualTo("bbbb");
    }

    [Test]
    [DisplayName("ReadString fixed-width FieldNullValue yields absent/empty Note for whitespace span")]
    public async Task ReadString_FixedWidth_FieldNullValue_NoteEmptyWhenWhitespace()
    {
        // Arrange: Id=7 (4 chars zero-padded), Note field 4 spaces — null-value substitution may be null or empty string.
        var engine = new FileHelperEngine<FixedNullSentinelRecord>();
        var line = "0007    ";

        // Act
        var rows = engine.ReadString(line);

        // Assert
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Id).IsEqualTo(7);
        await Assert.That(rows[0].Note is null || string.IsNullOrWhiteSpace(rows[0].Note)).IsTrue();
    }

    [Test]
    [DisplayName("ReadString fixed-width FieldNotEmpty throws when field is whitespace only")]
    public async Task ReadString_FixedWidth_FieldNotEmpty_Whitespace_Throws()
    {
        // Arrange
        var engine = new FileHelperEngine<FixedNotEmptyRecord>();

        // Act & Assert
        await Assert.That(() => engine.ReadString("      ")).ThrowsException();
    }

    [Test]
    [DisplayName("ReadString fixed-width FieldValueDiscarded skips assignment but advances layout")]
    public async Task ReadString_FixedWidth_FieldValueDiscarded_MiddleField()
    {
        // Arrange: 3 + 4 + 5 = 12 chars
        var engine = new FileHelperEngine<FixedDiscardedEngineRecord>();
        var line = "001SKIP00200";

        // Act
        var rows = engine.ReadString(line);

        // Assert
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Id).IsEqualTo(1);
        await Assert.That(rows[0].Skip).IsNull();
        await Assert.That(rows[0].Value).IsEqualTo(200);
    }
}
