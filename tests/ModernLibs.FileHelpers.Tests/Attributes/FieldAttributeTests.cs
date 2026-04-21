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
    [DisplayName("FieldOrderAttribute constructor stores order value correctly")]
    public async Task FieldOrder_Order_IsSet()
    {
        // Arrange & Act: Create attribute with order 3
        var attr = new FieldOrderAttribute(3);

        // Assert: Order property equals 3
        await Assert.That(attr.Order).IsEqualTo(3);
    }

    [Test]
    [DisplayName("FieldOrderAttribute targets Field and Property in AttributeUsage")]
    public async Task FieldOrder_Targets_FieldAndProperty()
    {
        // Arrange: Get AttributeUsageAttribute for FieldOrderAttribute
        var usage = GetUsage<FieldOrderAttribute>();
        bool hasField = usage.ValidOn.HasFlag(AttributeTargets.Field);

        // Act: Check for Property target
        bool hasProperty = usage.ValidOn.HasFlag(AttributeTargets.Property);

        // Assert: Targets both Field and Property
        await Assert.That(hasField).IsTrue();
        await Assert.That(hasProperty).IsTrue();
    }

    // ------- FieldFixedLengthAttribute -------

    [Test]
    [DisplayName("FieldFixedLengthAttribute constructor stores length value correctly")]
    public async Task FieldFixedLength_Length_IsSet()
    {
        // Arrange & Act: Create attribute with length 10
        var attr = new FieldFixedLengthAttribute(10);

        // Assert: Length property equals 10
        await Assert.That(attr.Length).IsEqualTo(10);
    }

    [Test]
    [DisplayName("FieldFixedLengthAttribute default OverflowMode is DiscardEnd")]
    public async Task FieldFixedLength_OverflowMode_DefaultIsDiscardEnd()
    {
        // Arrange & Act: Create attribute with default constructor
        var attr = new FieldFixedLengthAttribute(10);

        // Assert: Default OverflowMode is DiscardEnd
        await Assert.That(attr.OverflowMode).IsEqualTo(OverflowMode.DiscardEnd);
    }

    [Test]
    [DisplayName("FieldFixedLengthAttribute OverflowMode can be set via init-only property")]
    public async Task FieldFixedLength_OverflowMode_CanBeOverriddenViaInit()
    {
        // Arrange & Act: Create attribute with OverflowMode set via init
        var attr = new FieldFixedLengthAttribute(10) { OverflowMode = OverflowMode.Error };

        // Assert: OverflowMode property equals Error
        await Assert.That(attr.OverflowMode).IsEqualTo(OverflowMode.Error);
    }

    // ------- FieldAlignAttribute -------

    [Test]
    [DisplayName("FieldAlignAttribute default AlignChar is space when only AlignMode specified")]
    public async Task FieldAlign_DefaultChar_IsSpace()
    {
        // Arrange & Act: Create attribute with only AlignMode
        var attr = new FieldAlignAttribute(AlignMode.Right);

        // Assert: Align is Right, AlignChar defaults to space
        await Assert.That(attr.Align).IsEqualTo(AlignMode.Right);
        await Assert.That(attr.AlignChar).IsEqualTo(' ');
    }

    [Test]
    [DisplayName("FieldAlignAttribute constructor with custom char stores both values")]
    public async Task FieldAlign_CustomChar()
    {
        // Arrange & Act: Create attribute with custom align character
        var attr = new FieldAlignAttribute(AlignMode.Left, '0');

        // Assert: AlignChar equals '0'
        await Assert.That(attr.AlignChar).IsEqualTo('0');
    }

    // ------- FieldTrimAttribute -------

    [Test]
    [DisplayName("FieldTrimAttribute default TrimChars includes space and tab")]
    public async Task FieldTrim_Default_TrimsSpaceAndTab()
    {
        // Arrange & Act: Create attribute with TrimMode.Both
        var attr = new FieldTrimAttribute(TrimMode.Both);

        // Assert: TrimMode is Both, TrimChars includes space and tab
        await Assert.That(attr.TrimMode).IsEqualTo(TrimMode.Both);
        bool hasSpace = attr.TrimChars.Contains(' ');
        bool hasTab = attr.TrimChars.Contains('\t');
        await Assert.That(hasSpace).IsTrue();
        await Assert.That(hasTab).IsTrue();
    }

    [Test]
    [DisplayName("FieldTrimAttribute constructor with custom chars stores them in TrimChars")]
    public async Task FieldTrim_CustomChars()
    {
        // Arrange & Act: Create attribute with custom trim characters
        var attr = new FieldTrimAttribute(TrimMode.Left, '|', '-');

        // Assert: TrimChars contains pipe character, length is 2
        await Assert.That(attr.TrimChars.Length).IsEqualTo(2);
        bool hasPipe = attr.TrimChars.Contains('|');
        await Assert.That(hasPipe).IsTrue();
    }

    // ------- FieldQuotedAttribute -------

    [Test]
    [DisplayName("FieldQuotedAttribute defaults QuoteChar=\" and QuoteMode=OptionalForRead")]
    public async Task FieldQuoted_Default_DoubleQuoteOptionalForRead()
    {
        // Arrange & Act: Create attribute with default constructor
        var attr = new FieldQuotedAttribute();

        // Assert: Default QuoteChar is double quote, QuoteMode is OptionalForRead
        await Assert.That(attr.QuoteChar).IsEqualTo('"');
        await Assert.That(attr.QuoteMode).IsEqualTo(QuoteMode.OptionalForRead);
    }

    [Test]
    [DisplayName("FieldQuotedAttribute constructor with char stores custom quote character")]
    public async Task FieldQuoted_CustomChar()
    {
        // Arrange & Act: Create attribute with single quote character
        var attr = new FieldQuotedAttribute('\'');

        // Assert: QuoteChar is single quote, QuoteMode still OptionalForRead
        await Assert.That(attr.QuoteChar).IsEqualTo('\'');
        await Assert.That(attr.QuoteMode).IsEqualTo(QuoteMode.OptionalForRead);
    }

    [Test]
    [DisplayName("FieldQuotedAttribute constructor with QuoteMode stores mode correctly")]
    public async Task FieldQuoted_ModeOnly()
    {
        // Arrange & Act: Create attribute with AlwaysQuoted mode
        var attr = new FieldQuotedAttribute(QuoteMode.AlwaysQuoted);

        // Assert: QuoteMode is AlwaysQuoted, QuoteChar still double quote
        await Assert.That(attr.QuoteChar).IsEqualTo('"');
        await Assert.That(attr.QuoteMode).IsEqualTo(QuoteMode.AlwaysQuoted);
    }

    [Test]
    [DisplayName("FieldQuotedAttribute constructor with char and mode stores both values")]
    public async Task FieldQuoted_CharAndMode()
    {
        // Arrange & Act: Create attribute with single quote and OptionalForBoth mode
        var attr = new FieldQuotedAttribute('\'', QuoteMode.OptionalForBoth);

        // Assert: Both QuoteChar and QuoteMode set correctly
        await Assert.That(attr.QuoteChar).IsEqualTo('\'');
        await Assert.That(attr.QuoteMode).IsEqualTo(QuoteMode.OptionalForBoth);
    }

    // ------- FieldConverterAttribute -------

    [Test]
    [DisplayName("FieldConverterAttribute Kind overload stores ConverterKind and format args")]
    public async Task FieldConverter_KindOverload()
    {
        // Arrange & Act: Create attribute with ConverterKind.Date and format string
        var attr = new FieldConverterAttribute(ConverterKind.Date, "yyyy-MM-dd");

        // Assert: Kind is Date, ConverterArgs contains format string
        await Assert.That(attr.Kind).IsEqualTo(ConverterKind.Date);
        await Assert.That(attr.ConverterType).IsNull();
        await Assert.That(attr.ConverterArgs.Length).IsEqualTo(1);
    }

    [Test]
    [DisplayName("FieldConverterAttribute Type overload stores ConverterType with Kind=None")]
    public async Task FieldConverter_TypeOverload()
    {
        // Arrange & Act: Create attribute with custom converter type
        var attr = new FieldConverterAttribute(typeof(object));

        // Assert: Kind is None, ConverterType is typeof(object)
        await Assert.That(attr.Kind).IsEqualTo(ConverterKind.None);
        await Assert.That(attr.ConverterType).IsEqualTo(typeof(object));
    }

    // ------- FieldNullValueAttribute -------

    [Test]
    [DisplayName("FieldNullValueAttribute constructor stores null value string")]
    public async Task FieldNullValue_IsSet()
    {
        // Arrange & Act: Create attribute with null value "N/A"
        var attr = new FieldNullValueAttribute("N/A");

        // Assert: NullValue property equals "N/A"
        await Assert.That(attr.NullValue).IsEqualTo("N/A");
    }

    [Test]
    [DisplayName("FieldNullValueAttribute constructor accepts null argument")]
    public async Task FieldNullValue_NullArgument()
    {
        // Arrange & Act: Create attribute with null argument
        var attr = new FieldNullValueAttribute(null);

        // Assert: NullValue property is null
        await Assert.That(attr.NullValue).IsNull();
    }

    // ------- Marker attributes (no properties) -------

    [Test]
    [DisplayName("FieldOptionalAttribute can be instantiated")]
    public async Task FieldOptional_CanBeInstantiated()
    {
        // Arrange & Act: Create attribute
        var attr = new FieldOptionalAttribute();

        // Assert: Attribute instance created
        await Assert.That(attr).IsNotNull();
    }

    [Test]
    [DisplayName("FieldHiddenAttribute can be instantiated")]
    public async Task FieldHidden_CanBeInstantiated()
    {
        // Arrange & Act: Create attribute
        var attr = new FieldHiddenAttribute();

        // Assert: Attribute instance created
        await Assert.That(attr).IsNotNull();
    }

    [Test]
    [DisplayName("FieldNotEmptyAttribute can be instantiated")]
    public async Task FieldNotEmpty_CanBeInstantiated()
    {
        // Arrange & Act: Create attribute
        var attr = new FieldNotEmptyAttribute();

        // Assert: Attribute instance created
        await Assert.That(attr).IsNotNull();
    }

    [Test]
    [DisplayName("FieldValueDiscardedAttribute can be instantiated")]
    public async Task FieldValueDiscarded_CanBeInstantiated()
    {
        // Arrange & Act: Create attribute
        var attr = new FieldValueDiscardedAttribute();

        // Assert: Attribute instance created
        await Assert.That(attr).IsNotNull();
    }

    [Test]
    [DisplayName("FieldInNewLineAttribute can be instantiated")]
    public async Task FieldInNewLine_CanBeInstantiated()
    {
        // Arrange & Act: Create attribute
        var attr = new FieldInNewLineAttribute();

        // Assert: Attribute instance created
        await Assert.That(attr).IsNotNull();
    }

    // ------- FieldNotInFileAttribute (obsolete alias) -------

#pragma warning disable CS0618 // Type or member is obsolete — intentional: testing the obsolete attribute itself
    [Test]
    [DisplayName("FieldNotInFileAttribute is marked with [Obsolete] warning (not error)")]
    public async Task FieldNotInFile_IsObsolete()
    {
        // Arrange: Get ObsoleteAttribute from FieldNotInFileAttribute
        var obsolete = (ObsoleteAttribute?)Attribute.GetCustomAttribute(
            typeof(FieldNotInFileAttribute), typeof(ObsoleteAttribute));

        // Act: Check if attribute is obsolete

        // Assert: Obsolete attribute exists with warning (not error)
        await Assert.That(obsolete).IsNotNull();
        bool isError = obsolete!.IsError;
        await Assert.That(isError).IsFalse();
    }

    [Test]
    [DisplayName("FieldNotInFileAttribute targets Field only (not Property)")]
    public async Task FieldNotInFile_Targets_FieldOnly()
    {
        // Arrange: Get AttributeUsageAttribute for FieldNotInFileAttribute
        var usage = GetUsage<FieldNotInFileAttribute>();
        bool hasField = usage.ValidOn.HasFlag(AttributeTargets.Field);

        // Act: Check for Property target
        bool hasProperty = usage.ValidOn.HasFlag(AttributeTargets.Property);

        // Assert: Targets Field only, not Property
        await Assert.That(hasField).IsTrue();
        await Assert.That(hasProperty).IsFalse();
    }
#pragma warning restore CS0618

    // ------- AllowMultiple = false on all field attributes -------

    [Test]
    [DisplayName("All field-level attributes have AllowMultiple=false in AttributeUsage")]
    public async Task AllFieldAttributes_AllowMultiple_IsFalse()
    {
        // Arrange: List of all field attribute types to check
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

            // Act: Check AllowMultiple property
            bool allowMultiple = usage.AllowMultiple;

            // Assert: AllowMultiple is false for all field attributes
            await Assert.That(allowMultiple).IsFalse();
        }
    }

    // Helper
    private static AttributeUsageAttribute GetUsage<T>() where T : Attribute => GetUsage(typeof(T));
    private static AttributeUsageAttribute GetUsage(Type t) =>
        (AttributeUsageAttribute)Attribute.GetCustomAttribute(t, typeof(AttributeUsageAttribute))!;
}
