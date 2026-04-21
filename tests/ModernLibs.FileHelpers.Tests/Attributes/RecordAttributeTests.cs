// CA1707: Test methods intentionally use underscores for the Method_Scenario pattern.
#pragma warning disable CA1707

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Attributes;

// Test fixtures: minimal types that carry the attributes under test.
// These are compile-time checks — they verify the attribute can be applied to both class and struct.
[DelimitedRecord(",")]
file sealed class DelimitedClass { }

[DelimitedRecord("\t")]
file record struct DelimitedStruct { }

[FixedLengthRecord]
file sealed class FixedClass { }

[FixedLengthRecord(FixedMode.AllowMoreChars)]
file record struct FixedStruct { }

[IgnoreFirst]
[IgnoreLast(2)]
[IgnoreEmptyLines(true)]
[IgnoreCommentedLines('#')]
file sealed class FilteredRecord { }

/// <summary>Verifies record-level attribute construction, property values, and AttributeUsage metadata.</summary>
public sealed class RecordAttributeTests
{
    // ------- DelimitedRecordAttribute -------

    [Test]
    [DisplayName("DelimitedRecordAttribute constructor stores comma separator correctly")]
    public async Task DelimitedRecord_Separator_IsSet()
    {
        // Arrange & Act: Create attribute with comma separator
        var attr = new DelimitedRecordAttribute(",");

        // Assert: Separator property equals comma
        await Assert.That(attr.Separator).IsEqualTo(",");
    }

    [Test]
    [DisplayName("DelimitedRecordAttribute constructor stores tab separator correctly")]
    public async Task DelimitedRecord_Tab_Separator()
    {
        // Arrange & Act: Create attribute with tab separator
        var attr = new DelimitedRecordAttribute("\t");

        // Assert: Separator property equals tab
        await Assert.That(attr.Separator).IsEqualTo("\t");
    }

    [Test]
    [DisplayName("DelimitedRecordAttribute has AllowMultiple=false in AttributeUsage")]
    public async Task DelimitedRecord_AttributeUsage_AllowMultiple_IsFalse()
    {
        // Arrange: Get AttributeUsageAttribute for DelimitedRecordAttribute
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(DelimitedRecordAttribute), typeof(AttributeUsageAttribute))!;

        // Act: Read AllowMultiple property
        bool allowMultiple = usage.AllowMultiple;

        // Assert: AllowMultiple is false
        await Assert.That(allowMultiple).IsFalse();
    }

    [Test]
    [DisplayName("DelimitedRecordAttribute has Inherited=false in AttributeUsage")]
    public async Task DelimitedRecord_AttributeUsage_Inherited_IsFalse()
    {
        // Arrange: Get AttributeUsageAttribute for DelimitedRecordAttribute
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(DelimitedRecordAttribute), typeof(AttributeUsageAttribute))!;

        // Act: Read Inherited property
        bool inherited = usage.Inherited;

        // Assert: Inherited is false
        await Assert.That(inherited).IsFalse();
    }

    [Test]
    [DisplayName("DelimitedRecordAttribute targets Class and Struct in AttributeUsage")]
    public async Task DelimitedRecord_Targets_ClassAndStruct()
    {
        // Arrange: Get AttributeUsageAttribute for DelimitedRecordAttribute
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(DelimitedRecordAttribute), typeof(AttributeUsageAttribute))!;
        bool hasClass = usage.ValidOn.HasFlag(AttributeTargets.Class);

        // Act: Check for Struct target
        bool hasStruct = usage.ValidOn.HasFlag(AttributeTargets.Struct);

        // Assert: Targets both Class and Struct
        await Assert.That(hasClass).IsTrue();
        await Assert.That(hasStruct).IsTrue();
    }

    // ------- FixedLengthRecordAttribute -------

    [Test]
    [DisplayName("FixedLengthRecordAttribute default constructor sets FixedMode to ExactLength")]
    public async Task FixedLengthRecord_Default_IsExactLength()
    {
        // Arrange & Act: Create attribute with default constructor
        var attr = new FixedLengthRecordAttribute();

        // Assert: Default FixedMode is ExactLength
        await Assert.That(attr.FixedMode).IsEqualTo(FixedMode.ExactLength);
    }

    [Test]
    [DisplayName("FixedLengthRecordAttribute constructor with mode stores FixedMode correctly")]
    public async Task FixedLengthRecord_CtorWithMode()
    {
        // Arrange & Act: Create attribute with AllowMoreChars mode
        var attr = new FixedLengthRecordAttribute(FixedMode.AllowMoreChars);

        // Assert: FixedMode property equals AllowMoreChars
        await Assert.That(attr.FixedMode).IsEqualTo(FixedMode.AllowMoreChars);
    }

    [Test]
    [DisplayName("FixedLengthRecordAttribute targets Class and Struct in AttributeUsage")]
    public async Task FixedLengthRecord_Targets_ClassAndStruct()
    {
        // Arrange: Get AttributeUsageAttribute for FixedLengthRecordAttribute
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(FixedLengthRecordAttribute), typeof(AttributeUsageAttribute))!;
        bool hasClass = usage.ValidOn.HasFlag(AttributeTargets.Class);

        // Act: Check for Struct target
        bool hasStruct = usage.ValidOn.HasFlag(AttributeTargets.Struct);

        // Assert: Targets both Class and Struct
        await Assert.That(hasClass).IsTrue();
        await Assert.That(hasStruct).IsTrue();
    }

    // ------- IgnoreFirstAttribute -------

    [Test]
    [DisplayName("IgnoreFirstAttribute default constructor sets NumberOfLines to 1")]
    public async Task IgnoreFirst_Default_IsOne()
    {
        // Arrange & Act: Create attribute with default constructor
        var attr = new IgnoreFirstAttribute();

        // Assert: Default NumberOfLines is 1
        await Assert.That(attr.NumberOfLines).IsEqualTo(1);
    }

    [Test]
    [DisplayName("IgnoreFirstAttribute constructor stores custom line count correctly")]
    public async Task IgnoreFirst_CustomCount()
    {
        // Arrange & Act: Create attribute with custom line count
        var attr = new IgnoreFirstAttribute(3);

        // Assert: NumberOfLines property equals 3
        await Assert.That(attr.NumberOfLines).IsEqualTo(3);
    }

    // ------- IgnoreLastAttribute -------

    [Test]
    [DisplayName("IgnoreLastAttribute default constructor sets NumberOfLines to 1")]
    public async Task IgnoreLast_Default_IsOne()
    {
        // Arrange & Act: Create attribute with default constructor
        var attr = new IgnoreLastAttribute();

        // Assert: Default NumberOfLines is 1
        await Assert.That(attr.NumberOfLines).IsEqualTo(1);
    }

    [Test]
    [DisplayName("IgnoreLastAttribute constructor stores custom line count correctly")]
    public async Task IgnoreLast_CustomCount()
    {
        // Arrange & Act: Create attribute with custom line count
        var attr = new IgnoreLastAttribute(5);

        // Assert: NumberOfLines property equals 5
        await Assert.That(attr.NumberOfLines).IsEqualTo(5);
    }

    // ------- IgnoreEmptyLinesAttribute -------

    [Test]
    [DisplayName("IgnoreEmptyLinesAttribute default constructor sets IgnoreSpaces to false")]
    public async Task IgnoreEmptyLines_Default_IgnoreSpacesFalse()
    {
        // Arrange & Act: Create attribute with default constructor
        var attr = new IgnoreEmptyLinesAttribute();

        // Act: Read IgnoreSpaces property
        bool ignoreSpaces = attr.IgnoreSpaces;

        // Assert: Default IgnoreSpaces is false
        await Assert.That(ignoreSpaces).IsFalse();
    }

    [Test]
    [DisplayName("IgnoreEmptyLinesAttribute constructor with true sets IgnoreSpaces to true")]
    public async Task IgnoreEmptyLines_IgnoreSpacesTrue()
    {
        // Arrange & Act: Create attribute with IgnoreSpaces=true
        var attr = new IgnoreEmptyLinesAttribute(true);

        // Act: Read IgnoreSpaces property
        bool ignoreSpaces = attr.IgnoreSpaces;

        // Assert: IgnoreSpaces is true
        await Assert.That(ignoreSpaces).IsTrue();
    }

    // ------- IgnoreCommentedLinesAttribute -------

    [Test]
    [DisplayName("IgnoreCommentedLinesAttribute defaults AnyPlace to true for comment marker")]
    public async Task IgnoreCommentedLines_DefaultAnyPlaceIsTrue()
    {
        // Arrange & Act: Create attribute with comment marker
        var attr = new IgnoreCommentedLinesAttribute('#');

        // Assert: CommentMarker and AnyPlace set correctly
        await Assert.That(attr.CommentMarker).IsEqualTo('#');
        bool anyPlace = attr.AnyPlace;
        await Assert.That(anyPlace).IsTrue();
    }

    [Test]
    [DisplayName("IgnoreCommentedLinesAttribute constructor with AnyPlace=false stores both values")]
    public async Task IgnoreCommentedLines_AnyPlaceFalse()
    {
        // Arrange & Act: Create attribute with AnyPlace=false
        var attr = new IgnoreCommentedLinesAttribute(';', false);

        // Assert: CommentMarker and AnyPlace set correctly
        await Assert.That(attr.CommentMarker).IsEqualTo(';');
        bool anyPlace = attr.AnyPlace;
        await Assert.That(anyPlace).IsFalse();
    }
}
