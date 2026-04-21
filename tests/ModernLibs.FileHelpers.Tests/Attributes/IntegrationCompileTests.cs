#pragma warning disable CA1707  // CA1707: Test methods intentionally use underscores per TUnit convention
#pragma warning disable MA0006  // MA0006: String equality in test lambdas — ordinal comparison not needed for type name lookup
#pragma warning disable CS0618  // CS0618: FieldNotInFileAttribute is intentionally referenced to verify its [Obsolete] surface

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Attributes;

/// <summary>
/// Verifies that fixture types carrying the full attribute surface are correctly decorated —
/// attributes are present, not duplicated, and their properties carry expected values.
/// These tests confirm the complete attribute composition works end-to-end.
/// </summary>
public sealed class IntegrationCompileTests
{
    // ---- Delimited record struct ----

    [Test]
    [DisplayName("OrderRecord type has DelimitedRecordAttribute with Separator=\",\"")]
    public async Task OrderRecord_HasDelimitedRecordAttribute()
    {
        // Arrange: Find OrderRecord type in test assembly
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();

        // Act: Locate OrderRecord type
        var orderType = Array.Find(types, t => t.Name == "OrderRecord");

        // Assert: Type found with DelimitedRecordAttribute having comma separator
        await Assert.That(orderType).IsNotNull();
        var attr = (DelimitedRecordAttribute?)Attribute.GetCustomAttribute(orderType!, typeof(DelimitedRecordAttribute));
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.Separator).IsEqualTo(",");
    }

    [Test]
    [DisplayName("OrderRecord type has IgnoreFirstAttribute with NumberOfLines=1")]
    public async Task OrderRecord_HasIgnoreFirstAttribute()
    {
        // Arrange: Find OrderRecord type
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var orderType = Array.Find(types, t => t.Name == "OrderRecord")!;

        // Act: Get IgnoreFirstAttribute
        var attr = (IgnoreFirstAttribute?)Attribute.GetCustomAttribute(orderType, typeof(IgnoreFirstAttribute));

        // Assert: Attribute present with NumberOfLines=1
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.NumberOfLines).IsEqualTo(1);
    }

    [Test]
    [DisplayName("OrderRecord type has IgnoreEmptyLinesAttribute applied")]
    public async Task OrderRecord_HasIgnoreEmptyLinesAttribute()
    {
        // Arrange: Find OrderRecord type
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var orderType = Array.Find(types, t => t.Name == "OrderRecord")!;

        // Act: Get IgnoreEmptyLinesAttribute
        var attr = Attribute.GetCustomAttribute(orderType, typeof(IgnoreEmptyLinesAttribute));

        // Assert: Attribute is present
        await Assert.That(attr).IsNotNull();
    }

    [Test]
    [DisplayName("OrderRecord type has IgnoreCommentedLinesAttribute with CommentMarker='#' and AnyPlace=true")]
    public async Task OrderRecord_HasIgnoreCommentedLinesAttribute()
    {
        // Arrange: Find OrderRecord type
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var orderType = Array.Find(types, t => t.Name == "OrderRecord")!;

        // Act: Get IgnoreCommentedLinesAttribute
        var attr = (IgnoreCommentedLinesAttribute?)Attribute.GetCustomAttribute(orderType, typeof(IgnoreCommentedLinesAttribute));

        // Assert: Attribute present with # marker and AnyPlace=true
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.CommentMarker).IsEqualTo('#');
        bool anyPlace = attr.AnyPlace;
        await Assert.That(anyPlace).IsTrue();
    }

    // ---- Fixed-length class ----

    [Test]
    [DisplayName("FixedRecord type has FixedLengthRecordAttribute with FixedMode=ExactLength")]
    public async Task FixedRecord_HasFixedLengthRecordAttribute()
    {
        // Arrange: Find FixedRecord type
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var fixedType = Array.Find(types, t => t.Name == "FixedRecord")!;

        // Act: Get FixedLengthRecordAttribute
        var attr = (FixedLengthRecordAttribute?)Attribute.GetCustomAttribute(fixedType, typeof(FixedLengthRecordAttribute));

        // Assert: Attribute present with ExactLength mode
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.FixedMode).IsEqualTo(FixedMode.ExactLength);
    }

    [Test]
    [DisplayName("FixedRecord type has IgnoreLastAttribute with NumberOfLines=1")]
    public async Task FixedRecord_HasIgnoreLastAttribute()
    {
        // Arrange: Find FixedRecord type
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var fixedType = Array.Find(types, t => t.Name == "FixedRecord")!;

        // Act: Get IgnoreLastAttribute
        var attr = (IgnoreLastAttribute?)Attribute.GetCustomAttribute(fixedType, typeof(IgnoreLastAttribute));

        // Assert: Attribute present with NumberOfLines=1
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.NumberOfLines).IsEqualTo(1);
    }

    // ---- Readonly record struct ----

    [Test]
    [DisplayName("FixedStructRecord type has FixedLengthRecordAttribute applied")]
    public async Task FixedStructRecord_HasFixedLengthRecordAttribute()
    {
        // Arrange: Find FixedStructRecord type
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var structType = Array.Find(types, t => t.Name == "FixedStructRecord")!;

        // Act: Get FixedLengthRecordAttribute
        var attr = Attribute.GetCustomAttribute(structType, typeof(FixedLengthRecordAttribute));

        // Assert: Attribute is present
        await Assert.That(attr).IsNotNull();
    }

    // ---- Full namespace and sealed verification ----

    [Test]
    [DisplayName("All attribute types reside in ModernLibs.FileHelpers namespace")]
    public async Task AllAttributeNamespaces_AreInModernLibsFileHelpers()
    {
        // All attribute types must live in the ModernLibs.FileHelpers namespace (not sub-namespaces)
        // Arrange: List of all attribute types to check
        Type[] attrTypes =
        [
            typeof(DelimitedRecordAttribute), typeof(FixedLengthRecordAttribute),
            typeof(IgnoreFirstAttribute), typeof(IgnoreLastAttribute),
            typeof(IgnoreEmptyLinesAttribute), typeof(IgnoreCommentedLinesAttribute),
            typeof(FieldOrderAttribute), typeof(FieldFixedLengthAttribute),
            typeof(FieldAlignAttribute), typeof(FieldTrimAttribute),
            typeof(FieldQuotedAttribute), typeof(FieldConverterAttribute),
            typeof(FieldNullValueAttribute), typeof(FieldOptionalAttribute),
            typeof(FieldHiddenAttribute), typeof(FieldNotInFileAttribute),
            typeof(FieldNotEmptyAttribute), typeof(FieldValueDiscardedAttribute),
            typeof(FieldInNewLineAttribute),
        ];
        foreach (var t in attrTypes)
        {
            // Act: Check namespace

            // Assert: All attributes in correct namespace
            await Assert.That(t.Namespace).IsEqualTo("ModernLibs.FileHelpers");
        }
    }

    [Test]
    [DisplayName("All attribute types are declared as sealed")]
    public async Task AllAttributeTypes_AreSealed()
    {
        // Arrange: List of all attribute types to check
        Type[] attrTypes =
        [
            typeof(DelimitedRecordAttribute), typeof(FixedLengthRecordAttribute),
            typeof(IgnoreFirstAttribute), typeof(IgnoreLastAttribute),
            typeof(IgnoreEmptyLinesAttribute), typeof(IgnoreCommentedLinesAttribute),
            typeof(FieldOrderAttribute), typeof(FieldFixedLengthAttribute),
            typeof(FieldAlignAttribute), typeof(FieldTrimAttribute),
            typeof(FieldQuotedAttribute), typeof(FieldConverterAttribute),
            typeof(FieldNullValueAttribute), typeof(FieldOptionalAttribute),
            typeof(FieldHiddenAttribute), typeof(FieldNotInFileAttribute),
            typeof(FieldNotEmptyAttribute), typeof(FieldValueDiscardedAttribute),
            typeof(FieldInNewLineAttribute),
        ];
        foreach (var t in attrTypes)
        {
            // Act: Check if type is sealed
            bool isSealed = t.IsSealed;

            // Assert: All attribute types are sealed
            await Assert.That(isSealed).IsTrue();
        }
    }
}
