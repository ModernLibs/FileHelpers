// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using ModernLibs.FileHelpers.Converters;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>
/// P9: <see cref="BoolConverter"/> matches FileHelpers 3.x
/// <c>FileHelpers/Converters/BooleanConverter.cs</c> (legacy source is the ground truth).
/// </summary>
[Category("Converters")]
[Category("Parity")]
public sealed class BoolConverterParityTests
{
    [Test]
    [Arguments("true", true)]
    [Arguments("TRUE", true)]
    [Arguments("1", true)]
    [Arguments("y", true)]
    [Arguments("Y", true)]
    [Arguments("t", true)]
    [Arguments("T", true)]
    [Arguments("false", false)]
    [Arguments("False", false)]
    [Arguments("0", false)]
    [Arguments("n", false)]
    [Arguments("N", false)]
    [Arguments("f", false)]
    [Arguments("F", false)]
    [Arguments("  true ", true)]
    [Arguments("\tY\t", true)]
    [Arguments(" 0 ", false)]
    [DisplayName("BooleanConverter.cs default values: '$text' parses as $expected (trimmed, case-insensitive)")]
    public async Task Default_AcceptsLegacyValues(string text, bool expected)
    {
        // Act
        var (ok, value) = ParityHelpers.Parse(BoolConverter.Instance, text);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(expected);
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [DisplayName("BooleanConverter.cs default values: empty / blank '$text' parses as false")]
    public async Task Default_EmptyOrBlank_IsFalse(string text)
    {
        // Act
        var (ok, value) = ParityHelpers.Parse(BoolConverter.Instance, text);

        // Assert: 3.x maps the trimmed empty string to false
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsFalse();
    }

    [Test]
    [Arguments("yes")]
    [Arguments("no")]
    [Arguments("maybe")]
    [Arguments("2")]
    [Arguments("tr ue")]
    [DisplayName("BooleanConverter.cs default values: '$text' is rejected (3.x had no yes/no)")]
    public async Task Default_RejectsUnknownValues(string text)
    {
        // Act
        var (ok, value) = ParityHelpers.Parse(BoolConverter.Instance, text);

        // Assert
        await Assert.That(ok).IsFalse();
        await Assert.That(value).IsFalse();
    }

    [Test]
    [DisplayName("BooleanConverter.cs FieldToString: default values format as True / False")]
    public async Task Default_Formats_TrueFalse()
    {
        // Act
        string? t = ParityHelpers.Format(BoolConverter.Instance, true);
        string? f = ParityHelpers.Format(BoolConverter.Instance, false);

        // Assert
        await Assert.That(t).IsEqualTo("True");
        await Assert.That(f).IsEqualTo("False");
    }

    [Test]
    [Arguments("Si", true)]
    [Arguments("si", true)]
    [Arguments("SI", true)]
    [Arguments(" si  ", true)]
    [Arguments("No", false)]
    [Arguments("NO", false)]
    [Arguments("\tno", false)]
    [DisplayName("BooleanConverter.cs (trueStr, falseStr): '$text' parses as $expected (case-insensitive, trim fallback)")]
    public async Task Custom_MatchesConfiguredStrings(string text, bool expected)
    {
        // Arrange
        var converter = new BoolConverter("Si", "No");

        // Act
        var (ok, value) = ParityHelpers.Parse(converter, text);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(expected);
    }

    [Test]
    [Arguments("true")]
    [Arguments("1")]
    [Arguments("0")]
    [Arguments("")]
    [Arguments("S")]
    [DisplayName("BooleanConverter.cs (trueStr, falseStr): '$text' is rejected — defaults no longer apply")]
    public async Task Custom_RejectsOtherValues(string text)
    {
        // Arrange
        var converter = new BoolConverter("Si", "No");

        // Act
        var (ok, _) = ParityHelpers.Parse(converter, text);

        // Assert
        await Assert.That(ok).IsFalse();
    }

    [Test]
    [DisplayName("BooleanConverter.cs (trueStr, falseStr): formats with the configured strings verbatim and round-trips")]
    public async Task Custom_Formats_ConfiguredStrings_RoundTrip()
    {
        // Arrange
        var converter = new BoolConverter("Si", "No");

        // Act
        string? t = ParityHelpers.Format(converter, true);
        string? f = ParityHelpers.Format(converter, false);
        var (okT, vT) = ParityHelpers.Parse(converter, t!);
        var (okF, vF) = ParityHelpers.Parse(converter, f!);

        // Assert
        await Assert.That(t).IsEqualTo("Si");
        await Assert.That(f).IsEqualTo("No");
        await Assert.That(okT && vT).IsTrue();
        await Assert.That(okF && !vF).IsTrue();
    }

    [Test]
    [DisplayName("BoolConverter.TryFormat returns false when the destination is too small")]
    public async Task Format_DestinationTooSmall_ReturnsFalse()
    {
        // Arrange
        var converter = new BoolConverter("Verdadero", "Falso");
        char[] buffer = new char[3];

        // Act
        bool ok = converter.TryFormat(true, buffer, out int written);

        // Assert
        await Assert.That(ok).IsFalse();
        await Assert.That(written).IsEqualTo(0);
    }
}
