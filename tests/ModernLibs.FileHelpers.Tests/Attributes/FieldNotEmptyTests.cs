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
/// TDD tests for [FieldNotEmpty] validation attribute (FAVL-06, FAVL-09).
/// These tests define the expected behavior before implementation (RED phase).
/// </summary>
[Category("Phase13")]
[Category("Validation")]
public sealed class FieldNotEmptyTests
{
    #region Delimited Record Tests

    /// <summary>FAVL-06: Empty string field throws validation exception with field name.</summary>
    [Test]
    [DisplayName("Parse_FieldNotEmpty_EmptyString_ThrowsValidationException")]
    public async Task Parse_FieldNotEmpty_EmptyString_ThrowsValidationException()
    {
        // Arrange: record with [FieldNotEmpty] on string field
        var desc = FileHelperRegistry<NotEmptyDelimitedRecord>.Descriptor;

        // Act & Assert: empty string should throw exception
        await Assert.That(() => desc.Parse(",123".AsSpan())).ThrowsException();
    }

    /// <summary>FAVL-06: Whitespace-only field throws validation exception.</summary>
    [Test]
    [DisplayName("Parse_FieldNotEmpty_WhitespaceOnly_ThrowsValidationException")]
    public async Task Parse_FieldNotEmpty_WhitespaceOnly_ThrowsValidationException()
    {
        // Arrange
        var desc = FileHelperRegistry<NotEmptyDelimitedRecord>.Descriptor;

        // Act & Assert
        await Assert.That(() => desc.Parse("   ,123".AsSpan())).ThrowsException();
    }

    /// <summary>FAVL-06: Valid non-whitespace content parses successfully.</summary>
    [Test]
    [DisplayName("Parse_FieldNotEmpty_ValidContent_ParsesSuccessfully")]
    public async Task Parse_FieldNotEmpty_ValidContent_ParsesSuccessfully()
    {
        // Arrange
        var desc = FileHelperRegistry<NotEmptyDelimitedRecord>.Descriptor;

        // Act
        var record = desc.Parse("Alice,123".AsSpan());

        // Assert
        await Assert.That(record.Name).IsEqualTo("Alice");
        await Assert.That(record.Id).IsEqualTo(123);
    }

    /// <summary>FAVL-06: Content with internal spaces is valid (spaces are not trimmed by default).</summary>
    [Test]
    [DisplayName("Parse_FieldNotEmpty_InternalSpaces_ParsesSuccessfully")]
    public async Task Parse_FieldNotEmpty_InternalSpaces_ParsesSuccessfully()
    {
        // Arrange
        var desc = FileHelperRegistry<NotEmptyDelimitedRecord>.Descriptor;

        // Act
        var record = desc.Parse("Alice Smith,123".AsSpan());

        // Assert
        await Assert.That(record.Name).IsEqualTo("Alice Smith");
    }

    /// <summary>FAVL-09: ErrorMode.SaveAndContinue captures FieldNotEmpty errors in ErrorManager.</summary>
    [Test]
    [DisplayName("ReadFile_FieldNotEmpty_SaveAndContinue_CapturesErrors")]
    public async Task ReadFile_FieldNotEmpty_SaveAndContinue_CapturesErrors()
    {
        // Arrange: use engine with SaveAndContinue error mode
        var engine = new FileHelperEngine<NotEmptyDelimitedRecord>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        var input = "Alice,1\n,2\nBob,3\n   ,4\n";  // Lines 2 and 4 have empty/whitespace names

        // Act
        var results = engine.ReadString(input);

        // Assert
        await Assert.That(results.Count).IsEqualTo(2);  // Only Alice and Bob are valid
        await Assert.That(results[0].Name).IsEqualTo("Alice");
        await Assert.That(results[1].Name).IsEqualTo("Bob");
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsNotNull();
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.Errors[1].LineNumber).IsEqualTo(4);
    }

    /// <summary>FAVL-06: Trim happens BEFORE not-empty check - trimmed content can be valid.</summary>
    [Test]
    [DisplayName("Parse_FieldNotEmpty_WithTrim_TrimmedContent_ValidatesAfterTrim")]
    public async Task Parse_FieldNotEmpty_WithTrim_TrimmedContent_ValidatesAfterTrim()
    {
        // Arrange: field with both [FieldTrim(Both)] and [FieldNotEmpty]
        var desc = FileHelperRegistry<NotEmptyTrimmedRecord>.Descriptor;

        // Act: "  Alice  " trimmed to "Alice" should pass validation
        var record = desc.Parse("  Alice  ,123".AsSpan());

        // Assert
        await Assert.That(record.Name).IsEqualTo("Alice");
    }

    /// <summary>FAVL-06: Trim happens BEFORE not-empty check - trimmed to empty fails.</summary>
    [Test]
    [DisplayName("Parse_FieldNotEmpty_WithTrim_OnlyWhitespace_ThrowsAfterTrim")]
    public async Task Parse_FieldNotEmpty_WithTrim_OnlyWhitespace_ThrowsAfterTrim()
    {
        // Arrange: field with both [FieldTrim(Both)] and [FieldNotEmpty]
        var desc = FileHelperRegistry<NotEmptyTrimmedRecord>.Descriptor;

        // Act & Assert: "   " trimmed to "" should fail validation
        await Assert.That(() => desc.Parse("   ,123".AsSpan())).ThrowsException();
    }

    #endregion

    #region Fixed-Length Record Tests

    /// <summary>FAVL-06: Fixed-length record with empty field throws validation exception.</summary>
    [Test]
    [DisplayName("Parse_FixedLength_FieldNotEmpty_Empty_ThrowsValidationException")]
    public async Task Parse_FixedLength_FieldNotEmpty_Empty_ThrowsValidationException()
    {
        // Arrange: fixed-length record with [FieldNotEmpty]
        var desc = FileHelperRegistry<NotEmptyFixedRecord>.Descriptor;

        // Act & Assert: "      " (6 spaces) for empty name field
        await Assert.That(() => desc.Parse("      00100".AsSpan())).ThrowsException();
    }

    /// <summary>FAVL-06: Fixed-length record with valid content parses successfully.</summary>
    [Test]
    [DisplayName("Parse_FixedLength_FieldNotEmpty_Valid_ParsesSuccessfully")]
    public async Task Parse_FixedLength_FieldNotEmpty_Valid_ParsesSuccessfully()
    {
        // Arrange
        var desc = FileHelperRegistry<NotEmptyFixedRecord>.Descriptor;

        // Act: Name is 6 chars (FieldFixedLength(6)), Value is 5 chars (FieldFixedLength(5)) = 11 total
        var record = desc.Parse("Alice 00100".AsSpan());  // "Alice" + 1 space + "00100" = 11 chars

        // Assert
        await Assert.That(record.Name).IsEqualTo("Alice ");  // 5 chars + 1 space = 6
        await Assert.That(record.Value).IsEqualTo(100);
    }

    #endregion

    #region Multiple NotEmpty Fields Tests

    /// <summary>FAVL-06: Multiple fields with [FieldNotEmpty] - first empty throws.</summary>
    [Test]
    [DisplayName("Parse_MultipleNotEmpty_FirstEmpty_ThrowsWithFirstFieldName")]
    public async Task Parse_MultipleNotEmpty_FirstEmpty_ThrowsWithFirstFieldName()
    {
        // Arrange: record with multiple [FieldNotEmpty] fields
        var desc = FileHelperRegistry<MultipleNotEmptyRecord>.Descriptor;

        // Act & Assert: FirstName empty, LastName has value
        await Assert.That(() => desc.Parse(",Smith,30".AsSpan())).ThrowsException();
    }

    /// <summary>FAVL-06: Multiple fields with [FieldNotEmpty] - second empty throws.</summary>
    [Test]
    [DisplayName("Parse_MultipleNotEmpty_SecondEmpty_ThrowsWithSecondFieldName")]
    public async Task Parse_MultipleNotEmpty_SecondEmpty_ThrowsWithSecondFieldName()
    {
        // Arrange
        var desc = FileHelperRegistry<MultipleNotEmptyRecord>.Descriptor;

        // Act & Assert: FirstName has value, LastName empty
        await Assert.That(() => desc.Parse("John,,30".AsSpan())).ThrowsException();
    }

    /// <summary>FAVL-06: All NotEmpty fields valid - parses successfully.</summary>
    [Test]
    [DisplayName("Parse_MultipleNotEmpty_AllValid_ParsesSuccessfully")]
    public async Task Parse_MultipleNotEmpty_AllValid_ParsesSuccessfully()
    {
        // Arrange
        var desc = FileHelperRegistry<MultipleNotEmptyRecord>.Descriptor;

        // Act
        var record = desc.Parse("John,Smith,30".AsSpan());

        // Assert
        await Assert.That(record.FirstName).IsEqualTo("John");
        await Assert.That(record.LastName).IsEqualTo("Smith");
        await Assert.That(record.Age).IsEqualTo(30);
    }

    #endregion
}

#region Test Fixtures

/// <summary>Delimited record with [FieldNotEmpty] on string field.</summary>
[DelimitedRecord(",")]
internal sealed class NotEmptyDelimitedRecord
{
    [FieldOrder(1)]
    [FieldNotEmpty]
    public string Name = string.Empty;

    [FieldOrder(2)]
    public int Id;
}

/// <summary>Delimited record with both [FieldTrim] and [FieldNotEmpty].</summary>
[DelimitedRecord(",")]
internal sealed class NotEmptyTrimmedRecord
{
    [FieldOrder(1)]
    [FieldTrim(TrimMode.Both)]
    [FieldNotEmpty]
    public string Name = string.Empty;

    [FieldOrder(2)]
    public int Id;
}

/// <summary>Fixed-length record with [FieldNotEmpty].</summary>
[FixedLengthRecord]
internal sealed class NotEmptyFixedRecord
{
    [FieldOrder(1)]
    [FieldFixedLength(6)]
    [FieldNotEmpty]
    public string Name = string.Empty;

    [FieldOrder(2)]
    [FieldFixedLength(5)]
    public int Value;
}

/// <summary>Delimited record with multiple [FieldNotEmpty] fields.</summary>
[DelimitedRecord(",")]
internal sealed class MultipleNotEmptyRecord
{
    [FieldOrder(1)]
    [FieldNotEmpty]
    public string FirstName = string.Empty;

    [FieldOrder(2)]
    [FieldNotEmpty]
    public string LastName = string.Empty;

    [FieldOrder(3)]
    public int Age;
}

#endregion
