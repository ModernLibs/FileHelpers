// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture converters and records kept next to the tests that use them.
#pragma warning disable MA0048
// CA1051: Record fixtures use public fields (FileHelpers style).
#pragma warning disable CA1051

using System.Globalization;
using ModernLibs.FileHelpers.Converters;
using ModernLibs.FileHelpers.Tests.Errors;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>Counts calls and parses integers; empty input becomes -1 (visible when it is passed through).</summary>
public sealed class CountingIntConverter : ConverterBase
{
    public int Calls { get; private set; }

    public override object? StringToField(string text)
    {
        Calls++;
        return text.Trim().Length == 0 ? -1 : int.Parse(text, CultureInfo.InvariantCulture);
    }
}

/// <summary>Like <see cref="CountingIntConverter"/> but opts into 3.x <c>CustomNullHandling</c>.</summary>
public sealed class CustomNullIntConverter : ConverterBase
{
    public int Calls { get; private set; }

    // 'protected internal' only because this assembly is a friend (InternalsVisibleTo) of the library;
    // consumer converters override it as 'protected', exactly as with FileHelpers 3.x.
    protected internal override bool CustomNullHandling => true;

    public override object? StringToField(string text)
    {
        Calls++;
        return text.Trim().Length == 0 ? -1 : int.Parse(text, CultureInfo.InvariantCulture);
    }
}

/// <summary>Reports bad input through the 3.x <c>ThrowConvertException</c> helper.</summary>
public sealed class ThrowHelperConverter : ConverterBase
{
    public override object? StringToField(string text)
    {
        ThrowConvertException(text, "Only digits allowed.");
        return null;
    }
}

/// <summary>Engine fixture: custom converters with and without CustomNullHandling.</summary>
[DelimitedRecord(",")]
public sealed class CustomNullHandlingRecord
{
    [FieldConverter(typeof(CountingIntConverter))] public int Plain;
    [FieldConverter(typeof(CustomNullIntConverter))] public int Custom;
    [FieldConverter(typeof(CountingIntConverter))] public int? Optional;
}

/// <summary>
/// FileHelpers 3.x <c>FileHelpers/Converters/ConverterBase.cs</c> members (<c>CustomNullHandling</c>,
/// virtual <c>FieldToString</c>, <c>ThrowConvertException</c>) and the empty-value rule from
/// <c>FileHelpers/Fields/FieldBase.cs</c> (<c>AssignFromString</c>) applied by <see cref="ConverterBaseAdapter{T}"/>.
/// </summary>
[Category("Converters")]
[Category("Parity")]
public sealed class ConverterBaseParityTests
{
    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [DisplayName("FieldBase.cs: CustomNullHandling off — empty '$text' for a value type raises NullValueNotFoundException without calling the converter")]
    public async Task Default_EmptyValueType_RaisesNullValueNotFound(string text)
    {
        // Arrange
        var inner = new CountingIntConverter();
        var adapter = new ConverterBaseAdapter<int>(inner);

        // Act
        var ex = Capture.Exception(() => _ = adapter.TryParse(text, out _));

        // Assert
        await Assert.That(ex).IsTypeOf<NullValueNotFoundException>();
        await Assert.That(inner.Calls).IsEqualTo(0);
        await Assert.That(adapter.CustomNullHandling).IsFalse();
    }

    [Test]
    [DisplayName("FieldBase.cs: CustomNullHandling off — empty input for nullable / reference types is null without calling the converter")]
    public async Task Default_EmptyNullable_IsNull()
    {
        // Arrange
        var inner = new CountingIntConverter();
        var nullable = new ConverterBaseAdapter<int?>(inner);
        var reference = new ConverterBaseAdapter<object>(inner);

        // Act
        bool okNullable = nullable.TryParse(" ", out int? n);
        bool okReference = reference.TryParse(string.Empty, out object o);

        // Assert
        await Assert.That(okNullable).IsTrue();
        await Assert.That(n).IsNull();
        await Assert.That(okReference).IsTrue();
        await Assert.That(o).IsNull();
        await Assert.That(inner.Calls).IsEqualTo(0);
    }

    [Test]
    [DisplayName("ConverterBase.cs: CustomNullHandling on — the converter receives the empty value")]
    public async Task CustomNullHandling_PassesEmptyToConverter()
    {
        // Arrange
        var inner = new CustomNullIntConverter();
        var adapter = new ConverterBaseAdapter<int>(inner);

        // Act
        bool ok = adapter.TryParse("  ", out int value);

        // Assert
        await Assert.That(adapter.CustomNullHandling).IsTrue();
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(-1);
        await Assert.That(inner.Calls).IsEqualTo(1);
    }

    [Test]
    [DisplayName("Engine: CustomNullHandling on reads the converter value; nullable field reads null")]
    public async Task Engine_CustomNullHandling_ReadsConverterValue()
    {
        // Arrange
        var engine = new FileHelperEngine<CustomNullHandlingRecord>();

        // Act
        var rows = engine.ReadString("1,,");

        // Assert
        await Assert.That(rows[0].Plain).IsEqualTo(1);
        await Assert.That(rows[0].Custom).IsEqualTo(-1);
        await Assert.That(rows[0].Optional).IsNull();
    }

    [Test]
    [DisplayName("Engine: CustomNullHandling off — empty value-type field raises NullValueNotFoundException")]
    public async Task Engine_Default_EmptyValueType_Throws()
    {
        // Arrange
        var engine = new FileHelperEngine<CustomNullHandlingRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString(",2,3"));

        // Assert
        await Assert.That(ex).IsTypeOf<NullValueNotFoundException>();
    }

    [Test]
    [NotInParallel("CurrentCulture")]
    [DisplayName("ConverterBase.cs: default FieldToString writes ToString() (invariant) and empty for null")]
    public async Task FieldToString_Default_UsesInvariantToString()
    {
        // Arrange
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var converter = new CountingIntConverter();

            // Act
            string? number = converter.FieldToString(1.5m);
            string? empty = converter.FieldToString(null);
            string? text = converter.FieldToString("abc");
            string? adapted = ParityHelpers.Format(new ConverterBaseAdapter<int>(converter), 42);

            // Assert
            await Assert.That(number).IsEqualTo("1.5");
            await Assert.That(empty).IsEqualTo(string.Empty);
            await Assert.That(text).IsEqualTo("abc");
            await Assert.That(adapted).IsEqualTo("42");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    [DisplayName("ConverterBase.cs: ThrowConvertException raises ConvertException for the bound field type")]
    public async Task ThrowConvertException_RaisesConvertException()
    {
        // Arrange
        var adapter = new ConverterBaseAdapter<long?>(new ThrowHelperConverter());

        // Act
        var ex = Capture.Exception(() => _ = adapter.TryParse("x1", out _));

        // Assert
        await Assert.That(ex).IsTypeOf<ConvertException>();
        var convert = (ConvertException)ex!;
        await Assert.That(convert.FieldType).IsEqualTo(typeof(long));
        await Assert.That(convert.FieldStringValue).IsEqualTo("x1");
        await Assert.That(convert.Message).Contains("Only digits allowed.");
    }
}
