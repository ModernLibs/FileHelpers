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
    public async Task OrderRecord_HasDelimitedRecordAttribute()
    {
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var orderType = Array.Find(types, t => t.Name == "OrderRecord");
        await Assert.That(orderType).IsNotNull();
        var attr = (DelimitedRecordAttribute?)Attribute.GetCustomAttribute(orderType!, typeof(DelimitedRecordAttribute));
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.Separator).IsEqualTo(",");
    }

    [Test]
    public async Task OrderRecord_HasIgnoreFirstAttribute()
    {
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var orderType = Array.Find(types, t => t.Name == "OrderRecord")!;
        var attr = (IgnoreFirstAttribute?)Attribute.GetCustomAttribute(orderType, typeof(IgnoreFirstAttribute));
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.NumberOfLines).IsEqualTo(1);
    }

    [Test]
    public async Task OrderRecord_HasIgnoreEmptyLinesAttribute()
    {
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var orderType = Array.Find(types, t => t.Name == "OrderRecord")!;
        var attr = Attribute.GetCustomAttribute(orderType, typeof(IgnoreEmptyLinesAttribute));
        await Assert.That(attr).IsNotNull();
    }

    [Test]
    public async Task OrderRecord_HasIgnoreCommentedLinesAttribute()
    {
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var orderType = Array.Find(types, t => t.Name == "OrderRecord")!;
        var attr = (IgnoreCommentedLinesAttribute?)Attribute.GetCustomAttribute(orderType, typeof(IgnoreCommentedLinesAttribute));
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.CommentMarker).IsEqualTo('#');
        bool anyPlace = attr.AnyPlace;
        await Assert.That(anyPlace).IsTrue();
    }

    // ---- Fixed-length class ----

    [Test]
    public async Task FixedRecord_HasFixedLengthRecordAttribute()
    {
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var fixedType = Array.Find(types, t => t.Name == "FixedRecord")!;
        var attr = (FixedLengthRecordAttribute?)Attribute.GetCustomAttribute(fixedType, typeof(FixedLengthRecordAttribute));
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.FixedMode).IsEqualTo(FixedMode.ExactLength);
    }

    [Test]
    public async Task FixedRecord_HasIgnoreLastAttribute()
    {
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var fixedType = Array.Find(types, t => t.Name == "FixedRecord")!;
        var attr = (IgnoreLastAttribute?)Attribute.GetCustomAttribute(fixedType, typeof(IgnoreLastAttribute));
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.NumberOfLines).IsEqualTo(1);
    }

    // ---- Readonly record struct ----

    [Test]
    public async Task FixedStructRecord_HasFixedLengthRecordAttribute()
    {
        var types = typeof(IntegrationCompileTests).Assembly.GetTypes();
        var structType = Array.Find(types, t => t.Name == "FixedStructRecord")!;
        var attr = Attribute.GetCustomAttribute(structType, typeof(FixedLengthRecordAttribute));
        await Assert.That(attr).IsNotNull();
    }

    // ---- Full namespace and sealed verification ----

    [Test]
    public async Task AllAttributeNamespaces_AreInModernLibsFileHelpers()
    {
        // All attribute types must live in the ModernLibs.FileHelpers namespace (not sub-namespaces)
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
            await Assert.That(t.Namespace).IsEqualTo("ModernLibs.FileHelpers");
        }
    }

    [Test]
    public async Task AllAttributeTypes_AreSealed()
    {
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
            bool isSealed = t.IsSealed;
            await Assert.That(isSealed).IsTrue();
        }
    }
}
