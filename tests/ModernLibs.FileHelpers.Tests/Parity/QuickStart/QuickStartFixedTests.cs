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
/// QuickStart Fixed-Length Read/Write tests.
/// Based on original FileHelpers QuickStart examples from https://www.filehelpers.net/quickstart/
/// </summary>
[Category("Parity")]
[Category("QuickStart")]
public sealed class QuickStartFixedTests
{
    #region Read Fixed File

    /// <summary>
    /// Read fixed-length file - matches original FileHelpers behavior.
    /// </summary>
    [Test]
    [DisplayName("Read fixed-length file - matches original FileHelpers.ReadFile behavior")]
    public async Task ReadFixed_MatchesOriginalBehavior()
    {
        // Arrange: Fixed-width data (Name=20, Age=3, Country=10 = 33 chars per line)
        var input = "John Doe            30 USA       \r\nJane Smith          25 Canada    \r\nBob Wilson          35 UK        ";
        var engine = new FileHelperEngine<FixedCustomer>();

        // Act: Parse fixed-length records
        var records = engine.ReadString(input);

        // Assert: All 3 records parsed correctly
        await Assert.That(records.Length).IsEqualTo(3);

        await Assert.That(records[0].Name.Trim()).IsEqualTo("John Doe");
        await Assert.That(records[0].Age).IsEqualTo(30);
        await Assert.That(records[0].Country.Trim()).IsEqualTo("USA");

        await Assert.That(records[1].Name.Trim()).IsEqualTo("Jane Smith");
        await Assert.That(records[1].Age).IsEqualTo(25);
        await Assert.That(records[1].Country.Trim()).IsEqualTo("Canada");

        await Assert.That(records[2].Name.Trim()).IsEqualTo("Bob Wilson");
        await Assert.That(records[2].Age).IsEqualTo(35);
        await Assert.That(records[2].Country.Trim()).IsEqualTo("UK");
    }

    /// <summary>
    /// Read fixed-length from file path works like original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("ReadFile fixed-length - matches original FileHelpers behavior")]
    public async Task ReadFixed_FromFile_MatchesOriginalBehavior()
    {
        // Arrange: Write test file with fixed-width data
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_fixed_read_{Guid.NewGuid()}.txt");
        var content = "John Doe            30 USA       \r\nJane Smith          25 Canada    ";
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            var engine = new FileHelperEngine<FixedCustomer>();

            // Act: Read from file path
            var records = engine.ReadFile(tempFile);

            // Assert: Records parsed correctly
            await Assert.That(records.Length).IsEqualTo(2);
            await Assert.That(records[0].Name.Trim()).IsEqualTo("John Doe");
            await Assert.That(records[0].Age).IsEqualTo(30);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// ReadStream from TextReader for fixed-length matches original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("ReadStream fixed-length - matches original FileHelpers behavior")]
    public async Task ReadFixed_FromStream_MatchesOriginalBehavior()
    {
        // Arrange
        var input = "John Doe            30 USA       \r\nJane Smith          25 Canada    ";
        var engine = new FileHelperEngine<FixedCustomer>();

        // Act: Read from stream
        using var reader = new StringReader(input);
        var records = engine.ReadStream(reader);

        // Assert
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Name.Trim()).IsEqualTo("John Doe");
    }

    #endregion

    #region Write Fixed File

    /// <summary>
    /// Write fixed-length file - matches original FileHelpers behavior.
    /// </summary>
    [Test]
    [DisplayName("Write fixed-length file - matches original FileHelpers.WriteFile behavior")]
    public async Task WriteFixed_MatchesOriginalBehavior()
    {
        // Arrange: Create records with various field lengths
        var records = new[]
        {
            new FixedCustomer { Name = "John Doe", Age = 30, Country = "USA" },
            new FixedCustomer { Name = "Jane Smith", Age = 25, Country = "Canada" },
            new FixedCustomer { Name = "Bob", Age = 35, Country = "UK" }
        };
        var engine = new FileHelperEngine<FixedCustomer>();

        // Act: Write to string
        var output = engine.WriteString(records);

        // Assert: Each line is exactly 33 characters (20+3+10)
        var lines = output.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines.Length).IsEqualTo(3);

        foreach (var line in lines)
        {
            await Assert.That(line.Length).IsEqualTo(33);
        }

        // Verify field positioning by extracting fixed positions
        await Assert.That(lines[0].Substring(0, 20).Trim()).IsEqualTo("John Doe");
        await Assert.That(lines[0].Substring(20, 3)).IsEqualTo("30 ");
        await Assert.That(lines[0].Substring(23, 10).Trim()).IsEqualTo("USA");
    }

    /// <summary>
    /// Write fixed-length with left/right alignment matches original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("Write fixed-length with alignment - matches original FileHelpers behavior")]
    public async Task WriteFixed_WithAlignment_MatchesOriginalBehavior()
    {
        // Arrange
        var records = new[]
        {
            new FixedCustomerAligned { Name = "John", Age = 30, Country = "USA" }
        };
        var engine = new FileHelperEngine<FixedCustomerAligned>();

        // Act
        var output = engine.WriteString(records);
        var line = output.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries)[0];

        // Assert: Name is left-aligned (starts at position 0)
        await Assert.That(line.Substring(0, 4)).IsEqualTo("John");
        // Country is right-aligned (ends at position 32)
        await Assert.That(line.Substring(23, 10)).IsEqualTo("       USA");
    }

    /// <summary>
    /// Write fixed-length to file path works like original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("WriteFile fixed-length - matches original FileHelpers behavior")]
    public async Task WriteFixed_ToFile_MatchesOriginalBehavior()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_fixed_write_{Guid.NewGuid()}.txt");
        var records = new[]
        {
            new FixedCustomer { Name = "John Doe", Age = 30, Country = "USA" }
        };
        var engine = new FileHelperEngine<FixedCustomer>();

        try
        {
            // Act: Write to file
            engine.WriteFile(tempFile, records);

            // Assert: File contains exactly 33 characters per line
            var content = await File.ReadAllTextAsync(tempFile);
            var lines = content.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
            await Assert.That(lines[0].Length).IsEqualTo(33);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// WriteStream to TextWriter for fixed-length matches original FileHelpers.
    /// </summary>
    [Test]
    [DisplayName("WriteStream fixed-length - matches original FileHelpers behavior")]
    public async Task WriteFixed_ToStream_MatchesOriginalBehavior()
    {
        // Arrange
        var records = new[]
        {
            new FixedCustomer { Name = "John", Age = 30, Country = "USA" }
        };
        var engine = new FileHelperEngine<FixedCustomer>();
        var writer = new StringWriter();

        // Act
        engine.WriteStream(writer, records);

        // Assert
        var output = writer.ToString();
        var lines = output.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines[0].Length).IsEqualTo(33);
    }

    #endregion

    #region Round-Trip Tests (Read -> Write -> Read)

    /// <summary>
    /// Round-trip verification for fixed-length records.
    /// </summary>
    [Test]
    [DisplayName("Fixed-length round-trip read-write-read produces equivalent records")]
    public async Task FixedRoundTrip_ProducesEquivalentRecords()
    {
        // Arrange: Original fixed-width input
        var originalInput = "John Doe            30 USA       \r\nJane Smith          25 Canada    ";
        var readEngine = new FileHelperEngine<FixedCustomer>();
        var originalRecords = readEngine.ReadString(originalInput);

        // Act: Write records to string
        var writeEngine = new FileHelperEngine<FixedCustomer>();
        var intermediateOutput = writeEngine.WriteString(originalRecords);

        // Act: Read back from output
        var roundTripEngine = new FileHelperEngine<FixedCustomer>();
        var roundTripRecords = roundTripEngine.ReadString(intermediateOutput);

        // Assert: Round-trip produces same data
        await Assert.That(roundTripRecords.Length).IsEqualTo(originalRecords.Length);

        for (int i = 0; i < originalRecords.Length; i++)
        {
            await Assert.That(roundTripRecords[i].Name.Trim()).IsEqualTo(originalRecords[i].Name.Trim());
            await Assert.That(roundTripRecords[i].Age).IsEqualTo(originalRecords[i].Age);
            await Assert.That(roundTripRecords[i].Country.Trim()).IsEqualTo(originalRecords[i].Country.Trim());
        }
    }

    #endregion
}
