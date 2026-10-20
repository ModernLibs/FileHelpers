// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using System.Globalization;
using ModernLibs.FileHelpers.Converters;
using ModernLibs.FileHelpers.Tests.Errors;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>
/// P11: numeric converters match FileHelpers 3.x <c>FileHelpers/Converters/Int32Converter.cs</c> (and the
/// other integer/float converters), <c>ConvertHelpers.RemoveBlanks</c> and <c>CultureConverter.cs</c>.
/// </summary>
[Category("Converters")]
[Category("Parity")]
public sealed class NumberConverterParityTests
{
    [Test]
    [Arguments("42", 42)]
    [Arguments(" 42 ", 42)]
    [Arguments("+ 21 ", 21)]
    [Arguments(" -   21", -21)]
    [Arguments("\t- 7", -7)]
    [Arguments("1,234", 1234)]
    [Arguments("5.0", 5)]
    [Arguments("1e3", 1000)]
    [Arguments("5-", -5)]
    [DisplayName("Int32Converter.cs + RemoveBlanks: '$text' parses as $expected (Number | AllowExponent)")]
    public async Task Int32_AcceptsLegacyStyles(string text, int expected)
    {
        // Act
        var (ok, value) = ParityHelpers.Parse(Int32Converter.Instance, text);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(expected);
    }

    [Test]
    [Arguments("5.5")]
    [Arguments("abc")]
    [Arguments("(5)")]
    [Arguments("$5")]
    [Arguments("+")]
    [Arguments("- ")]
    [Arguments("")]
    [Arguments("0x10")]
    [Arguments("1 000")]
    [DisplayName("Int32Converter.cs: '$text' is rejected")]
    public async Task Int32_RejectsInvalid(string text)
    {
        // Act
        var (ok, _) = ParityHelpers.Parse(Int32Converter.Instance, text);

        // Assert
        await Assert.That(ok).IsFalse();
    }

    [Test]
    [DisplayName("Int16/Int64/UInt16/UInt32/UInt64Converter.cs: exponent, thousands and detached sign are accepted")]
    public async Task OtherIntegers_AcceptLegacyStyles()
    {
        // Act + Assert: each 3.x integer converter uses Number | AllowExponent after RemoveBlanks
        await Assert.That(ParityHelpers.Parse(Int16Converter.Instance, "- 1e2")).IsEqualTo((true, (short)-100));
        await Assert.That(ParityHelpers.Parse(Int64Converter.Instance, "1,000,000")).IsEqualTo((true, 1_000_000L));
        await Assert.That(ParityHelpers.Parse(UInt16Converter.Instance, "+ 6e1 ")).IsEqualTo((true, (ushort)60));
        await Assert.That(ParityHelpers.Parse(UInt32Converter.Instance, "4.0e2")).IsEqualTo((true, 400u));
        await Assert.That(ParityHelpers.Parse(UInt64Converter.Instance, " 7 ")).IsEqualTo((true, 7ul));
    }

    [Test]
    [DisplayName("ByteConverter.cs / SByteConverter.cs: NumberStyles.Number only — exponent rejected, blanks removed")]
    public async Task ByteAndSByte_NumberStylesOnly()
    {
        // Act + Assert
        await Assert.That(ParityHelpers.Parse(ByteConverter.Instance, " + 7 ")).IsEqualTo((true, (byte)7));
        await Assert.That(ParityHelpers.Parse(SByteConverter.Instance, "-  12")).IsEqualTo((true, (sbyte)-12));
        await Assert.That(ParityHelpers.Parse(ByteConverter.Instance, "1e2").Ok).IsFalse();
        await Assert.That(ParityHelpers.Parse(SByteConverter.Instance, "1e1").Ok).IsFalse();
    }

    [Test]
    [Arguments("1,234.56", "1234.56")]
    [Arguments(" - 1.5", "-1.5")]
    [Arguments("1.5e2", "150")]
    [Arguments("1.5-", "-1.5")]
    [DisplayName("DecimalConverter.cs: '$text' parses as $expected")]
    public async Task Decimal_AcceptsLegacyStyles(string text, string expected)
    {
        // Act
        var (ok, value) = ParityHelpers.Parse(DecimalConverter.Instance, text);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(decimal.Parse(expected, CultureInfo.InvariantCulture));
    }

    [Test]
    [Arguments("(5)")]
    [Arguments("$5")]
    [Arguments("¤5")]
    [Arguments("5%")]
    [Arguments("1.2.3")]
    [DisplayName("Decimal/Double/SingleConverter.cs: '$text' is rejected (3.x had no currency or parentheses)")]
    public async Task Floats_RejectCurrencyAndParentheses(string text)
    {
        // Act + Assert
        await Assert.That(ParityHelpers.Parse(DecimalConverter.Instance, text).Ok).IsFalse();
        await Assert.That(ParityHelpers.Parse(DoubleConverter.Instance, text).Ok).IsFalse();
        await Assert.That(ParityHelpers.Parse(SingleConverter.Instance, text).Ok).IsFalse();
    }

    [Test]
    [DisplayName("DoubleConverter.cs / SingleConverter.cs: exponent and detached sign are accepted")]
    public async Task DoubleAndSingle_AcceptLegacyStyles()
    {
        // Act + Assert
        await Assert.That(ParityHelpers.Parse(DoubleConverter.Instance, "- 1.5E-3")).IsEqualTo((true, -0.0015));
        await Assert.That(ParityHelpers.Parse(SingleConverter.Instance, "2,500.25")).IsEqualTo((true, 2500.25f));
    }

    [Test]
    [DisplayName("CultureConverter.cs: \",\" separator — DecimalConverter(\",\") parses 1.234,56 and writes 1234,56")]
    public async Task Decimal_CommaSeparator_RoundTrips()
    {
        // Arrange
        var converter = new DecimalConverter(",");

        // Act
        var parsed = ParityHelpers.Parse(converter, "1.234,56");
        string? written = ParityHelpers.Format(converter, 1234.56m);
        var reparsed = ParityHelpers.Parse(converter, written!);

        // Assert
        await Assert.That(parsed).IsEqualTo((true, 1234.56m));
        await Assert.That(written).IsEqualTo("1234,56");
        await Assert.That(reparsed).IsEqualTo((true, 1234.56m));
    }

    [Test]
    [DisplayName("CultureConverter.cs: \",\" separator applies to Double, Single, Int32 and PercentDouble")]
    public async Task CommaSeparator_AllNumericConverters()
    {
        // Act + Assert
        await Assert.That(ParityHelpers.Parse(new DoubleConverter(","), "3,14")).IsEqualTo((true, 3.14));
        await Assert.That(ParityHelpers.Format(new DoubleConverter(","), -3.14)).IsEqualTo("-3,14");
        await Assert.That(ParityHelpers.Parse(new SingleConverter(","), "0,5")).IsEqualTo((true, 0.5f));
        await Assert.That(ParityHelpers.Parse(new Int32Converter(","), "1.234")).IsEqualTo((true, 1234));
        await Assert.That(ParityHelpers.Parse(new Int32Converter(","), "1,5").Ok).IsFalse();
        await Assert.That(ParityHelpers.Parse(new PercentDoubleConverter(","), "3,5")).IsEqualTo((true, 3.5));
        await Assert.That(ParityHelpers.Format(new PercentDoubleConverter(","), 3.5)).IsEqualTo("3,5");
    }

    [Test]
    [DisplayName("CultureConverter.cs: \".\" separator is invariant (',' groups)")]
    public async Task DotSeparator_IsInvariant()
    {
        // Act + Assert
        await Assert.That(ParityHelpers.Parse(new DecimalConverter("."), "1,234.5")).IsEqualTo((true, 1234.5m));
        await Assert.That(ParityHelpers.Parse(new UInt64Converter("."), "1,234")).IsEqualTo((true, 1234ul));
    }

    [Test]
    [DisplayName("CultureConverter.cs: a culture name selects that culture's separators")]
    public async Task CultureName_SelectsCulture()
    {
        // Act + Assert
        await Assert.That(ParityHelpers.Parse(new DecimalConverter("de-DE"), "1.234,5")).IsEqualTo((true, 1234.5m));
        await Assert.That(ParityHelpers.Format(new DecimalConverter("de-DE"), 1234.5m)).IsEqualTo("1234,5");
        await Assert.That(ParityHelpers.Parse(new Int64Converter(CultureInfo.GetCultureInfo("de-DE")), "1.234")).IsEqualTo((true, 1234L));
    }

    [Test]
    [DisplayName("An invalid separator / culture name raises BadUsageException")]
    public async Task InvalidSeparator_Throws()
    {
        // Act
        var ex = Capture.Exception(() => _ = new DecimalConverter(";"));

        // Assert
        await Assert.That(ex).IsTypeOf<BadUsageException>();
    }

    [Test]
    [NotInParallel("CurrentCulture")]
    [DisplayName("Default numeric converters ignore the current culture (no culture leakage)")]
    public async Task Defaults_IgnoreCurrentCulture()
    {
        // Arrange
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            // Act
            var dec = ParityHelpers.Parse(DecimalConverter.Instance, "1.5");
            var integer = ParityHelpers.Parse(Int32Converter.Instance, "1,000");
            string? written = ParityHelpers.Format(DoubleConverter.Instance, 1.5);

            // Assert
            await Assert.That(dec).IsEqualTo((true, 1.5m));
            await Assert.That(integer).IsEqualTo((true, 1000));
            await Assert.That(written).IsEqualTo("1.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    [DisplayName("Formatting round-trips through parsing for extreme numeric values")]
    public async Task Format_RoundTrips()
    {
        // Act + Assert
        await Assert.That(RoundTrip(DoubleConverter.Instance, 1e20)).IsEqualTo(1e20);
        await Assert.That(RoundTrip(new DoubleConverter(","), -1.25e-7)).IsEqualTo(-1.25e-7);
        await Assert.That(RoundTrip(SingleConverter.Instance, float.MaxValue)).IsEqualTo(float.MaxValue);
        await Assert.That(RoundTrip(DecimalConverter.Instance, decimal.MinValue)).IsEqualTo(decimal.MinValue);
        await Assert.That(RoundTrip(new Int32Converter(","), int.MinValue)).IsEqualTo(int.MinValue);
        await Assert.That(RoundTrip(SByteConverter.Instance, sbyte.MinValue)).IsEqualTo(sbyte.MinValue);
    }

    [Test]
    [DisplayName("A detached sign longer than the stack buffer still parses")]
    public async Task DetachedSign_LongInput_Parses()
    {
        // Arrange: 200 leading zeros push the joined text past the 128-char stack buffer
        string text = "- " + new string('0', 200) + "42";

        // Act
        var result = ParityHelpers.Parse(Int32Converter.Instance, text);

        // Assert
        await Assert.That(result).IsEqualTo((true, -42));
    }

    private static T RoundTrip<T>(ISpanConverter<T> converter, T value)
    {
        string text = ParityHelpers.Format(converter, value)!;
        return ParityHelpers.Parse(converter, text).Value;
    }
}
