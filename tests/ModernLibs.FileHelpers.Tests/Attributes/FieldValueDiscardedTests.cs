// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0025: Fixture bodies intentionally empty.
#pragma warning disable MA0025
// MA0048: Multiple fixture types per file — intentional.
#pragma warning disable MA0048
// CS0649: Public fields assigned by source-generated descriptor.
#pragma warning disable CS0649

using System.Buffers;
using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Attributes;

/// <summary>
/// TDD tests for [FieldValueDiscarded] attribute (FAVL-07, FAVL-10).
/// These tests define the expected behavior before implementation (RED phase).
/// </summary>
[Category("Phase13")]
[Category("Validation")]
public sealed class FieldValueDiscardedTests
{
    #region Delimited Record Tests

    /// <summary>FAVL-07: Delimited record with discarded middle field - field advances, value not assigned.</summary>
    [Test]
    [DisplayName("Parse_FieldValueDiscarded_MiddleField_AdvancesButNotAssigned")]
    public async Task Parse_FieldValueDiscarded_MiddleField_AdvancesButNotAssigned()
    {
        // Arrange: record with discarded middle field
        var desc = FileHelperRegistry<MixedDiscardedRecord>.Descriptor;

        // Act: "1,middle,100" - Id=1, SkipMe should be default (not "middle"), Value=100
        var record = desc.Parse("1,middle,100".AsSpan());

        // Assert
        await Assert.That(record.Id).IsEqualTo(1);
        await Assert.That(record.SkipMe).IsNull();  // Not assigned, remains null
        await Assert.That(record.Value).IsEqualTo(100);
    }

    /// <summary>FAVL-07: Discarded field at end of record - parser handles correctly.</summary>
    [Test]
    [DisplayName("Parse_FieldValueDiscarded_EndField_ParsesCorrectly")]
    public async Task Parse_FieldValueDiscarded_EndField_ParsesCorrectly()
    {
        // Arrange: record with discarded field at end
        var desc = FileHelperRegistry<EndDiscardedRecord>.Descriptor;

        // Act: "Alice,ignored"
        var record = desc.Parse("Alice,ignored".AsSpan());

        // Assert
        await Assert.That(record.Name).IsEqualTo("Alice");
        await Assert.That(record.Discarded).IsNull();  // Not assigned
    }

    /// <summary>FAVL-07: Multiple discarded fields interspersed with active fields.</summary>
    [Test]
    [DisplayName("Parse_FieldValueDiscarded_MultipleDiscarded_ParsesCorrectly")]
    public async Task Parse_FieldValueDiscarded_MultipleDiscarded_ParsesCorrectly()
    {
        // Arrange: record with multiple discarded fields
        var desc = FileHelperRegistry<MultipleDiscardedRecord>.Descriptor;

        // Act: "1,skipA,2,skipB,3" - only Id1, Id2, Id3 should be assigned
        var record = desc.Parse("1,skipA,2,skipB,3".AsSpan());

        // Assert
        await Assert.That(record.Id1).IsEqualTo(1);
        await Assert.That(record.SkipA).IsNull();
        await Assert.That(record.Id2).IsEqualTo(2);
        await Assert.That(record.SkipB).IsNull();
        await Assert.That(record.Id3).IsEqualTo(3);
    }

    /// <summary>FAVL-07: Record member retains default value when field is discarded.</summary>
    [Test]
    [DisplayName("Parse_FieldValueDiscarded_RetainsDefaultValue")]
    public async Task Parse_FieldValueDiscarded_RetainsDefaultValue()
    {
        // Arrange: record with non-null default value for discarded field
        var desc = FileHelperRegistry<DiscardedWithDefaultRecord>.Descriptor;

        // Act
        var record = desc.Parse("1,replaced,100".AsSpan());

        // Assert: SkipMe retains its default value, not "replaced"
        await Assert.That(record.Id).IsEqualTo(1);
        await Assert.That(record.SkipMe).IsEqualTo("DEFAULT");  // Should retain default
        await Assert.That(record.Value).IsEqualTo(100);
    }

    #endregion

    #region Fixed-Length Record Tests

    /// <summary>FAVL-10: Fixed-length record with discarded field - offset advances, value not assigned.</summary>
    [Test]
    [DisplayName("Parse_FixedLength_FieldValueDiscarded_AdvancesOffsetButNotAssigned")]
    public async Task Parse_FixedLength_FieldValueDiscarded_AdvancesOffsetButNotAssigned()
    {
        // Arrange: fixed-length record with discarded field
        var desc = FileHelperRegistry<FixedDiscardedRecord>.Descriptor;

        // Act: "00001SKIP00200" - Id=1, Skip stays default, Value=200
        var record = desc.Parse("00001SKIP00200".AsSpan());

        // Assert
        await Assert.That(record.Id).IsEqualTo(1);
        await Assert.That(record.Skip).IsNull();  // Not assigned
        await Assert.That(record.Value).IsEqualTo(200);
    }

    /// <summary>FAVL-10: Fixed-length with discarded at beginning - subsequent fields at correct offsets.</summary>
    [Test]
    [DisplayName("Parse_FixedLength_FieldValueDiscarded_Beginning_CorrectOffsets")]
    public async Task Parse_FixedLength_FieldValueDiscarded_Beginning_CorrectOffsets()
    {
        // Arrange: record with discarded field at beginning
        var desc = FileHelperRegistry<FixedDiscardedBeginningRecord>.Descriptor;

        // Act: "SKIP00001" - Skip is 4 chars, Id should read from position 4
        var record = desc.Parse("SKIP00001".AsSpan());

        // Assert
        await Assert.That(record.Skip).IsNull();
        await Assert.That(record.Id).IsEqualTo(1);  // Should correctly read "00001" after "SKIP"
    }

    /// <summary>FAVL-10: Fixed-length with multiple discarded fields - all offsets correct.</summary>
    [Test]
    [DisplayName("Parse_FixedLength_MultipleDiscarded_AllOffsetsCorrect")]
    public async Task Parse_FixedLength_MultipleDiscarded_AllOffsetsCorrect()
    {
        // Arrange
        var desc = FileHelperRegistry<FixedMultipleDiscardedRecord>.Descriptor;

        // Act: "AA111BB222CC333" - AA and BB discarded, Id1=111, Id2=222, Id3=333
        var record = desc.Parse("AA111BB222CC333".AsSpan());

        // Assert
        await Assert.That(record.Skip1).IsNull();
        await Assert.That(record.Id1).IsEqualTo(111);
        await Assert.That(record.Skip2).IsNull();
        await Assert.That(record.Id2).IsEqualTo(222);
        await Assert.That(record.Skip3).IsNull();
        await Assert.That(record.Id3).IsEqualTo(333);
    }

    #endregion

    #region Write/Format Tests

    /// <summary>FAVL-07: Write operation skips discarded fields.</summary>
    [Test]
    [DisplayName("Write_FieldValueDiscarded_SkipsDiscardedFields")]
    public async Task Write_FieldValueDiscarded_SkipsDiscardedFields()
    {
        // Arrange: record with discarded middle field
        var desc = FileHelperRegistry<MixedDiscardedRecord>.Descriptor;
        var record = new MixedDiscardedRecord { Id = 1, Value = 100 };

        // Act: format the record
        var writer = new ArrayBufferWriter<char>(256);
        desc.Format(record, writer);
        var output = new string(writer.WrittenSpan);

        // Assert: output should be "1,100" not "1,,100" (discarded field not written)
        await Assert.That(output).IsEqualTo("1,100");
    }

    /// <summary>FAVL-07: Write with multiple discarded fields - all skipped.</summary>
    [Test]
    [DisplayName("Write_FieldValueDiscarded_Multiple_AllSkipped")]
    public async Task Write_FieldValueDiscarded_Multiple_AllSkipped()
    {
        // Arrange
        var desc = FileHelperRegistry<MultipleDiscardedRecord>.Descriptor;
        var record = new MultipleDiscardedRecord { Id1 = 1, Id2 = 2, Id3 = 3 };

        // Act
        var writer = new ArrayBufferWriter<char>(256);
        desc.Format(record, writer);
        var output = new string(writer.WrittenSpan);

        // Assert: output should be "1,2,3"
        await Assert.That(output).IsEqualTo("1,2,3");
    }

    /// <summary>FAVL-10: Write fixed-length record - discarded fields produce no output.</summary>
    [Test]
    [DisplayName("Write_FixedLength_FieldValueDiscarded_NoOutputForDiscarded")]
    public async Task Write_FixedLength_FieldValueDiscarded_NoOutputForDiscarded()
    {
        // Arrange
        var desc = FileHelperRegistry<FixedDiscardedRecord>.Descriptor;
        var record = new FixedDiscardedRecord { Id = 1, Value = 200 };

        // Act
        var writer = new ArrayBufferWriter<char>(256);
        desc.Format(record, writer);
        var output = new string(writer.WrittenSpan);

        // Assert: output should be "0000100200" (5 chars for Id + 0 for Skip + 5 chars for Value = 10)
        // For fixed-length discarded fields, they should still occupy space (maintaining alignment)
        // So output should be "00001     00200" (5 + 3 + 5 = 13)
        await Assert.That(output.Length).IsEqualTo(13);
    }

    #endregion
}

#region Test Fixtures

/// <summary>Delimited record with discarded middle field.</summary>
[DelimitedRecord(",")]
internal sealed class MixedDiscardedRecord
{
    [FieldOrder(1)]
    public int Id;

    [FieldOrder(2)]
    [FieldValueDiscarded]
    public string? SkipMe;

    [FieldOrder(3)]
    public int Value;
}

/// <summary>Delimited record with discarded field at end.</summary>
[DelimitedRecord(",")]
internal sealed class EndDiscardedRecord
{
    [FieldOrder(1)]
    public string Name = string.Empty;

    [FieldOrder(2)]
    [FieldValueDiscarded]
    public string? Discarded;
}

/// <summary>Delimited record with multiple discarded fields.</summary>
[DelimitedRecord(",")]
internal sealed class MultipleDiscardedRecord
{
    [FieldOrder(1)]
    public int Id1;

    [FieldOrder(2)]
    [FieldValueDiscarded]
    public string? SkipA;

    [FieldOrder(3)]
    public int Id2;

    [FieldOrder(4)]
    [FieldValueDiscarded]
    public string? SkipB;

    [FieldOrder(5)]
    public int Id3;
}

/// <summary>Delimited record with discarded field that has non-null default.</summary>
[DelimitedRecord(",")]
internal sealed class DiscardedWithDefaultRecord
{
    [FieldOrder(1)]
    public int Id;

    [FieldOrder(2)]
    [FieldValueDiscarded]
    public string SkipMe = "DEFAULT";

    [FieldOrder(3)]
    public int Value;
}

/// <summary>Fixed-length record with discarded field.</summary>
[FixedLengthRecord]
internal sealed class FixedDiscardedRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(5)]
    public int Id;

    [FieldOrder(2)]
    [FieldFixedLength(3)]
    [FieldValueDiscarded]
    public string? Skip;

    [FieldOrder(3)]
    [FieldFixedLength(5)]
    public int Value;
}

/// <summary>Fixed-length record with discarded field at beginning.</summary>
[FixedLengthRecord]
internal sealed class FixedDiscardedBeginningRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(4)]
    [FieldValueDiscarded]
    public string? Skip;

    [FieldOrder(2)]
    [FieldFixedLength(5)]
    public int Id;
}

/// <summary>Fixed-length record with multiple discarded fields.</summary>
[FixedLengthRecord]
internal sealed class FixedMultipleDiscardedRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(2)]
    [FieldValueDiscarded]
    public string? Skip1;

    [FieldOrder(2)]
    [FieldFixedLength(3)]
    public int Id1;

    [FieldOrder(3)]
    [FieldFixedLength(2)]
    [FieldValueDiscarded]
    public string? Skip2;

    [FieldOrder(4)]
    [FieldFixedLength(3)]
    public int Id2;

    [FieldOrder(5)]
    [FieldFixedLength(2)]
    [FieldValueDiscarded]
    public string? Skip3;

    [FieldOrder(6)]
    [FieldFixedLength(3)]
    public int Id3;
}

#endregion
