// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0048: File name matches primary test class; helper records at namespace level are intentional.
#pragma warning disable MA0048
// MA0048: Multiple test types per file — intentional for related test fixtures.
#pragma warning disable MA0048

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Attributes;

/// <summary>
/// TDD RED tests for [FieldDelimiter] attribute (FDEL-01).
/// These tests define the contract before implementation (Wave 0).
/// </summary>
[Category("Phase17")]
[Category("Attributes")]
public sealed class FieldDelimiterTests
{
    // ------- FieldDelimiterAttribute construction -------

    [Test]
    [DisplayName("FieldDelimiterAttribute constructor stores delimiter string correctly")]
    public async Task AttributeConstruction_SetsDelimiterProperty()
    {
        // Arrange & Act: Create attribute with semicolon delimiter
        var attr = new FieldDelimiterAttribute(";");

        // Assert: Delimiter property equals ";"
        await Assert.That(attr.Delimiter).IsEqualTo(";");
    }

    [Test]
    [DisplayName("FieldDelimiterAttribute supports multi-character delimiter")]
    public async Task AttributeConstruction_SetsMultiCharDelimiter()
    {
        // Arrange & Act: Create attribute with multi-char delimiter
        var attr = new FieldDelimiterAttribute("|~|");

        // Assert: Delimiter property equals "|~|"
        await Assert.That(attr.Delimiter).IsEqualTo("|~|");
    }

    [Test]
    [DisplayName("FieldDelimiterAttribute supports tab delimiter")]
    public async Task AttributeConstruction_SetsTabDelimiter()
    {
        // Arrange & Act: Create attribute with tab delimiter
        var attr = new FieldDelimiterAttribute("\t");

        // Assert: Delimiter property equals "\t"
        await Assert.That(attr.Delimiter).IsEqualTo("\t");
    }

    // ------- FieldDelimiterAttribute AttributeUsage -------

    [Test]
    [DisplayName("FieldDelimiterAttribute targets Field and Property in AttributeUsage")]
    public async Task AttributeUsage_IsFieldAndPropertyOnly()
    {
        // Arrange: Get AttributeUsageAttribute for FieldDelimiterAttribute
        var usage = GetUsage<FieldDelimiterAttribute>();
        bool hasField = usage.ValidOn.HasFlag(AttributeTargets.Field);
        bool hasProperty = usage.ValidOn.HasFlag(AttributeTargets.Property);

        // Assert: Targets both Field and Property
        await Assert.That(hasField).IsTrue();
        await Assert.That(hasProperty).IsTrue();
    }

    [Test]
    [DisplayName("FieldDelimiterAttribute has AllowMultiple=false")]
    public async Task AttributeUsage_DoesNotAllowMultiple()
    {
        // Arrange: Get AttributeUsageAttribute
        var usage = GetUsage<FieldDelimiterAttribute>();

        // Act: Check AllowMultiple property
        bool allowMultiple = usage.AllowMultiple;

        // Assert: AllowMultiple is false
        await Assert.That(allowMultiple).IsFalse();
    }

    [Test]
    [DisplayName("FieldDelimiterAttribute has Inherited=false")]
    public async Task AttributeUsage_NotInherited()
    {
        // Arrange: Get AttributeUsageAttribute
        var usage = GetUsage<FieldDelimiterAttribute>();

        // Act: Check Inherited property
        bool inherited = usage.Inherited;

        // Assert: Inherited is false
        await Assert.That(inherited).IsFalse();
    }

    // ------- Compile-time attribute application tests -------

    [Test]
    [DisplayName("FieldDelimiterAttribute can be applied to a property (compile test)")]
    public async Task AttributeAppliedToProperty_Compiles()
    {
        // This test verifies the attribute can be applied to properties
        // The test record below uses the attribute - if it compiles, the test passes

        // Arrange: Define a record with FieldDelimiter on a property
        var record = new TestPropertyRecord { Field1 = "a", Field2 = "b", Field3 = "c" };

        // Act & Assert: If this compiles, the attribute application works
        await Assert.That(record.Field1).IsEqualTo("a");
        await Assert.That(record.Field2).IsEqualTo("b");
        await Assert.That(record.Field3).IsEqualTo("c");
    }

    [Test]
    [DisplayName("FieldDelimiterAttribute can be applied to a field (compile test)")]
    public async Task AttributeAppliedToField_Compiles()
    {
        // This test verifies the attribute can be applied to fields

        // Arrange: Define a record with FieldDelimiter on a field
        var record = new TestFieldRecord { Field1 = "x", Field2 = "y", Field3 = "z" };

        // Act & Assert: If this compiles, the attribute application works
        await Assert.That(record.Field1).IsEqualTo("x");
        await Assert.That(record.Field2).IsEqualTo("y");
        await Assert.That(record.Field3).IsEqualTo("z");
    }

    // Helper
    private static AttributeUsageAttribute GetUsage<T>() where T : Attribute =>
        (AttributeUsageAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(AttributeUsageAttribute))!;
}

// ------- Test record types for compile-time verification -------
// These are at namespace level so the generator can access them

/// <summary>
/// Test record for property-based FieldDelimiter application.
/// Uses comma at record level, semicolon override for Field2.
/// </summary>
[DelimitedRecord(",")]
internal sealed record TestPropertyRecord
{
    [FieldOrder(1)]
    public string Field1 { get; set; } = "";

    [FieldOrder(2)]
    [FieldDelimiter(";")]
    public string Field2 { get; set; } = "";

    [FieldOrder(3)]
    public string Field3 { get; set; } = "";
}

/// <summary>
/// Test record for field-based FieldDelimiter application.
/// Uses comma at record level, pipe override for Field2.
/// </summary>
[DelimitedRecord(",")]
internal sealed record TestFieldRecord
{
    [FieldOrder(1)]
    public string Field1 = "";

    [FieldOrder(2)]
    [FieldDelimiter("|")]
    public string Field2 = "";

    [FieldOrder(3)]
    public string Field3 = "";
}
