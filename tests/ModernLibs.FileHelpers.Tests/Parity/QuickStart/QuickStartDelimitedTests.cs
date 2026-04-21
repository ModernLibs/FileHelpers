// CA1707: Test method names use underscores for readability.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional.
#pragma warning disable MA0048
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using ModernLibs.FileHelpers.Tests.Parity.QuickStart.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parity.QuickStart;

/// <summary>
/// TEST-01, TEST-02: QuickStart Delimited Read/Write tests.
/// Based on original FileHelpers QuickStart examples from https://www.filehelpers.net/quickstart/
/// </summary>
[Category("Phase19")]
[Category("Parity")]
[Category("QuickStart")]
public sealed class QuickStartDelimitedTests
{
    #region TEST-01: Read Delimited File

    /// <summary>
    /// TEST-01: QuickStart Read Delimited File - matches original FileHelpers behavior.
    /// </summary>
    [Test]
    [DisplayName("TEST-01: Read delimited CSV file - matches original FileHelpers.ReadFile behavior")]
    public async Task ReadDelimited_MatchesOriginalBehavior()
    {
        // Arrange: Input matching original FileHelpers QuickStart example
        var input = "John Doe,30,USA\r\nJane Smith,25,Canada\r\nBob Wilson,35,UK";
        var engine = new FileHelperEngine<Customer>();

        // Act: Parse using ModernLibs
        var records = engine.ReadString(input);

        // Assert: All 3 records parsed correctly
        await Assert.That(records.Length).IsEqualTo(3);

        await Assert.That(records[0].Name).IsEqualTo("John Doe");
        await Assert.That(records[0].Age).IsEqualTo(30);
        await Assert.That(records[0].Country).IsEqualTo("USA");

        await Assert.That(records[1].Name).IsEqualTo("Jane Smith");
        await Assert.That(records[1].Age).IsEqualTo(25);
        await Assert.That(records[1].Country).IsEqualTo("Canada");

        await Assert.That(records[2].Name).IsEqualTo("Bob Wilson");
        await Assert.That(records[2].Age).IsEqualTo(35);
        await Assert.That(records[2].Country).IsEqualTo("UK");
    }

    /// <summary>
    /// TEST-01: Read delimited with header skip matches original FileHelpers [IgnoreFirst] behavior.
    /// </summary>
    [Test]
    [DisplayName("TEST-01: Read delimited with header - IgnoreFirst captures header in HeaderText")]
    public async Task ReadDelimited_WithHeader_MatchesOriginalBehavior()
    {
        // Arrange: CSV with header row
        var input = "Name,Age,Country\r\nJohn Doe,30,USA\r\nJane Smith,25,Canada";
        var engine = new FileHelperEngine<CustomerWithHeader>();

        // Act: Parse with header
        var records = engine.ReadString(input);

        // Assert: Header skipped, HeaderText captured, 2 records parsed
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(engine.HeaderText).IsEqualTo("Name,Age,Country");

        await Assert.That(records[0].Name).IsEqualTo("John Doe");
        await Assert.That(records[0].Age).IsEqualTo(30);
    }

    /// <summary>
    /// TEST-01: Read delimited from file path works like original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("TEST-01: ReadFile reads from file path - matches original FileHelpers behavior")]
    public async Task ReadDelimited_FromFile_MatchesOriginalBehavior()
    {
        // Arrange: Write test file
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_read_{Guid.NewGuid()}.txt");
        var content = "John Doe,30,USA\r\nJane Smith,25,Canada";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            var engine = new FileHelperEngine<Customer>();

            // Act: Read from file path
            var records = engine.ReadFile(tempFile);

            // Assert: Records parsed correctly
            await Assert.That(records.Length).IsEqualTo(2);
            await Assert.That(records[0].Name).IsEqualTo("John Doe");
            await Assert.That(records[1].Country).IsEqualTo("Canada");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// TEST-01: ReadStream from TextReader matches original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("TEST-01: ReadStream reads from TextReader - matches original FileHelpers behavior")]
    public async Task ReadDelimited_FromStream_MatchesOriginalBehavior()
    {
        // Arrange
        var input = "John Doe,30,USA\r\nJane Smith,25,Canada";
        var engine = new FileHelperEngine<Customer>();

        // Act: Read from stream
        using var reader = new StringReader(input);
        var records = engine.ReadStream(reader);

        // Assert
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Name).IsEqualTo("John Doe");
    }

    #endregion

    #region TEST-02: Write Delimited File

    /// <summary>
    /// TEST-02: Write delimited CSV file - matches original FileHelpers behavior.
    /// </summary>
    [Test]
    [DisplayName("TEST-02: Write delimited CSV - matches original FileHelpers.WriteFile behavior")]
    public async Task WriteDelimited_MatchesOriginalBehavior()
    {
        // Arrange: Create records like original FileHelpers example
        var records = new[]
        {
            new Customer { Name = "John Doe", Age = 30, Country = "USA" },
            new Customer { Name = "Jane Smith", Age = 25, Country = "Canada" },
            new Customer { Name = "Bob Wilson", Age = 35, Country = "UK" }
        };
        var engine = new FileHelperEngine<Customer>();

        // Act: Write to string
        var output = engine.WriteString(records);

        // Assert: Output matches expected CSV format
        var expected = "John Doe,30,USA\r\nJane Smith,25,Canada\r\nBob Wilson,35,UK\r\n";
        await Assert.That(output).IsEqualTo(expected);
    }

    /// <summary>
    /// TEST-02: Write delimited to file path works like original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("TEST-02: WriteFile writes to file path - matches original FileHelpers behavior")]
    public async Task WriteDelimited_ToFile_MatchesOriginalBehavior()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_write_{Guid.NewGuid()}.txt");
        var records = new[]
        {
            new Customer { Name = "John Doe", Age = 30, Country = "USA" }
        };
        var engine = new FileHelperEngine<Customer>();

        try
        {
            // Act: Write to file
            engine.WriteFile(tempFile, records);

            // Assert: File content matches expected CSV
            var content = await File.ReadAllTextAsync(tempFile);
            await Assert.That(content.TrimEnd()).IsEqualTo("John Doe,30,USA");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// TEST-02: WriteStream to TextWriter matches original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("TEST-02: WriteStream writes to TextWriter - matches original FileHelpers behavior")]
    public async Task WriteDelimited_ToStream_MatchesOriginalBehavior()
    {
        // Arrange
        var records = new[]
        {
            new Customer { Name = "John Doe", Age = 30, Country = "USA" }
        };
        var engine = new FileHelperEngine<Customer>();
        var writer = new StringWriter();

        // Act
        engine.WriteStream(writer, records);

        // Assert
        var output = writer.ToString();
        await Assert.That(output.TrimEnd()).IsEqualTo("John Doe,30,USA");
    }

    #endregion

    #region Round-Trip Tests (Read -> Write -> Read)

    /// <summary>
    /// TEST-01/02: Round-trip verification - read then write then read produces equivalent results.
    /// </summary>
    [Test]
    [DisplayName("TEST-01/02: Round-trip read-write-read produces equivalent records")]
    public async Task RoundTrip_ProducesEquivalentRecords()
    {
        // Arrange: Original input
        var originalInput = "John Doe,30,USA\r\nJane Smith,25,Canada\r\nBob Wilson,35,UK";
        var readEngine = new FileHelperEngine<Customer>();
        var originalRecords = readEngine.ReadString(originalInput);

        // Act: Write records to string
        var writeEngine = new FileHelperEngine<Customer>();
        var intermediateOutput = writeEngine.WriteString(originalRecords);

        // Act: Read back from output
        var roundTripEngine = new FileHelperEngine<Customer>();
        var roundTripRecords = roundTripEngine.ReadString(intermediateOutput);

        // Assert: Round-trip produces same data
        await Assert.That(roundTripRecords.Length).IsEqualTo(originalRecords.Length);

        for (int i = 0; i < originalRecords.Length; i++)
        {
            await Assert.That(roundTripRecords[i].Name).IsEqualTo(originalRecords[i].Name);
            await Assert.That(roundTripRecords[i].Age).IsEqualTo(originalRecords[i].Age);
            await Assert.That(roundTripRecords[i].Country).IsEqualTo(originalRecords[i].Country);
        }
    }

    #endregion
}
