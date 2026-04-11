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
    public async Task DelimitedRecord_Separator_IsSet()
    {
        var attr = new DelimitedRecordAttribute(",");
        await Assert.That(attr.Separator).IsEqualTo(",");
    }

    [Test]
    public async Task DelimitedRecord_Tab_Separator()
    {
        var attr = new DelimitedRecordAttribute("\t");
        await Assert.That(attr.Separator).IsEqualTo("\t");
    }

    [Test]
    public async Task DelimitedRecord_AttributeUsage_AllowMultiple_IsFalse()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(DelimitedRecordAttribute), typeof(AttributeUsageAttribute))!;
        bool allowMultiple = usage.AllowMultiple;
        await Assert.That(allowMultiple).IsFalse();
    }

    [Test]
    public async Task DelimitedRecord_AttributeUsage_Inherited_IsFalse()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(DelimitedRecordAttribute), typeof(AttributeUsageAttribute))!;
        bool inherited = usage.Inherited;
        await Assert.That(inherited).IsFalse();
    }

    [Test]
    public async Task DelimitedRecord_Targets_ClassAndStruct()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(DelimitedRecordAttribute), typeof(AttributeUsageAttribute))!;
        bool hasClass = usage.ValidOn.HasFlag(AttributeTargets.Class);
        bool hasStruct = usage.ValidOn.HasFlag(AttributeTargets.Struct);
        await Assert.That(hasClass).IsTrue();
        await Assert.That(hasStruct).IsTrue();
    }

    // ------- FixedLengthRecordAttribute -------

    [Test]
    public async Task FixedLengthRecord_Default_IsExactLength()
    {
        var attr = new FixedLengthRecordAttribute();
        await Assert.That(attr.FixedMode).IsEqualTo(FixedMode.ExactLength);
    }

    [Test]
    public async Task FixedLengthRecord_CtorWithMode()
    {
        var attr = new FixedLengthRecordAttribute(FixedMode.AllowMoreChars);
        await Assert.That(attr.FixedMode).IsEqualTo(FixedMode.AllowMoreChars);
    }

    [Test]
    public async Task FixedLengthRecord_Targets_ClassAndStruct()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(
            typeof(FixedLengthRecordAttribute), typeof(AttributeUsageAttribute))!;
        bool hasClass = usage.ValidOn.HasFlag(AttributeTargets.Class);
        bool hasStruct = usage.ValidOn.HasFlag(AttributeTargets.Struct);
        await Assert.That(hasClass).IsTrue();
        await Assert.That(hasStruct).IsTrue();
    }

    // ------- IgnoreFirstAttribute -------

    [Test]
    public async Task IgnoreFirst_Default_IsOne()
    {
        var attr = new IgnoreFirstAttribute();
        await Assert.That(attr.NumberOfLines).IsEqualTo(1);
    }

    [Test]
    public async Task IgnoreFirst_CustomCount()
    {
        var attr = new IgnoreFirstAttribute(3);
        await Assert.That(attr.NumberOfLines).IsEqualTo(3);
    }

    // ------- IgnoreLastAttribute -------

    [Test]
    public async Task IgnoreLast_Default_IsOne()
    {
        var attr = new IgnoreLastAttribute();
        await Assert.That(attr.NumberOfLines).IsEqualTo(1);
    }

    [Test]
    public async Task IgnoreLast_CustomCount()
    {
        var attr = new IgnoreLastAttribute(5);
        await Assert.That(attr.NumberOfLines).IsEqualTo(5);
    }

    // ------- IgnoreEmptyLinesAttribute -------

    [Test]
    public async Task IgnoreEmptyLines_Default_IgnoreSpacesFalse()
    {
        var attr = new IgnoreEmptyLinesAttribute();
        bool ignoreSpaces = attr.IgnoreSpaces;
        await Assert.That(ignoreSpaces).IsFalse();
    }

    [Test]
    public async Task IgnoreEmptyLines_IgnoreSpacesTrue()
    {
        var attr = new IgnoreEmptyLinesAttribute(true);
        bool ignoreSpaces = attr.IgnoreSpaces;
        await Assert.That(ignoreSpaces).IsTrue();
    }

    // ------- IgnoreCommentedLinesAttribute -------

    [Test]
    public async Task IgnoreCommentedLines_DefaultAnyPlaceIsTrue()
    {
        var attr = new IgnoreCommentedLinesAttribute('#');
        await Assert.That(attr.CommentMarker).IsEqualTo('#');
        bool anyPlace = attr.AnyPlace;
        await Assert.That(anyPlace).IsTrue();
    }

    [Test]
    public async Task IgnoreCommentedLines_AnyPlaceFalse()
    {
        var attr = new IgnoreCommentedLinesAttribute(';', false);
        await Assert.That(attr.CommentMarker).IsEqualTo(';');
        bool anyPlace = attr.AnyPlace;
        await Assert.That(anyPlace).IsFalse();
    }
}
