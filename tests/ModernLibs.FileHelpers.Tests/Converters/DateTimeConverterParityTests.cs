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
/// P10: date converters match FileHelpers 3.x <c>FileHelpers/Converters/DateTimeConverter.cs</c>,
/// <c>DateTimeMultiFormatConverter.cs</c> and the <c>ConverterBase.DefaultDateTimeFormat</c> default.
/// </summary>
[Category("Converters")]
[Category("Parity")]
public sealed class DateTimeConverterParityTests
{
    [Test]
    [Arguments("25122026")]
    [Arguments("  25122026 ")]
    [Arguments("\t25122026\t")]
    [DisplayName("DateTimeConverter.cs: default format ddMMyyyy parses '$text' (input trimmed)")]
    public async Task Default_IsLegacyFormat_Trimmed(string text)
    {
        // Act
        var (ok, value) = ParityHelpers.Parse(DateTimeConverter.Instance, text);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(new DateTime(2026, 12, 25));
    }

    [Test]
    [DisplayName("DateTimeConverter.cs: default format writes ddMMyyyy and round-trips")]
    public async Task Default_Formats_LegacyFormat()
    {
        // Act
        string? written = ParityHelpers.Format(DateTimeConverter.Instance, new DateTime(2026, 1, 7, 13, 45, 0));
        var (ok, value) = ParityHelpers.Parse(DateTimeConverter.Instance, written!);

        // Assert
        await Assert.That(written).IsEqualTo("07012026");
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(new DateTime(2026, 1, 7));
    }

    [Test]
    [Arguments("12/25/2026")]
    [Arguments("2026-12-25")]
    [Arguments("251226")]
    [DisplayName("DateTimeConverter.cs: default format rejects '$text'")]
    public async Task Default_RejectsOtherFormats(string text)
    {
        // Act
        var (ok, _) = ParityHelpers.Parse(DateTimeConverter.Instance, text);

        // Assert
        await Assert.That(ok).IsFalse();
    }

    [Test]
    [DisplayName("DateTimeConverter.cs(format): explicit format parses trimmed input and round-trips")]
    public async Task ExplicitFormat_Trimmed_RoundTrips()
    {
        // Arrange
        var converter = new DateTimeConverter("yyyy-MM-dd HH:mm");

        // Act
        var (ok, value) = ParityHelpers.Parse(converter, "  2026-03-04 05:06  ");
        string? written = ParityHelpers.Format(converter, value);

        // Assert
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(new DateTime(2026, 3, 4, 5, 6, 0));
        await Assert.That(written).IsEqualTo("2026-03-04 05:06");
    }

    [Test]
    [DisplayName("DateTimeConverter.cs(format, culture): culture-specific month names parse and round-trip")]
    public async Task ExplicitCulture_RoundTrips()
    {
        // Arrange
        var converter = new DateTimeConverter("dd MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR"));
        var date = new DateTime(2026, 12, 25);

        // Act
        string? written = ParityHelpers.Format(converter, date);
        var (ok, value) = ParityHelpers.Parse(converter, " " + written + " ");

        // Assert
        await Assert.That(written).IsEqualTo("25 décembre 2026");
        await Assert.That(ok).IsTrue();
        await Assert.That(value).IsEqualTo(date);
    }

    [Test]
    [NotInParallel("CurrentCulture")]
    [DisplayName("Date converters without a culture ignore the current culture ('/' stays literal)")]
    public async Task NoCulture_IgnoresCurrentCulture()
    {
        // Arrange
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var converter = new DateTimeConverter("dd/MM/yyyy");

            // Act
            var (ok, value) = ParityHelpers.Parse(converter, "25/12/2026");
            string? written = ParityHelpers.Format(converter, value);

            // Assert
            await Assert.That(ok).IsTrue();
            await Assert.That(written).IsEqualTo("25/12/2026");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    [Arguments("")]
    [Arguments("%")]
    [DisplayName("DateTimeConverter.cs: format '$format' raises BadUsageException")]
    public async Task InvalidFormat_Throws(string format)
    {
        // Act
        var ex = Capture.Exception(() => _ = new DateTimeConverter(format));

        // Assert
        await Assert.That(ex).IsTypeOf<BadUsageException>();
    }

    [Test]
    [DisplayName("DateTimeMultiFormatConverter.cs: tries each format on trimmed input and writes the first")]
    public async Task MultiFormat_TrimsAndWritesFirstFormat()
    {
        // Arrange
        var converter = new DateMultiFormatConverter(["yyyyMMdd", "dd/MM/yyyy", "MM-dd-yyyy"]);

        // Act
        var first = ParityHelpers.Parse(converter, " 20261225 ");
        var second = ParityHelpers.Parse(converter, "25/12/2026\t");
        var third = ParityHelpers.Parse(converter, "12-25-2026");
        var bad = ParityHelpers.Parse(converter, "2026.12.25");
        string? written = ParityHelpers.Format(converter, new DateTime(2026, 12, 25));

        // Assert
        await Assert.That(first).IsEqualTo((true, new DateTime(2026, 12, 25)));
        await Assert.That(second).IsEqualTo((true, new DateTime(2026, 12, 25)));
        await Assert.That(third).IsEqualTo((true, new DateTime(2026, 12, 25)));
        await Assert.That(bad.Ok).IsFalse();
        await Assert.That(written).IsEqualTo("20261225");
    }

    [Test]
    [DisplayName("DateTimeMultiFormatConverter.cs: no formats or an empty format raises BadUsageException")]
    public async Task MultiFormat_InvalidFormats_Throw()
    {
        // Act
        var none = Capture.Exception(() => _ = new DateMultiFormatConverter([]));
        var empty = Capture.Exception(() => _ = new DateMultiFormatConverter(["yyyyMMdd", ""]));

        // Assert
        await Assert.That(none).IsTypeOf<BadUsageException>();
        await Assert.That(empty).IsTypeOf<BadUsageException>();
    }

    [Test]
    [DisplayName("DateOnly / TimeOnly converters trim the input like the 3.x DateTimeConverter")]
    public async Task DateOnlyTimeOnly_Trim()
    {
        // Act + Assert
        await Assert.That(ParityHelpers.Parse(DateOnlyConverter.Instance, " 12/25/2026 ")).IsEqualTo((true, new DateOnly(2026, 12, 25)));
        await Assert.That(ParityHelpers.Parse(TimeOnlyConverter.Instance, "\t13:45:00 ")).IsEqualTo((true, new TimeOnly(13, 45, 0)));
    }

    [Test]
    [DisplayName("ConverterBase.cs: DefaultDateTimeFormat is ddMMyyyy by default")]
    public async Task DefaultDateTimeFormat_IsLegacyDefault()
    {
        // Assert
        await Assert.That(ConverterBase.DefaultDateTimeFormat).IsEqualTo("ddMMyyyy");
    }

    [Test]
    [NotInParallel]
    [DisplayName("ConverterBase.cs: setting DefaultDateTimeFormat changes the default DateTimeConverter")]
    public async Task DefaultDateTimeFormat_Setter_ChangesDefaultConverter()
    {
        // Arrange
        string previous = ConverterBase.DefaultDateTimeFormat;
        try
        {
            ConverterBase.DefaultDateTimeFormat = "yyyyMMdd";

            // Act
            var parsed = ParityHelpers.Parse(DateTimeConverter.Instance, "20261225");
            string? written = ParityHelpers.Format(new DateTimeConverter(), new DateTime(2026, 12, 25));
            var explicitFormat = ParityHelpers.Parse(new DateTimeConverter("ddMMyyyy"), "25122026");
            var invalid = Capture.Exception(() => ConverterBase.DefaultDateTimeFormat = "");

            // Assert
            await Assert.That(parsed).IsEqualTo((true, new DateTime(2026, 12, 25)));
            await Assert.That(written).IsEqualTo("20261225");
            await Assert.That(explicitFormat).IsEqualTo((true, new DateTime(2026, 12, 25)));
            await Assert.That(invalid).IsTypeOf<BadUsageException>();
            await Assert.That(ConverterBase.DefaultDateTimeFormat).IsEqualTo("yyyyMMdd");
        }
        finally
        {
            ConverterBase.DefaultDateTimeFormat = previous;
        }
    }
}
