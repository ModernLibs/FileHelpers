// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using System.Globalization;
using ModernLibs.FileHelpers.Converters;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>A format provider that fails, as a broken custom culture would.</summary>
public sealed class NcThrowingProvider : IFormatProvider
{
    public object? GetFormat(Type? formatType) => throw new InvalidOperationException("provider failed");
}

/// <summary>
/// The 3.x slow path of number parsing joins a detached sign to its digits in a pooled buffer when the text is
/// longer than its stack buffer; the buffer goes back to the pool even when the parse throws.
/// </summary>
[Category("Converters")]
public sealed class NumberConversionTests
{
    private static string DetachedSign(int digits) => "- " + new string('1', digits);

    [Test]
    [DisplayName("Detached sign longer than the stack buffer: an exception of the format provider propagates, and parsing goes on working")]
    public async Task PooledPath_ProviderThrows_Propagates()
    {
        // Arrange: 200 digits take the pooled path
        var text = DetachedSign(200);

        // Act
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            NumberConversion.TryParse<decimal>(text, NumberStyles.Number, new NcThrowingProvider(), out _));
        bool ok = NumberConversion.TryParse<double>(text, NumberStyles.Number, CultureInfo.InvariantCulture, out double value);

        // Assert
        await Assert.That(thrown.Message).IsEqualTo("provider failed");
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsLessThan(0d);
    }
}
