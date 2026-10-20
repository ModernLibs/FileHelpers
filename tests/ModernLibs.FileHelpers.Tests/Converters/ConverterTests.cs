// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0025: Stub bodies intentionally incomplete — some tests are [Skip] until later waves.
#pragma warning disable MA0025
// MA0048: Multiple test classes per file — intentional; groups related converter stubs.
#pragma warning disable MA0048

using ModernLibs.FileHelpers.Converters;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>
/// Tests for ISpanConverter&lt;T&gt; and all built-in converters.
/// </summary>

[Category("Converters")]
public sealed class IntegerConverterTests
{
    [Test]
    [DisplayName("Int32Converter.TryParse and TryFormat round-trip value 42 correctly")]
    public async Task Int32Converter_RoundTrip_ParseFormat()
    {
        // Arrange: Parse "42" and format back to string
        bool parseOk = Int32Converter.Instance.TryParse("42", out int v);
        // Format into array before any await (Span<T> cannot cross await boundary)
        char[] buf = new char[12];
        bool fmtOk = Int32Converter.Instance.TryFormat(42, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with correct values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(42);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("42");
    }

    [Test]
    [DisplayName("Int32Converter.TryParse returns false for invalid input \"abc\"")]
    public async Task Int32Converter_Parse_InvalidInput_ReturnsFalse()
    {
        // Arrange & Act: Attempt to parse invalid input
        bool ok = Int32Converter.Instance.TryParse("abc", out int v);

        // Assert: Parse failed with default output value
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsEqualTo(default(int));
    }

    [Test]
    [DisplayName("Int64Converter round-trips long.MinValue (-9223372036854775808) correctly")]
    public async Task Int64Converter_RoundTrip()
    {
        // Arrange: Parse long.MinValue and format back
        bool parseOk = Int64Converter.Instance.TryParse("-9223372036854775808", out long v);
        char[] buf = new char[20];
        bool fmtOk = Int64Converter.Instance.TryFormat(long.MinValue, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with correct values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(long.MinValue);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("-9223372036854775808");
    }

    [Test]
    [DisplayName("Int16Converter round-trips short.MaxValue (32767) correctly")]
    public async Task Int16Converter_RoundTrip()
    {
        // Arrange: Parse "32767" and format short.MaxValue back
        bool parseOk = Int16Converter.Instance.TryParse("32767", out short v);
        char[] buf = new char[6];
        bool fmtOk = Int16Converter.Instance.TryFormat(short.MaxValue, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with correct values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(short.MaxValue);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("32767");
    }

    [Test]
    [DisplayName("ByteConverter round-trips byte value 255 correctly")]
    public async Task ByteConverter_RoundTrip()
    {
        // Arrange: Parse "255" and format byte value back
        bool parseOk = ByteConverter.Instance.TryParse("255", out byte v);
        char[] buf = new char[4];
        bool fmtOk = ByteConverter.Instance.TryFormat(255, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with correct values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo((byte)255);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("255");
    }

    [Test]
    [DisplayName("SByteConverter round-trips sbyte.MinValue (-128) correctly")]
    public async Task SByteConverter_RoundTrip()
    {
        // Arrange: Parse "-128" and format sbyte.MinValue back
        bool parseOk = SByteConverter.Instance.TryParse("-128", out sbyte v);
        char[] buf = new char[4];
        bool fmtOk = SByteConverter.Instance.TryFormat(sbyte.MinValue, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with correct values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(sbyte.MinValue);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("-128");
    }

    [Test]
    [DisplayName("UInt16Converter round-trips ushort.MaxValue (65535) correctly")]
    public async Task UInt16Converter_RoundTrip()
    {
        // Arrange: Parse "65535" and format ushort.MaxValue back
        bool parseOk = UInt16Converter.Instance.TryParse("65535", out ushort v);
        char[] buf = new char[6];
        bool fmtOk = UInt16Converter.Instance.TryFormat(ushort.MaxValue, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with correct values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(ushort.MaxValue);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("65535");
    }

    [Test]
    [DisplayName("UInt32Converter round-trips uint.MaxValue (4294967295) correctly")]
    public async Task UInt32Converter_RoundTrip()
    {
        // Arrange: Parse "4294967295" and format uint.MaxValue back
        bool parseOk = UInt32Converter.Instance.TryParse("4294967295", out uint v);
        char[] buf = new char[12];
        bool fmtOk = UInt32Converter.Instance.TryFormat(uint.MaxValue, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with correct values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(uint.MaxValue);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("4294967295");
    }

    [Test]
    [DisplayName("UInt64Converter round-trips ulong.MaxValue (18446744073709551615) correctly")]
    public async Task UInt64Converter_RoundTrip()
    {
        // Arrange: Parse ulong.MaxValue and format back
        bool parseOk = UInt64Converter.Instance.TryParse("18446744073709551615", out ulong v);
        char[] buf = new char[20];
        bool fmtOk = UInt64Converter.Instance.TryFormat(ulong.MaxValue, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with correct values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(ulong.MaxValue);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("18446744073709551615");
    }
}

[Category("Converters")]
public sealed class FloatConverterTests
{
    [Test]
    [DisplayName("DecimalConverter round-trips 1.23 with invariant culture formatting")]
    public async Task DecimalConverter_RoundTrip_InvariantCulture()
    {
        // Arrange: Parse "1.23" and format 1.23m back
        bool parseOk = DecimalConverter.Instance.TryParse("1.23", out decimal v);
        char[] buf = new char[32];
        bool fmtOk = DecimalConverter.Instance.TryFormat(1.23m, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with correct values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(1.23m);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("1.23");
    }

    [Test]
    [DisplayName("DoubleConverter round-trips 3.14 with invariant culture formatting")]
    public async Task DoubleConverter_RoundTrip_InvariantCulture()
    {
        // Arrange: Parse "3.14" and format 3.14 back
        bool parseOk = DoubleConverter.Instance.TryParse("3.14", out double v);
        char[] buf = new char[24];
        bool fmtOk = DoubleConverter.Instance.TryFormat(3.14, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with correct values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(3.14);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("3.14");
    }

    [Test]
    [DisplayName("SingleConverter round-trips 1.5 with invariant culture formatting")]
    public async Task SingleConverter_RoundTrip_InvariantCulture()
    {
        // Arrange: Parse "1.5" and format 1.5f back
        bool parseOk = SingleConverter.Instance.TryParse("1.5", out float v);
        char[] buf = new char[16];
        bool fmtOk = SingleConverter.Instance.TryFormat(1.5f, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with correct values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(1.5f);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("1.5");
    }

    [Test]
    [DisplayName("DecimalConverter.TryParse correctly handles negative value -99.99")]
    public async Task DecimalConverter_Parse_Negative()
    {
        // Arrange & Act: Parse negative decimal value
        bool ok = DecimalConverter.Instance.TryParse("-99.99", out decimal v);

        // Assert: Parse succeeded with correct negative value
        await Assert.That(ok).IsTrue();
        await Assert.That(v).IsEqualTo(-99.99m);
    }
}

[Category("Converters")]
public sealed class DateTimeConverterTests
{
    [Test]
    [DisplayName("DateTimeConverter round-trips with the 3.x default format ddMMyyyy (12042026)")]
    public async Task DateTimeConverter_RoundTrip_DefaultFormat()
    {
        // Arrange: Parse default format datetime
        bool parseOk = DateTimeConverter.Instance.TryParse("12042026", out DateTime v);
        char[] buf = new char[25];
        bool fmtOk = DateTimeConverter.Instance.TryFormat(v, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with expected values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(new DateTime(2026, 4, 12));
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("12042026");
    }

    [Test]
    [DisplayName("DateTimeConverter with custom format \"yyyy-MM-dd\" round-trips correctly")]
    public async Task DateTimeConverter_CustomFormat()
    {
        // Arrange: Create converter with custom format and parse
        var conv = new DateTimeConverter("yyyy-MM-dd");
        bool parseOk = conv.TryParse("2026-04-12", out DateTime v);
        char[] buf = new char[12];
        bool fmtOk = conv.TryFormat(v, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with expected values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(new DateTime(2026, 4, 12));
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("2026-04-12");
    }

    [Test]
    [DisplayName("DateOnlyConverter round-trips date (04/12/2026) correctly")]
    public async Task DateOnlyConverter_RoundTrip()
    {
        // Arrange: Parse DateOnly value
        bool parseOk = DateOnlyConverter.Instance.TryParse("04/12/2026", out DateOnly v);
        char[] buf = new char[12];
        bool fmtOk = DateOnlyConverter.Instance.TryFormat(v, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with expected values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(new DateOnly(2026, 4, 12));
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("04/12/2026");
    }

    [Test]
    [DisplayName("TimeOnlyConverter round-trips time (14:30:00) correctly")]
    public async Task TimeOnlyConverter_RoundTrip()
    {
        // Arrange: Parse TimeOnly value
        bool parseOk = TimeOnlyConverter.Instance.TryParse("14:30:00", out TimeOnly v);
        char[] buf = new char[10];
        bool fmtOk = TimeOnlyConverter.Instance.TryFormat(v, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with expected values
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(new TimeOnly(14, 30, 0));
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("14:30:00");
    }

    [Test]
    [DisplayName("DateTimeConverter.TryParse returns false for invalid input \"not-a-date\"")]
    public async Task DateTimeConverter_InvalidInput_ReturnsFalse()
    {
        // Arrange & Act: Attempt to parse invalid datetime
        bool ok = DateTimeConverter.Instance.TryParse("not-a-date", out DateTime v);

        // Assert: Parse failed with default output value
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsEqualTo(default(DateTime));
    }

    [Test]
    [DisplayName("DateMultiFormatConverter tries each format and formats with first format")]
    public async Task DateMultiFormat_TriesEachFormat()
    {
        // Arrange: Create converter with multiple input formats
        var conv = new DateMultiFormatConverter(["yyyy-MM-dd", "MM/dd/yyyy"]);
        bool ok1 = conv.TryParse("2026-04-12", out DateTime v1);
        bool ok2 = conv.TryParse("04/12/2026", out DateTime v2);

        // Format uses first format
        char[] buf = new char[12];
        bool fmtOk = conv.TryFormat(new DateTime(2026, 4, 12), buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Both parse formats succeeded, formats with first format
        await Assert.That(ok1).IsTrue();
        await Assert.That(v1).IsEqualTo(new DateTime(2026, 4, 12));
        await Assert.That(ok2).IsTrue();
        await Assert.That(v2).IsEqualTo(new DateTime(2026, 4, 12));
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("2026-04-12");
    }
}

[Category("Converters")]
public sealed class BoolConverterTests
{
    [Test]
    [DisplayName("BoolConverter.TryParse recognizes TRUE, True, and true case-insensitively")]
    public async Task BoolConverter_Parse_True_CaseInsensitive()
    {
        // Arrange: Test multiple case variations of "true"
        bool r1 = BoolConverter.Instance.TryParse("TRUE", out bool v1);
        bool r2 = BoolConverter.Instance.TryParse("True", out bool v2);

        // Act: Test lowercase variation
        bool r3 = BoolConverter.Instance.TryParse("true", out bool v3);

        // Assert: All case variations parse to true
        await Assert.That(r1).IsTrue();
        await Assert.That(v1).IsTrue();
        await Assert.That(r2).IsTrue();
        await Assert.That(v2).IsTrue();
        await Assert.That(r3).IsTrue();
        await Assert.That(v3).IsTrue();
    }

    [Test]
    [DisplayName("BoolConverter.TryParse recognizes \"y\" and \"1\" as true (3.x BooleanConverter)")]
    public async Task BoolConverter_Parse_Y_And_One()
    {
        // Arrange: Test "y" parses to true
        bool r1 = BoolConverter.Instance.TryParse("y", out bool v1);

        // Act: Test "1" parses to true
        bool r2 = BoolConverter.Instance.TryParse("1", out bool v2);

        // Assert: Both alternative true values recognized
        await Assert.That(r1).IsTrue();
        await Assert.That(v1).IsTrue();
        await Assert.That(r2).IsTrue();
        await Assert.That(v2).IsTrue();
    }

    [Test]
    [DisplayName("BoolConverter.TryParse recognizes FALSE, \"0\", and \"n\" as false (3.x BooleanConverter)")]
    public async Task BoolConverter_Parse_False_CaseInsensitive()
    {
        // Arrange: Test "FALSE" parses to false
        bool r1 = BoolConverter.Instance.TryParse("FALSE", out bool v1);
        bool r2 = BoolConverter.Instance.TryParse("0", out bool v2);

        // Act: Test "n" parses to false
        bool r3 = BoolConverter.Instance.TryParse("n", out bool v3);

        // Assert: All false variations recognized with correct output values
        await Assert.That(r1).IsTrue();
        await Assert.That(v1).IsFalse();
        await Assert.That(r2).IsTrue();
        await Assert.That(v2).IsFalse();
        await Assert.That(r3).IsTrue();
        await Assert.That(v3).IsFalse();
    }

    [Test]
    [DisplayName("BoolConverter.TryParse returns false for invalid input \"maybe\"")]
    public async Task BoolConverter_Parse_Invalid_ReturnsFalse()
    {
        // Arrange & Act: Attempt to parse invalid boolean value
        bool ok = BoolConverter.Instance.TryParse("maybe", out bool v);

        // Assert: Parse failed with false output value
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsFalse();
    }

    [Test]
    [DisplayName("BoolConverter.TryFormat writes capitalized \"True\" and \"False\"")]
    public async Task BoolConverter_Format_WritesCapitalizedTrueFalse()
    {
        // Arrange: Format true and false values
        char[] bufTrue = new char[5];
        char[] bufFalse = new char[5];
        bool r1 = BoolConverter.Instance.TryFormat(true, bufTrue, out int cw1);
        bool r2 = BoolConverter.Instance.TryFormat(false, bufFalse, out int cw2);
        string trueStr = new string(bufTrue, 0, cw1);

        // Act: Create false string
        string falseStr = new string(bufFalse, 0, cw2);

        // Assert: Format writes capitalized "True" and "False"
        await Assert.That(r1).IsTrue();
        await Assert.That(trueStr).IsEqualTo("True");
        await Assert.That(r2).IsTrue();
        await Assert.That(falseStr).IsEqualTo("False");
    }
}

[Category("Converters")]
public sealed class MiscConverterTests
{
    [Test]
    [DisplayName("CharConverter round-trips single character 'A' correctly")]
    public async Task CharConverter_SingleChar_RoundTrip()
    {
        // Arrange: Parse "A" and format 'A' back
        bool parseOk = CharConverter.Instance.TryParse("A", out char v);
        char[] buf = new char[1];
        bool fmtOk = CharConverter.Instance.TryFormat('A', buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with correct character
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo('A');
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("A");
    }

    [Test]
    [DisplayName("CharConverter.TryParse takes the first character of multi-character input \"AB\" (3.x CharConverter)")]
    public async Task CharConverter_MultiChar_TakesFirstChar()
    {
        // Arrange & Act: Parse multi-character input
        bool ok = CharConverter.Instance.TryParse("AB", out char v);

        // Assert: 3.x takes the first character
        await Assert.That(ok).IsTrue();
        await Assert.That(v).IsEqualTo('A');
    }

    [Test]
    [DisplayName("StringConverter round-trips \"hello\" without modification")]
    public async Task StringConverter_RoundTrip()
    {
        // Arrange: Parse "hello" and format back
        bool parseOk = StringConverter.Instance.TryParse("hello", out string v);
        char[] buf = new char[10];
        bool fmtOk = StringConverter.Instance.TryFormat("hello", buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Parse and format both succeeded with original value
        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo("hello");
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("hello");
    }

    [Test]
    [DisplayName("GuidConverter round-trips new Guid with Format D (with hyphens)")]
    public async Task GuidConverter_RoundTrip_FormatD()
    {
        // Arrange: Create new Guid, format it, then parse back
        Guid original = Guid.NewGuid();
        char[] buf = new char[36]; // "D" format = 36 chars (8-4-4-4-12 + 4 hyphens)
        bool fmtOk = GuidConverter.Instance.TryFormat(original, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        // Act: Parse the formatted Guid string
        bool parseOk = GuidConverter.Instance.TryParse(formatted, out Guid parsed);

        // Assert: Format and parse both succeeded with matching Guids
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(cw).IsEqualTo(36);
        await Assert.That(parseOk).IsTrue();
        await Assert.That(parsed).IsEqualTo(original);
    }

    [Test]
    [DisplayName("GuidConverter.TryParse returns false for invalid input \"not-a-guid\"")]
    public async Task GuidConverter_Invalid_ReturnsFalse()
    {
        // Arrange & Act: Attempt to parse invalid Guid
        bool ok = GuidConverter.Instance.TryParse("not-a-guid", out Guid v);

        // Assert: Parse failed with empty output Guid
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsEqualTo(default(Guid));
    }

    [Test]
    [DisplayName("PercentDoubleConverter parses \"3.14\" as 3.14 and rejects \"3.14%\" like FileHelpers 3.5.2")]
    public async Task PercentDoubleConverter_Parse_RejectsPercent()
    {
        // Arrange: Parse value without percent sign
        bool r1 = PercentDoubleConverter.Instance.TryParse("3.14", out double v1);

        // Act: Parse values with percent sign (3.5.2 raises ConvertException for both)
        bool r2 = PercentDoubleConverter.Instance.TryParse("3.14%", out _);
        bool r3 = PercentDoubleConverter.Instance.TryParse(" 7 % ", out _);

        // Assert: Plain number keeps its value; any '%' fails to convert
        await Assert.That(r1).IsTrue();
        await Assert.That(v1).IsEqualTo(3.14);
        await Assert.That(r2).IsFalse();
        await Assert.That(r3).IsFalse();
    }

    [Test]
    [DisplayName("PercentDoubleConverter.TryFormat writes the plain number like FileHelpers 3.5.2")]
    public async Task PercentDoubleConverter_Format_WritesPlainNumber()
    {
        // Arrange: Format value with percent converter
        char[] buf = new char[30];
        bool ok = PercentDoubleConverter.Instance.TryFormat(0.5, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: No percent sign and no scaling
        await Assert.That(ok).IsTrue();
        await Assert.That(formatted).IsEqualTo("0.5");
    }

    [Test]
    [DisplayName("EnumConverter.TryParse recognizes \"monday\" case-insensitively as DayOfWeek.Monday")]
    public async Task EnumConverter_RoundTrip_CaseInsensitiveParse()
    {
        // Arrange & Act: Parse enum value case-insensitively
        bool ok = EnumConverter<DayOfWeek>.Instance.TryParse("monday", out DayOfWeek v);

        // Assert: Parse succeeded with correct enum value
        await Assert.That(ok).IsTrue();
        await Assert.That(v).IsEqualTo(DayOfWeek.Monday);
    }

    [Test]
    [DisplayName("EnumConverter.TryFormat writes enum name \"Monday\" for DayOfWeek.Monday")]
    public async Task EnumConverter_Format_WritesName()
    {
        // Arrange: Format enum value
        char[] buf = new char[16];
        bool ok = EnumConverter<DayOfWeek>.Instance.TryFormat(DayOfWeek.Monday, buf, out int cw);

        // Act: Create formatted string
        string formatted = new string(buf, 0, cw);

        // Assert: Format writes enum name
        await Assert.That(ok).IsTrue();
        await Assert.That(formatted).IsEqualTo("Monday");
    }

    [Test]
    [DisplayName("ISpanConverter.Instance returns singleton Int32Converter for same type")]
    public async Task ISpanConverter_Instance_IsSingleton()
    {
        // Arrange & Assert: Type verification
        await Assert.That(Int32Converter.Instance).IsTypeOf<Int32Converter>();

        // Act & Assert: Reference equality confirms singleton
        await Assert.That(ReferenceEquals(Int32Converter.Instance, Int32Converter.Instance)).IsTrue();
    }
}
