// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests.
#pragma warning disable CA1849
// CA1859: Interface-typed variables are intentional — tests verify interface-based usage.
#pragma warning disable CA1859

using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// CultureConverter tests — per-field CultureInfo support.
/// </summary>
public class CultureConverterTests
{
    [Test]
    [DisplayName("European decimal parsing with fr-FR culture")]
    public async Task EuropeanDecimal_Parse_FrFr()
    {
        // Arrange: fr-FR uses comma as decimal separator; record uses semicolon delimiter
        var engine = new FileHelperEngine<EuropeanDecimalRecord>();
        const string input = "42,5;Test";

        // Act: parse European format
        var result = engine.ReadString(input);

        // Assert: comma parsed as decimal point
        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result[0].Amount).IsEqualTo(42.5m);
        await Assert.That(result[0].Name).IsEqualTo("Test");
    }

    [Test]
    [DisplayName("European decimal formatting with fr-FR culture")]
    public async Task EuropeanDecimal_Format_FrFr()
    {
        // Arrange: record with decimal value
        var engine = new FileHelperEngine<EuropeanDecimalRecord>();
        var record = new EuropeanDecimalRecord { Amount = 42.5m, Name = "Test" };

        // Act: write with fr-FR culture
        var output = engine.WriteString(new[] { record });

        // Assert: comma as decimal separator in output
        await Assert.That(output).Contains("42,5");
    }

    [Test]
    [DisplayName("German date parsing with de-DE culture")]
    public async Task GermanDate_Parse_DeDe()
    {
        // Arrange: dd.MM.yyyy format with de-DE culture
        var engine = new FileHelperEngine<GermanDateRecord>();
        const string input = "1;15.03.2024;Event";

        // Act: parse German date format
        var result = engine.ReadString(input);

        // Assert: date parsed correctly
        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result[0].Id).IsEqualTo(1);
        await Assert.That(result[0].Date).IsEqualTo(new DateTime(2024, 3, 15));
        await Assert.That(result[0].Description).IsEqualTo("Event");
    }

    [Test]
    [DisplayName("InvariantCulture default for decimal without culture")]
    public async Task InvariantDecimal_Parse_DotSeparator()
    {
        // Arrange: InvariantCulture uses dot as decimal separator
        var engine = new FileHelperEngine<InvariantDecimalRecord>();
        const string input = "42.5;Test";

        // Act: parse with InvariantCulture
        var result = engine.ReadString(input);

        // Assert: dot parsed as decimal point
        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result[0].Amount).IsEqualTo(42.5m);
        await Assert.That(result[0].Name).IsEqualTo("Test");
    }

    [Test]
    [DisplayName("InvariantCulture does not accept European decimal format")]
    public async Task InvariantDecimal_Rejects_CommaSeparator()
    {
        // Arrange: InvariantCulture should not accept comma as decimal separator
        var engine = new FileHelperEngine<InvariantDecimalRecord>();
        const string input = "42,5;Test";

        // Act: attempt to parse European format with InvariantCulture
        var result = engine.ReadString(input);

        // Assert: parsing either fails or returns incorrect value (comma not accepted)
        // InvariantCulture may parse "42" and ignore ",5", or fail entirely
        if (result.Length > 0)
        {
            // If it parsed, the value should NOT be 42.5 (European format not recognized)
            await Assert.That(result[0].Amount).IsNotEqualTo(42.5m);
        }
    }

    [Test]
    [DisplayName("Multiple cultures in same record parsed correctly")]
    public async Task MultiCulture_Parse_ThreeCultures()
    {
        // Arrange: record with fr-FR, de-DE, and InvariantCulture fields; semicolon delimiter
        var engine = new FileHelperEngine<MultiCultureRecord>();
        const string input = "42,5;123,45;99.99";

        // Act: parse with multiple cultures
        var result = engine.ReadString(input);

        // Assert: each field parsed with its own culture
        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result[0].FrenchAmount).IsEqualTo(42.5m);
        await Assert.That(result[0].GermanAmount).IsEqualTo(123.45m);
        await Assert.That(result[0].InvariantAmount).IsEqualTo(99.99m);
    }

    [Test]
    [DisplayName("Fixed-length record with culture-aware decimal parsing")]
    public async Task FixedCulture_Parse_FrFr()
    {
        // Arrange: fixed-length record with fr-FR culture (10 + 20 = 30 chars)
        // "     42,5" = 10 chars (6 spaces padding + "42,5")
        var engine = new FileHelperEngine<FixedCultureRecord>();
        const string input = "      42,5Test                ";

        // Act: parse fixed-length with culture
        var result = engine.ReadString(input);

        // Assert: decimal parsed with fr-FR culture
        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result[0].Amount).IsEqualTo(42.5m);
        await Assert.That(result[0].Name.Trim()).IsEqualTo("Test");
    }

    [Test]
    [DisplayName("Double parsing with fr-FR culture")]
    public async Task CultureDouble_Parse_FrFr()
    {
        // Arrange: double field with fr-FR culture; semicolon delimiter
        var engine = new FileHelperEngine<CultureDoubleRecord>();
        const string input = "3,14159;Pi";

        // Act: parse with fr-FR culture
        var result = engine.ReadString(input);

        // Assert: comma parsed as decimal point for double
        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result[0].Value).IsEqualTo(3.14159).Within(0.00001);
        await Assert.That(result[0].Label).IsEqualTo("Pi");
    }

    [Test]
    [DisplayName("Culture-aware parse/write round-trip")]
    public async Task EuropeanDecimal_RoundTrip()
    {
        // Arrange: parse European format
        var engine = new FileHelperEngine<EuropeanDecimalRecord>();
        const string input = "42,5;Test";
        var parsed = engine.ReadString(input);

        // Act: write back
        var output = engine.WriteString(parsed);

        // Assert: output matches original input
        await Assert.That(output.Trim()).IsEqualTo(input);
    }
}
