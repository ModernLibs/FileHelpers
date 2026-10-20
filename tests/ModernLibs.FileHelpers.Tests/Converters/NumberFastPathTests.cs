// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707

using System.Globalization;
using System.Numerics;
using ModernLibs.FileHelpers.Converters;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>
/// The numeric converters' fast path for plain ASCII numbers must give exactly the result of the
/// FileHelpers 3.x parse (<c>NumberStyles.Number | NumberStyles.AllowExponent</c>, invariant culture):
/// same success/failure and the same value (including decimal scale and double bits).
/// </summary>
public sealed class NumberFastPathTests
{
    private const NumberStyles Styles = NumberStyles.Number | NumberStyles.AllowExponent;

    private static readonly string[] Inputs =
    [
        "0", "7", "-7", "+7", "007", "-0", "+0", "  42", "42  ", "\t42\r", " -42 ", "- 42", "4 2", "",
        "   ", "-", "+", "1.", ".5", "-.5", "0.10", "1.000", "12.34", "-12.34", "99999999", "999999999",
        "1000000000", "2147483647", "2147483648", "-2147483648", "-2147483649", "9223372036854775807",
        "-9223372036854775808", "9223372036854775808", "18446744073709551615", "18446744073709551616",
        "123456789012345678", "1234567890123456789", "0.1", "0.2", "0.3", "1.7976931348623157", "3.4028235",
        "123456789012345.6", "1234567.8", "0.000000000000000000001", "1e5", "1,000", "1.2.3", "12a",
        " 12", "12 ", "５", "255", "256", "-1", "127", "128", "-128", "-129", "32767", "32768",
        "65535", "65536", "4294967295", "4294967296", "0.0000001", "9999999", "99999999.5",
    ];

    /// <summary>Reference for 3.x <c>ConvertHelpers.RemoveBlanks</c>: leading blanks and blanks after a leading sign go.</summary>
    private static string RemoveBlanks(string input)
    {
        string trimmed = input.TrimStart();
        if (trimmed.Length >= 2 && trimmed[0] is '+' or '-' && char.IsWhiteSpace(trimmed[1]))
            return trimmed[0] + trimmed[1..].TrimStart();
        return trimmed;
    }

    private static async Task AssertSameAsFullParse<T>(ISpanConverter<T> converter, NumberStyles styles)
        where T : struct, INumberBase<T>
    {
        foreach (string input in Inputs)
        {
            bool expectedOk = T.TryParse(RemoveBlanks(input), styles, CultureInfo.InvariantCulture, out T expected);
            bool actualOk = converter.TryParse(input, out T actual);
            await Assert.That(actualOk).IsEqualTo(expectedOk).Because($"{typeof(T).Name} '{input}'");
            if (!expectedOk) continue;
            if (typeof(T) == typeof(decimal))
            {
                // Same value AND same scale/sign bits.
                int[] e = decimal.GetBits((decimal)(object)expected);
                int[] a = decimal.GetBits((decimal)(object)actual);
                await Assert.That(a).IsEquivalentTo(e).Because($"decimal bits '{input}'");
            }
            else if (typeof(T) == typeof(double))
            {
                await Assert.That(BitConverter.DoubleToInt64Bits((double)(object)actual))
                    .IsEqualTo(BitConverter.DoubleToInt64Bits((double)(object)expected)).Because($"double bits '{input}'");
            }
            else if (typeof(T) == typeof(float))
            {
                await Assert.That(BitConverter.SingleToInt32Bits((float)(object)actual))
                    .IsEqualTo(BitConverter.SingleToInt32Bits((float)(object)expected)).Because($"float bits '{input}'");
            }
            else
            {
                await Assert.That(actual).IsEqualTo(expected).Because($"{typeof(T).Name} '{input}'");
            }
        }
    }

    [Test]
    [DisplayName("Integer converters: fast path matches the 3.x parse for every sample input")]
    public async Task Integers_MatchFullParse()
    {
        // Arrange / Act / Assert: compare each integer converter with the reference parse
        await AssertSameAsFullParse(Int32Converter.Instance, Styles).ConfigureAwait(false);
        await AssertSameAsFullParse(Int64Converter.Instance, Styles).ConfigureAwait(false);
        await AssertSameAsFullParse(Int16Converter.Instance, Styles).ConfigureAwait(false);
        await AssertSameAsFullParse(UInt16Converter.Instance, Styles).ConfigureAwait(false);
        await AssertSameAsFullParse(UInt32Converter.Instance, Styles).ConfigureAwait(false);
        await AssertSameAsFullParse(UInt64Converter.Instance, Styles).ConfigureAwait(false);
        await AssertSameAsFullParse(ByteConverter.Instance, NumberStyles.Number).ConfigureAwait(false);
        await AssertSameAsFullParse(SByteConverter.Instance, NumberStyles.Number).ConfigureAwait(false);
    }

    [Test]
    [DisplayName("Decimal/Double/Single converters: fast path matches the 3.x parse bit for bit")]
    public async Task Floating_MatchFullParse()
    {
        // Arrange / Act / Assert: compare value bits with the reference parse
        await AssertSameAsFullParse(DecimalConverter.Instance, Styles).ConfigureAwait(false);
        await AssertSameAsFullParse(DoubleConverter.Instance, Styles).ConfigureAwait(false);
        await AssertSameAsFullParse(SingleConverter.Instance, Styles).ConfigureAwait(false);
    }

    [Test]
    [DisplayName("Fast path is not used for other separators: decimal comma still parses through the full path")]
    public async Task CommaSeparator_UsesFullPath()
    {
        // Arrange: converter with ',' decimal separator
        var converter = new DecimalConverter(",");

        // Act: parse a comma decimal and a dot decimal
        bool comma = converter.TryParse("1,5", out decimal commaValue);
        bool dot = converter.TryParse("1.5", out decimal dotValue);

        // Assert: comma is the decimal separator, '.' is a group separator (3.x)
        await Assert.That(comma).IsTrue();
        await Assert.That(commaValue).IsEqualTo(1.5m);
        await Assert.That(dot).IsTrue();
        await Assert.That(dotValue).IsEqualTo(15m);
    }
}
