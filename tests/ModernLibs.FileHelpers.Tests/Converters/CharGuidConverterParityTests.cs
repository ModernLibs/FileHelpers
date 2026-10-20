// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Char and Guid parity tests grouped in one file.
#pragma warning disable MA0048

using ModernLibs.FileHelpers.Converters;
using ModernLibs.FileHelpers.Tests.Errors;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>
/// P13: <see cref="CharConverter"/> matches FileHelpers 3.x <c>FileHelpers/Converters/CharConverter.cs</c>.
/// </summary>
[Category("Converters")]
[Category("Parity")]
public sealed class CharConverterParityTests
{
    [Test]
    [Arguments("A", 'A')]
    [Arguments("AB", 'A')]
    [Arguments("xyz", 'x')]
    [Arguments(" A", ' ')]
    [Arguments(" ", ' ')]
    [DisplayName("CharConverter.cs: '$text' parses as its first character, untrimmed")]
    public async Task Parse_TakesFirstCharacter(string text, char expected)
    {
        // Act
        var (ok, value) = ParityHelpers.Parse(CharConverter.Instance, text);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(expected);
    }

    [Test]
    [DisplayName("CharConverter.cs: empty input parses as '\\0' (char.MinValue)")]
    public async Task Parse_Empty_IsMinValue()
    {
        // Act
        var (ok, value) = ParityHelpers.Parse(CharConverter.Instance, string.Empty);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(char.MinValue);
    }

    [Test]
    [Arguments("x", "ab", 'a', "a")]
    [Arguments("lower", "Ab", 'a', "a")]
    [Arguments(" x ", "Q", 'q', "q")]
    [Arguments("X", "ab", 'A', "A")]
    [Arguments("upper", "zZ", 'Z', "Z")]
    [Arguments("", "aB", 'a', "a")]
    [DisplayName("CharConverter.cs(format '$format'): '$text' parses as '$expected' and formats as '$formatted'")]
    public async Task Format_Argument_ChangesCase(string format, string text, char expected, string formatted)
    {
        // Arrange
        var converter = new CharConverter(format);

        // Act
        var (ok, value) = ParityHelpers.Parse(converter, text);
        string? written = ParityHelpers.Format(converter, value);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(expected);
        await Assert.That(written).IsEqualTo(formatted);
    }

    [Test]
    [DisplayName("CharConverter.cs(format 'X'): formatting upper-cases a lower-case value")]
    public async Task Format_Upper_UpperCasesOnWrite()
    {
        // Act
        string? written = ParityHelpers.Format(new CharConverter("X"), 'q');

        // Assert
        await Assert.That(written).IsEqualTo("Q");
    }

    [Test]
    [Arguments("u")]
    [Arguments("UPPER")]
    [Arguments("Lower")]
    [DisplayName("CharConverter.cs: invalid format '$format' raises BadUsageException")]
    public async Task InvalidFormat_Throws(string format)
    {
        // Act
        var ex = Capture.Exception(() => _ = new CharConverter(format));

        // Assert
        await Assert.That(ex).IsTypeOf<BadUsageException>();
    }
}

/// <summary>
/// P12: <see cref="GuidConverter"/> matches FileHelpers 3.x <c>FileHelpers/Converters/GuidConverter.cs</c>.
/// </summary>
[Category("Converters")]
[Category("Parity")]
public sealed class GuidConverterParityTests
{
    private static readonly Guid Sample = new("0f8fad5b-d9cb-469f-a165-70867728950e");

    [Test]
    [Arguments("0f8fad5b-d9cb-469f-a165-70867728950e")]
    [Arguments("0f8fad5bd9cb469fa16570867728950e")]
    [Arguments("{0f8fad5b-d9cb-469f-a165-70867728950e}")]
    [Arguments("(0f8fad5b-d9cb-469f-a165-70867728950e)")]
    [Arguments("{0x0f8fad5b,0xd9cb,0x469f,{0xa1,0x65,0x70,0x86,0x77,0x28,0x95,0x0e}}")]
    [Arguments("  0F8FAD5B-D9CB-469F-A165-70867728950E  ")]
    [DisplayName("GuidConverter.cs: new Guid(s) accepts '$text'")]
    public async Task Parse_AcceptsEveryGuidFormat(string text)
    {
        // Act
        var (ok, value) = ParityHelpers.Parse(GuidConverter.Instance, text);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(Sample);
    }

    [Test]
    [DisplayName("GuidConverter.cs: empty input parses as Guid.Empty")]
    public async Task Parse_Empty_IsGuidEmpty()
    {
        // Act
        var (ok, value) = ParityHelpers.Parse(new GuidConverter("N"), string.Empty);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(Guid.Empty);
    }

    [Test]
    [Arguments("not-a-guid")]
    [Arguments("0f8fad5b-d9cb-469f-a165")]
    [DisplayName("GuidConverter.cs: '$text' is rejected")]
    public async Task Parse_Invalid_ReturnsFalse(string text)
    {
        // Act
        var (ok, _) = ParityHelpers.Parse(GuidConverter.Instance, text);

        // Assert
        await Assert.That(ok).IsFalse();
    }

    [Test]
    [Arguments("D", "0f8fad5b-d9cb-469f-a165-70867728950e")]
    [Arguments("N", "0f8fad5bd9cb469fa16570867728950e")]
    [Arguments("B", "{0f8fad5b-d9cb-469f-a165-70867728950e}")]
    [Arguments("P", "(0f8fad5b-d9cb-469f-a165-70867728950e)")]
    [Arguments(" b ", "{0f8fad5b-d9cb-469f-a165-70867728950e}")]
    [Arguments("", "0f8fad5b-d9cb-469f-a165-70867728950e")]
    [DisplayName("GuidConverter.cs(format '$format'): writes '$expected' and round-trips")]
    public async Task Format_Argument_RoundTrips(string format, string expected)
    {
        // Arrange
        var converter = new GuidConverter(format);

        // Act
        string? written = ParityHelpers.Format(converter, Sample);
        var (ok, value) = ParityHelpers.Parse(converter, written!);

        // Assert
        await Assert.That(written).IsEqualTo(expected);
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(Sample);
    }

    [Test]
    [Arguments("X")]
    [Arguments("Q")]
    [DisplayName("GuidConverter.cs: format '$format' raises BadUsageException (only N, D, B, P)")]
    public async Task InvalidFormat_Throws(string format)
    {
        // Act
        var ex = Capture.Exception(() => _ = new GuidConverter(format));

        // Assert
        await Assert.That(ex).IsTypeOf<BadUsageException>();
    }
}
