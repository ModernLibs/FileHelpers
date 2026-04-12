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
/// Tests for CONV-01 through CONV-08 (ISpanConverter&lt;T&gt; + all built-in converters).
/// </summary>

[Category("Phase06")]
[Category("Converters")]
public sealed class IntegerConverterTests
{
    [Test]
    public async Task Int32Converter_RoundTrip_ParseFormat()
    {
        bool parseOk = Int32Converter.Instance.TryParse("42", out int v);
        // Format into array before any await (Span<T> cannot cross await boundary)
        char[] buf = new char[12];
        bool fmtOk = Int32Converter.Instance.TryFormat(42, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(42);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("42");
    }

    [Test]
    public async Task Int32Converter_Parse_InvalidInput_ReturnsFalse()
    {
        bool ok = Int32Converter.Instance.TryParse("abc", out int v);
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsEqualTo(default(int));
    }

    [Test]
    public async Task Int64Converter_RoundTrip()
    {
        bool parseOk = Int64Converter.Instance.TryParse("-9223372036854775808", out long v);
        char[] buf = new char[20];
        bool fmtOk = Int64Converter.Instance.TryFormat(long.MinValue, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(long.MinValue);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("-9223372036854775808");
    }

    [Test]
    public async Task Int16Converter_RoundTrip()
    {
        bool parseOk = Int16Converter.Instance.TryParse("32767", out short v);
        char[] buf = new char[6];
        bool fmtOk = Int16Converter.Instance.TryFormat(short.MaxValue, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(short.MaxValue);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("32767");
    }

    [Test]
    public async Task ByteConverter_RoundTrip()
    {
        bool parseOk = ByteConverter.Instance.TryParse("255", out byte v);
        char[] buf = new char[4];
        bool fmtOk = ByteConverter.Instance.TryFormat(255, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo((byte)255);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("255");
    }

    [Test]
    public async Task SByteConverter_RoundTrip()
    {
        bool parseOk = SByteConverter.Instance.TryParse("-128", out sbyte v);
        char[] buf = new char[4];
        bool fmtOk = SByteConverter.Instance.TryFormat(sbyte.MinValue, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(sbyte.MinValue);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("-128");
    }

    [Test]
    public async Task UInt16Converter_RoundTrip()
    {
        bool parseOk = UInt16Converter.Instance.TryParse("65535", out ushort v);
        char[] buf = new char[6];
        bool fmtOk = UInt16Converter.Instance.TryFormat(ushort.MaxValue, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(ushort.MaxValue);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("65535");
    }

    [Test]
    public async Task UInt32Converter_RoundTrip()
    {
        bool parseOk = UInt32Converter.Instance.TryParse("4294967295", out uint v);
        char[] buf = new char[12];
        bool fmtOk = UInt32Converter.Instance.TryFormat(uint.MaxValue, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(uint.MaxValue);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("4294967295");
    }

    [Test]
    public async Task UInt64Converter_RoundTrip()
    {
        bool parseOk = UInt64Converter.Instance.TryParse("18446744073709551615", out ulong v);
        char[] buf = new char[20];
        bool fmtOk = UInt64Converter.Instance.TryFormat(ulong.MaxValue, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(ulong.MaxValue);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("18446744073709551615");
    }
}

[Category("Phase06")]
[Category("Converters")]
public sealed class FloatConverterTests
{
    [Test]
    public async Task DecimalConverter_RoundTrip_InvariantCulture()
    {
        bool parseOk = DecimalConverter.Instance.TryParse("1.23", out decimal v);
        char[] buf = new char[32];
        bool fmtOk = DecimalConverter.Instance.TryFormat(1.23m, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(1.23m);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("1.23");
    }

    [Test]
    public async Task DoubleConverter_RoundTrip_InvariantCulture()
    {
        bool parseOk = DoubleConverter.Instance.TryParse("3.14", out double v);
        char[] buf = new char[24];
        bool fmtOk = DoubleConverter.Instance.TryFormat(3.14, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(3.14);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("3.14");
    }

    [Test]
    public async Task SingleConverter_RoundTrip_InvariantCulture()
    {
        bool parseOk = SingleConverter.Instance.TryParse("1.5", out float v);
        char[] buf = new char[16];
        bool fmtOk = SingleConverter.Instance.TryFormat(1.5f, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(1.5f);
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("1.5");
    }

    [Test]
    public async Task DecimalConverter_Parse_Negative()
    {
        bool ok = DecimalConverter.Instance.TryParse("-99.99", out decimal v);
        await Assert.That(ok).IsTrue();
        await Assert.That(v).IsEqualTo(-99.99m);
    }
}

[Category("Phase06")]
[Category("Converters")]
public sealed class DateTimeConverterTests
{
    [Test]
    public async Task DateTimeConverter_RoundTrip_DefaultFormat()
    {
        bool parseOk = DateTimeConverter.Instance.TryParse("04/12/2026 14:30:00", out DateTime v);
        char[] buf = new char[25];
        bool fmtOk = DateTimeConverter.Instance.TryFormat(v, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(new DateTime(2026, 4, 12, 14, 30, 0));
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("04/12/2026 14:30:00");
    }

    [Test]
    public async Task DateTimeConverter_CustomFormat()
    {
        var conv = new DateTimeConverter("yyyy-MM-dd");
        bool parseOk = conv.TryParse("2026-04-12", out DateTime v);
        char[] buf = new char[12];
        bool fmtOk = conv.TryFormat(v, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(new DateTime(2026, 4, 12));
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("2026-04-12");
    }

    [Test]
    public async Task DateOnlyConverter_RoundTrip()
    {
        bool parseOk = DateOnlyConverter.Instance.TryParse("04/12/2026", out DateOnly v);
        char[] buf = new char[12];
        bool fmtOk = DateOnlyConverter.Instance.TryFormat(v, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(new DateOnly(2026, 4, 12));
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("04/12/2026");
    }

    [Test]
    public async Task TimeOnlyConverter_RoundTrip()
    {
        bool parseOk = TimeOnlyConverter.Instance.TryParse("14:30:00", out TimeOnly v);
        char[] buf = new char[10];
        bool fmtOk = TimeOnlyConverter.Instance.TryFormat(v, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo(new TimeOnly(14, 30, 0));
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("14:30:00");
    }

    [Test]
    public async Task DateTimeConverter_InvalidInput_ReturnsFalse()
    {
        bool ok = DateTimeConverter.Instance.TryParse("not-a-date", out DateTime v);
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsEqualTo(default(DateTime));
    }

    [Test]
    public async Task DateMultiFormat_TriesEachFormat()
    {
        var conv = new DateMultiFormatConverter(["yyyy-MM-dd", "MM/dd/yyyy"]);
        bool ok1 = conv.TryParse("2026-04-12", out DateTime v1);
        bool ok2 = conv.TryParse("04/12/2026", out DateTime v2);
        // Format uses first format
        char[] buf = new char[12];
        bool fmtOk = conv.TryFormat(new DateTime(2026, 4, 12), buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(ok1).IsTrue();
        await Assert.That(v1).IsEqualTo(new DateTime(2026, 4, 12));
        await Assert.That(ok2).IsTrue();
        await Assert.That(v2).IsEqualTo(new DateTime(2026, 4, 12));
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("2026-04-12");
    }
}

[Category("Phase06")]
[Category("Converters")]
public sealed class BoolConverterTests
{
    [Test]
    public async Task BoolConverter_Parse_True_CaseInsensitive()
    {
        bool r1 = BoolConverter.Instance.TryParse("TRUE", out bool v1);
        bool r2 = BoolConverter.Instance.TryParse("True", out bool v2);
        bool r3 = BoolConverter.Instance.TryParse("true", out bool v3);

        await Assert.That(r1).IsTrue();
        await Assert.That(v1).IsTrue();
        await Assert.That(r2).IsTrue();
        await Assert.That(v2).IsTrue();
        await Assert.That(r3).IsTrue();
        await Assert.That(v3).IsTrue();
    }

    [Test]
    public async Task BoolConverter_Parse_Yes_And_One()
    {
        bool r1 = BoolConverter.Instance.TryParse("yes", out bool v1);
        bool r2 = BoolConverter.Instance.TryParse("1", out bool v2);

        await Assert.That(r1).IsTrue();
        await Assert.That(v1).IsTrue();
        await Assert.That(r2).IsTrue();
        await Assert.That(v2).IsTrue();
    }

    [Test]
    public async Task BoolConverter_Parse_False_CaseInsensitive()
    {
        bool r1 = BoolConverter.Instance.TryParse("FALSE", out bool v1);
        bool r2 = BoolConverter.Instance.TryParse("0", out bool v2);
        bool r3 = BoolConverter.Instance.TryParse("no", out bool v3);

        await Assert.That(r1).IsTrue();
        await Assert.That(v1).IsFalse();
        await Assert.That(r2).IsTrue();
        await Assert.That(v2).IsFalse();
        await Assert.That(r3).IsTrue();
        await Assert.That(v3).IsFalse();
    }

    [Test]
    public async Task BoolConverter_Parse_Invalid_ReturnsFalse()
    {
        bool ok = BoolConverter.Instance.TryParse("maybe", out bool v);
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsFalse();
    }

    [Test]
    public async Task BoolConverter_Format_WritesCapitalizedTrueFalse()
    {
        char[] bufTrue = new char[5];
        char[] bufFalse = new char[5];
        bool r1 = BoolConverter.Instance.TryFormat(true, bufTrue, out int cw1);
        bool r2 = BoolConverter.Instance.TryFormat(false, bufFalse, out int cw2);
        string trueStr = new string(bufTrue, 0, cw1);
        string falseStr = new string(bufFalse, 0, cw2);

        await Assert.That(r1).IsTrue();
        await Assert.That(trueStr).IsEqualTo("True");
        await Assert.That(r2).IsTrue();
        await Assert.That(falseStr).IsEqualTo("False");
    }
}

[Category("Phase06")]
[Category("Converters")]
public sealed class MiscConverterTests
{
    [Test]
    public async Task CharConverter_SingleChar_RoundTrip()
    {
        bool parseOk = CharConverter.Instance.TryParse("A", out char v);
        char[] buf = new char[1];
        bool fmtOk = CharConverter.Instance.TryFormat('A', buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo('A');
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("A");
    }

    [Test]
    public async Task CharConverter_MultiChar_ReturnsFalse()
    {
        bool ok = CharConverter.Instance.TryParse("AB", out char v);
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsEqualTo(default(char));
    }

    [Test]
    public async Task StringConverter_RoundTrip()
    {
        bool parseOk = StringConverter.Instance.TryParse("hello", out string v);
        char[] buf = new char[10];
        bool fmtOk = StringConverter.Instance.TryFormat("hello", buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(parseOk).IsTrue();
        await Assert.That(v).IsEqualTo("hello");
        await Assert.That(fmtOk).IsTrue();
        await Assert.That(formatted).IsEqualTo("hello");
    }

    [Test]
    public async Task GuidConverter_RoundTrip_FormatD()
    {
        Guid original = Guid.NewGuid();
        char[] buf = new char[36]; // "D" format = 36 chars (8-4-4-4-12 + 4 hyphens)
        bool fmtOk = GuidConverter.Instance.TryFormat(original, buf, out int cw);
        string formatted = new string(buf, 0, cw);
        bool parseOk = GuidConverter.Instance.TryParse(formatted, out Guid parsed);

        await Assert.That(fmtOk).IsTrue();
        await Assert.That(cw).IsEqualTo(36);
        await Assert.That(parseOk).IsTrue();
        await Assert.That(parsed).IsEqualTo(original);
    }

    [Test]
    public async Task GuidConverter_Invalid_ReturnsFalse()
    {
        bool ok = GuidConverter.Instance.TryParse("not-a-guid", out Guid v);
        await Assert.That(ok).IsFalse();
        await Assert.That(v).IsEqualTo(default(Guid));
    }

    [Test]
    public async Task PercentDoubleConverter_Parse_WithPercent()
    {
        bool r1 = PercentDoubleConverter.Instance.TryParse("3.14%", out double v1);
        bool r2 = PercentDoubleConverter.Instance.TryParse("3.14", out double v2);

        await Assert.That(r1).IsTrue();
        await Assert.That(v1).IsEqualTo(3.14);
        await Assert.That(r2).IsTrue();
        await Assert.That(v2).IsEqualTo(3.14);
    }

    [Test]
    public async Task PercentDoubleConverter_Format_AppendsPercent()
    {
        char[] buf = new char[30];
        bool ok = PercentDoubleConverter.Instance.TryFormat(3.14, buf, out int cw);
        string formatted = new string(buf, 0, cw);

        await Assert.That(ok).IsTrue();
        await Assert.That(formatted).IsEqualTo("3.14%");
    }

    [Test]
    public async Task EnumConverter_RoundTrip_CaseInsensitiveParse()
    {
        bool ok = EnumConverter<DayOfWeek>.Instance.TryParse("monday", out DayOfWeek v);
        await Assert.That(ok).IsTrue();
        await Assert.That(v).IsEqualTo(DayOfWeek.Monday);
    }

    [Test]
    public async Task EnumConverter_Format_WritesName()
    {
        char[] buf = new char[16];
        bool ok = EnumConverter<DayOfWeek>.Instance.TryFormat(DayOfWeek.Monday, buf, out int cw);
        string formatted = new string(buf, 0, cw);
        await Assert.That(ok).IsTrue();
        await Assert.That(formatted).IsEqualTo("Monday");
    }

    [Test]
    public async Task ISpanConverter_Instance_IsSingleton()
    {
        await Assert.That(Int32Converter.Instance).IsTypeOf<Int32Converter>();
        await Assert.That(ReferenceEquals(Int32Converter.Instance, Int32Converter.Instance)).IsTrue();
    }
}
