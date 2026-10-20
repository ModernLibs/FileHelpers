// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using System.Buffers;
using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Generator.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>
/// Integration tests for the Format body round-trip.
/// Tests verify the source-generated Format method for existing fixture types.
/// </summary>
[Category("Format")]
public sealed class FormatBodyTests
{
    [Test]
    [DisplayName("Format SimpleDelimitedRecord produces \"AAPL,10,1.23\" output")]
    public async Task Format_DelimitedRecord_ProducesCorrectOutput()
    {
        // Arrange: Create ArrayBufferWriter and format SimpleDelimitedRecord
        var writer = new ArrayBufferWriter<char>(256);
        var desc = FileHelperRegistry<SimpleDelimitedRecord>.Descriptor;
        desc.Format(new SimpleDelimitedRecord { Symbol = "AAPL", Quantity = 10, Price = 1.23m }, writer);

        // Act: Get formatted string from writer
        string result = new string(writer.WrittenSpan);

        // Assert: Output matches expected CSV format
        await Assert.That(result).IsEqualTo("AAPL,10,1.23");
    }

    [Test]
    [DisplayName("Format PriceRecord decimal uses invariant culture (1.23 not 1,23)")]
    public async Task Format_DelimitedRecord_IntField_UsesInvariantCulture()
    {
        // PriceRecord uses decimal — must format as "1.23" (not "1,23" in locale-sensitive cultures)
        // Arrange: Create ArrayBufferWriter and format PriceRecord
        var writer = new ArrayBufferWriter<char>(256);
        var desc = FileHelperRegistry<PriceRecord>.Descriptor;
        desc.Format(new PriceRecord(5, 1.23m), writer);

        // Act: Get formatted string from writer
        string result = new string(writer.WrittenSpan);

        // decimal formatted via InvariantCulture: 1.23 not 1,23
        // Assert: Decimal uses period as decimal separator
        await Assert.That(result).Contains("1.23");
    }

    [Test]
    [DisplayName("Format SimpleDelimitedRecord with Symbol=\"hello world\" copies characters to output")]
    public async Task Format_DelimitedRecord_StringField_CopiesChars()
    {
        // Arrange: Create ArrayBufferWriter and format with long string field
        var writer = new ArrayBufferWriter<char>(256);
        var desc = FileHelperRegistry<SimpleDelimitedRecord>.Descriptor;
        desc.Format(new SimpleDelimitedRecord { Symbol = "hello world", Quantity = 0, Price = 0m }, writer);

        // Act: Get formatted string from writer
        string result = new string(writer.WrittenSpan);

        // Assert: String field copied correctly with delimiter
        await Assert.That(result).StartsWith("hello world,");
    }

    [Test]
    [DisplayName("Format SimpleFixedLengthRecord pads Name field to width 10")]
    public async Task Format_FixedLengthRecord_PadsToWidth()
    {
        // SimpleFixedLengthRecord: Name=10, Code=5
        // Code=42 in width-5 field → "42   " (right-padded with spaces)
        // Arrange: Create ArrayBufferWriter and format fixed-length record
        var writer = new ArrayBufferWriter<char>(256);
        var desc = FileHelperRegistry<SimpleFixedLengthRecord>.Descriptor;
        desc.Format(new SimpleFixedLengthRecord { Name = "Alice", Code = 42 }, writer);

        // Act: Get formatted string from writer
        string result = new string(writer.WrittenSpan);

        // Total = 15 chars. Code field occupies the last 5 chars.
        // Assert: Total length 15, Code field padded with spaces
        await Assert.That(result.Length).IsEqualTo(15);
        // The Code=42 portion starts at position 10 (after the 10-char Name field)
        string codeField = result.Substring(10, 5);
        await Assert.That(codeField).StartsWith("42");
        await Assert.That(codeField.Length).IsEqualTo(5);
    }

    [Test]
    [DisplayName("Format SimpleFixedLengthRecord truncates Name longer than width 10")]
    public async Task Format_FixedLengthRecord_TruncatesLongField()
    {
        // Name field is width 10 — "ABCDEFGHIJK" (11 chars) should be truncated to 10
        // Arrange: Create ArrayBufferWriter and format with long name
        var writer = new ArrayBufferWriter<char>(256);
        var desc = FileHelperRegistry<SimpleFixedLengthRecord>.Descriptor;
        desc.Format(new SimpleFixedLengthRecord { Name = "ABCDEFGHIJK", Code = 0 }, writer);

        // Act: Get formatted string from writer
        string result = new string(writer.WrittenSpan);

        // Assert: Total length 15, Name field truncated to 10 chars
        await Assert.That(result.Length).IsEqualTo(15);
        string nameField = result.Substring(0, 10);
        await Assert.That(nameField).IsEqualTo("ABCDEFGHIJ");
    }

    [Test]
    [DisplayName("Format does not write line terminator characters")]
    public async Task Format_DoesNotWriteLineTerminator()
    {
        // Arrange: Create ArrayBufferWriter and format record
        var writer = new ArrayBufferWriter<char>(256);
        var desc = FileHelperRegistry<SimpleDelimitedRecord>.Descriptor;
        desc.Format(new SimpleDelimitedRecord { Symbol = "X", Quantity = 1, Price = 1m }, writer);

        // Act: Get formatted string from writer
        string result = new string(writer.WrittenSpan);

        // Assert: No line terminators in output
        await Assert.That(result.EndsWith('\n')).IsFalse();
        await Assert.That(result.EndsWith('\r')).IsFalse();
    }

    [Test]
    [DisplayName("Parse then Format round-trip produces equivalent output for same input")]
    public async Task Format_RoundTrip_ParseThenFormat_ProducesEquivalentOutput()
    {
        // Arrange: Parse input line, then format parsed record
        string line = "MSFT,100,42.50";
        var desc = FileHelperRegistry<SimpleDelimitedRecord>.Descriptor;
        var parsed = desc.Parse(line.AsSpan());
        var w2 = new ArrayBufferWriter<char>(256);

        // Act: Format parsed record
        desc.Format(parsed, w2);

        // Assert: Formatted output equals original input line
        await Assert.That(new string(w2.WrittenSpan)).IsEqualTo(line);
    }
}
