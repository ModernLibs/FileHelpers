// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using System.Buffers;
using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Generator;

/// <summary>
/// Tests for generator-emitted [FieldQuoted] handling.
/// Exercises Parse (strip + unescape) and Format (wrap + double) round-trip paths.
/// </summary>
[Category("Generator")]
public sealed class GeneratorFieldQuotedTests
{
    /// <summary>Format wraps quoted field values in double quotes (always-quote semantics).</summary>
    [Test]
    [DisplayName("Format wraps FieldQuoted value in double quotes")]
    public async Task Format_FieldQuoted_WrapsInDoubleQuotes()
    {
        // Arrange: Get descriptor for QuotedRecord and format with value "test"
        var writer = new ArrayBufferWriter<char>(64);
        var desc = FileHelperRegistry<QuotedRecord>.Descriptor;
        desc.Format(new QuotedRecord { Description = "test" }, writer);

        // Act: Get formatted string
        string result = new string(writer.WrittenSpan);

        // Assert: Value wrapped in double quotes
        await Assert.That(result).IsEqualTo("\"test\"");
    }

    /// <summary>Parse strips surrounding double quotes from a quoted field value.</summary>
    [Test]
    [DisplayName("Parse strips outer double quotes from FieldQuoted value")]
    public async Task Parse_FieldQuoted_RemovesOuterQuotes()
    {
        // Arrange: Get descriptor for QuotedRecord
        var desc = FileHelperRegistry<QuotedRecord>.Descriptor;

        // Act: Parse input with quoted value
        var record = desc.Parse("\"Hello World\"".AsSpan());

        // Assert: Outer quotes removed, result is "Hello World"
        await Assert.That(record.Description).IsEqualTo("Hello World");
    }

    /// <summary>Format escapes internal quotes by doubling (e.g., " becomes "").</summary>
    [Test]
    [DisplayName("Format escapes internal quotes by doubling in FieldQuoted value")]
    public async Task Format_FieldQuoted_EscapesInternalQuotes()
    {
        // Arrange: Get descriptor for QuotedRecord and format with internal quote
        var writer = new ArrayBufferWriter<char>(64);
        var desc = FileHelperRegistry<QuotedRecord>.Descriptor;
        desc.Format(new QuotedRecord { Description = "He said \"hi\"" }, writer);

        // Act: Get formatted string
        string result = new string(writer.WrittenSpan);

        // Expected: a leading quote, the body with doubled internal quotes, a trailing quote.
        // Assert: Internal quotes doubled, wrapped in outer quotes
        await Assert.That(result).IsEqualTo("\"He said \"\"hi\"\"\"");
    }

    /// <summary>Parse unescapes doubled internal quotes back to a single quote.</summary>
    [Test]
    [DisplayName("Parse unescapes doubled quotes to single quote in FieldQuoted value")]
    public async Task Parse_FieldQuoted_UnescapesDoubleQuotes()
    {
        // Arrange: Get descriptor for QuotedRecord
        var desc = FileHelperRegistry<QuotedRecord>.Descriptor;

        // Act: Parse input with escaped quotes
        var record = desc.Parse("\"He said \"\"hi\"\"\"".AsSpan());

        // Assert: Doubled quotes unescaped to single quotes
        await Assert.That(record.Description).IsEqualTo("He said \"hi\"");
    }

    /// <summary>
    /// Embedded delimiters inside a [FieldQuoted] payload do not terminate the
    /// field. The parser must track "inside quotes" state and scan past commas that
    /// appear inside the quoted span.
    /// </summary>
    [Test]
    [DisplayName("Parse treats delimiter inside quotes as literal content, not field separator")]
    public async Task Parse_FieldQuoted_DelimiterInsideQuotesIsNotASplit()
    {
        // Arrange: Get descriptor for QuotedWithTrailing
        var desc = FileHelperRegistry<QuotedWithTrailing>.Descriptor;

        // Act: Parse input with comma inside quoted field followed by unquoted field
        var record = desc.Parse("\"Hello, World\",42".AsSpan());

        // Assert: Comma inside quotes preserved, Count field parsed correctly
        await Assert.That(record.Description).IsEqualTo("Hello, World");
        await Assert.That(record.Count).IsEqualTo(42);
    }

    /// <summary>
    /// Doubled quotes inside a quoted span keep the parser "inside quotes" so an
    /// adjacent delimiter is still treated as literal content until the outer quote closes.
    /// </summary>
    [Test]
    [DisplayName("Parse handles doubled quotes and delimiter inside quoted field correctly")]
    public async Task Parse_FieldQuoted_DoubledQuotesInsideDoNotFlipQuoteState()
    {
        // Arrange: Get descriptor for QuotedWithTrailing
        var desc = FileHelperRegistry<QuotedWithTrailing>.Descriptor;

        // Act: Parse input with doubled quotes and comma inside quoted field
        var record = desc.Parse("\"He said \"\"hi, friend\"\"\",7".AsSpan());

        // Assert: Doubled quotes and comma inside correctly parsed
        await Assert.That(record.Description).IsEqualTo("He said \"hi, friend\"");
        await Assert.That(record.Count).IsEqualTo(7);
    }

    /// <summary>
    /// Write a record whose quoted field contains the record's own delimiter,
    /// then read it back with the same engine. Guards against regressions in the write-then-read
    /// quote-aware path.
    /// </summary>
    [Test]
    [DisplayName("Round-trip preserves quoted field containing delimiter across write and read")]
    public async Task RoundTrip_FieldQuoted_EmbeddedDelimiter()
    {
        // Arrange: Get descriptor and format record with comma in quoted field
        var desc = FileHelperRegistry<QuotedWithTrailing>.Descriptor;
        var writer = new ArrayBufferWriter<char>(64);
        desc.Format(new QuotedWithTrailing { Description = "a, b, c", Count = 99 }, writer);

        // Act: Get serialized string and parse back
        string serialized = new string(writer.WrittenSpan);

        // Assert: Serialized correctly with comma in quotes, then parsed correctly
        await Assert.That(serialized).IsEqualTo("\"a, b, c\",99");
        var record = desc.Parse(serialized.AsSpan());
        await Assert.That(record.Description).IsEqualTo("a, b, c");
        await Assert.That(record.Count).IsEqualTo(99);
    }
}
