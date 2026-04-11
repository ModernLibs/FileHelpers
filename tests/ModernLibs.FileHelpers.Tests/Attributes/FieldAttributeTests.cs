// CA1707: Test methods intentionally use underscores for the Method_Scenario pattern.
#pragma warning disable CA1707

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Attributes;

/// <summary>Verifies field-level attribute construction, property values, and AttributeUsage metadata.</summary>
public sealed class FieldAttributeTests
{
    // ------- FieldOrderAttribute -------

    [Test]
    public async Task FieldOrder_Order_IsSet()
    {
        var attr = new FieldOrderAttribute(3);
        await Assert.That(attr.Order).IsEqualTo(3);
    }

    [Test]
    public async Task FieldOrder_Targets_FieldAndProperty()
    {
        var usage = GetUsage<FieldOrderAttribute>();
        bool hasField = usage.ValidOn.HasFlag(AttributeTargets.Field);
        bool hasProperty = usage.ValidOn.HasFlag(AttributeTargets.Property);
        await Assert.That(hasField).IsTrue();
        await Assert.That(hasProperty).IsTrue();
    }

    // ------- FieldFixedLengthAttribute -------

    [Test]
    public async Task FieldFixedLength_Length_IsSet()
    {
        var attr = new FieldFixedLengthAttribute(10);
        await Assert.That(attr.Length).IsEqualTo(10);
    }

    [Test]
    public async Task FieldFixedLength_OverflowMode_DefaultIsDiscardEnd()
    {
        var attr = new FieldFixedLengthAttribute(10);
        await Assert.That(attr.OverflowMode).IsEqualTo(OverflowMode.DiscardEnd);
    }

    [Test]
    public async Task FieldFixedLength_OverflowMode_CanBeOverriddenViaInit()
    {
        var attr = new FieldFixedLengthAttribute(10) { OverflowMode = OverflowMode.Error };
        await Assert.That(attr.OverflowMode).IsEqualTo(OverflowMode.Error);
    }

    // ------- FieldAlignAttribute -------

    [Test]
    public async Task FieldAlign_DefaultChar_IsSpace()
    {
        var attr = new FieldAlignAttribute(AlignMode.Right);
        await Assert.That(attr.Align).IsEqualTo(AlignMode.Right);
        await Assert.That(attr.AlignChar).IsEqualTo(' ');
    }

    [Test]
    public async Task FieldAlign_CustomChar()
    {
        var attr = new FieldAlignAttribute(AlignMode.Left, '0');
        await Assert.That(attr.AlignChar).IsEqualTo('0');
    }

    // ------- FieldTrimAttribute -------

    [Test]
    public async Task FieldTrim_Default_TrimsSpaceAndTab()
    {
        var attr = new FieldTrimAttribute(TrimMode.Both);
        await Assert.That(attr.TrimMode).IsEqualTo(TrimMode.Both);
        bool hasSpace = attr.TrimChars.Contains(' ');
        bool hasTab = attr.TrimChars.Contains('\t');
        await Assert.That(hasSpace).IsTrue();
        await Assert.That(hasTab).IsTrue();
    }

    [Test]
    public async Task FieldTrim_CustomChars()
    {
        var attr = new FieldTrimAttribute(TrimMode.Left, '|', '-');
        await Assert.That(attr.TrimChars.Length).IsEqualTo(2);
        bool hasPipe = attr.TrimChars.Contains('|');
        await Assert.That(hasPipe).IsTrue();
    }

    // ------- FieldQuotedAttribute -------

    [Test]
    public async Task FieldQuoted_Default_DoubleQuoteOptionalForRead()
    {
        var attr = new FieldQuotedAttribute();
        await Assert.That(attr.QuoteChar).IsEqualTo('"');
        await Assert.That(attr.QuoteMode).IsEqualTo(QuoteMode.OptionalForRead);
    }

    [Test]
    public async Task FieldQuoted_CustomChar()
    {
        var attr = new FieldQuotedAttribute('\'');
        await Assert.That(attr.QuoteChar).IsEqualTo('\'');
        await Assert.That(attr.QuoteMode).IsEqualTo(QuoteMode.OptionalForRead);
    }

    [Test]
    public async Task FieldQuoted_ModeOnly()
    {
        var attr = new FieldQuotedAttribute(QuoteMode.AlwaysQuoted);
        await Assert.That(attr.QuoteChar).IsEqualTo('"');
        await Assert.That(attr.QuoteMode).IsEqualTo(QuoteMode.AlwaysQuoted);
    }

    [Test]
    public async Task FieldQuoted_CharAndMode()
    {
        var attr = new FieldQuotedAttribute('\'', QuoteMode.OptionalForBoth);
        await Assert.That(attr.QuoteChar).IsEqualTo('\'');
        await Assert.That(attr.QuoteMode).IsEqualTo(QuoteMode.OptionalForBoth);
    }

    // ------- FieldConverterAttribute -------

    [Test]
    public async Task FieldConverter_KindOverload()
    {
        var attr = new FieldConverterAttribute(ConverterKind.Date, "yyyy-MM-dd");
        await Assert.That(attr.Kind).IsEqualTo(ConverterKind.Date);
        await Assert.That(attr.ConverterType).IsNull();
        await Assert.That(attr.ConverterArgs.Length).IsEqualTo(1);
    }

    [Test]
    public async Task FieldConverter_TypeOverload()
    {
        var attr = new FieldConverterAttribute(typeof(object));
        await Assert.That(attr.Kind).IsEqualTo(ConverterKind.None);
        await Assert.That(attr.ConverterType).IsEqualTo(typeof(object));
    }

    // ------- FieldNullValueAttribute -------

    [Test]
    public async Task FieldNullValue_IsSet()
    {
        var attr = new FieldNullValueAttribute("N/A");
        await Assert.That(attr.NullValue).IsEqualTo("N/A");
    }

    [Test]
    public async Task FieldNullValue_NullArgument()
    {
        var attr = new FieldNullValueAttribute(null);
        await Assert.That(attr.NullValue).IsNull();
    }

    // ------- Marker attributes (no properties) -------

    [Test]
    public async Task FieldOptional_CanBeInstantiated()
    {
        var attr = new FieldOptionalAttribute();
        await Assert.That(attr).IsNotNull();
    }

    [Test]
    public async Task FieldHidden_CanBeInstantiated()
    {
        var attr = new FieldHiddenAttribute();
        await Assert.That(attr).IsNotNull();
    }

    [Test]
    public async Task FieldNotEmpty_CanBeInstantiated()
    {
        var attr = new FieldNotEmptyAttribute();
        await Assert.That(attr).IsNotNull();
    }

    [Test]
    public async Task FieldValueDiscarded_CanBeInstantiated()
    {
        var attr = new FieldValueDiscardedAttribute();
        await Assert.That(attr).IsNotNull();
    }

    [Test]
    public async Task FieldInNewLine_CanBeInstantiated()
    {
        var attr = new FieldInNewLineAttribute();
        await Assert.That(attr).IsNotNull();
    }

    // ------- FieldNotInFileAttribute (obsolete alias) -------

#pragma warning disable CS0618 // Type or member is obsolete — intentional: testing the obsolete attribute itself
    [Test]
    public async Task FieldNotInFile_IsObsolete()
    {
        var obsolete = (ObsoleteAttribute?)Attribute.GetCustomAttribute(
            typeof(FieldNotInFileAttribute), typeof(ObsoleteAttribute));
        await Assert.That(obsolete).IsNotNull();
        bool isError = obsolete!.IsError;
        await Assert.That(isError).IsFalse();
    }

    [Test]
    public async Task FieldNotInFile_Targets_FieldOnly()
    {
        var usage = GetUsage<FieldNotInFileAttribute>();
        bool hasField = usage.ValidOn.HasFlag(AttributeTargets.Field);
        bool hasProperty = usage.ValidOn.HasFlag(AttributeTargets.Property);
        await Assert.That(hasField).IsTrue();
        await Assert.That(hasProperty).IsFalse();
    }
#pragma warning restore CS0618

    // ------- AllowMultiple = false on all field attributes -------

    [Test]
    public async Task AllFieldAttributes_AllowMultiple_IsFalse()
    {
        Type[] types =
        [
            typeof(FieldOrderAttribute), typeof(FieldFixedLengthAttribute), typeof(FieldAlignAttribute),
            typeof(FieldTrimAttribute), typeof(FieldQuotedAttribute), typeof(FieldConverterAttribute),
            typeof(FieldNullValueAttribute), typeof(FieldOptionalAttribute), typeof(FieldHiddenAttribute),
            typeof(FieldNotEmptyAttribute), typeof(FieldValueDiscardedAttribute), typeof(FieldInNewLineAttribute),
        ];
        foreach (var t in types)
        {
            var usage = GetUsage(t);
            bool allowMultiple = usage.AllowMultiple;
            await Assert.That(allowMultiple).IsFalse();
        }
    }

    // Helper
    private static AttributeUsageAttribute GetUsage<T>() where T : Attribute => GetUsage(typeof(T));
    private static AttributeUsageAttribute GetUsage(Type t) =>
        (AttributeUsageAttribute)Attribute.GetCustomAttribute(t, typeof(AttributeUsageAttribute))!;
}
