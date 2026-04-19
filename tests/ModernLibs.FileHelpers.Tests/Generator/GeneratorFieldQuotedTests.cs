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
/// GREEN tests for generator-emitted [FieldQuoted] handling (D-06).
/// Exercises Parse (strip + unescape) and Format (wrap + double) round-trip paths.
/// </summary>
[Category("Phase07")]
[Category("Generator")]
public sealed class GeneratorFieldQuotedTests
{
    /// <summary>D-06: Format wraps quoted field values in double quotes (always-quote semantics).</summary>
    [Test]
    public async Task Format_FieldQuoted_WrapsInDoubleQuotes()
    {
        var writer = new ArrayBufferWriter<char>(64);
        var desc = FileHelperRegistry<QuotedRecord>.Descriptor;
        desc.Format(new QuotedRecord { Description = "test" }, writer);
        string result = new string(writer.WrittenSpan);
        await Assert.That(result).IsEqualTo("\"test\"");
    }

    /// <summary>D-06: Parse strips surrounding double quotes from a quoted field value.</summary>
    [Test]
    public async Task Parse_FieldQuoted_RemovesOuterQuotes()
    {
        var desc = FileHelperRegistry<QuotedRecord>.Descriptor;
        var record = desc.Parse("\"Hello World\"".AsSpan());
        await Assert.That(record.Description).IsEqualTo("Hello World");
    }

    /// <summary>D-06: Format escapes internal quotes by doubling (e.g., " becomes "").</summary>
    [Test]
    public async Task Format_FieldQuoted_EscapesInternalQuotes()
    {
        var writer = new ArrayBufferWriter<char>(64);
        var desc = FileHelperRegistry<QuotedRecord>.Descriptor;
        desc.Format(new QuotedRecord { Description = "He said \"hi\"" }, writer);
        string result = new string(writer.WrittenSpan);
        // Expected: a leading quote, the body with doubled internal quotes, a trailing quote.
        await Assert.That(result).IsEqualTo("\"He said \"\"hi\"\"\"");
    }

    /// <summary>D-06: Parse unescapes doubled internal quotes back to a single quote.</summary>
    [Test]
    public async Task Parse_FieldQuoted_UnescapesDoubleQuotes()
    {
        var desc = FileHelperRegistry<QuotedRecord>.Descriptor;
        var record = desc.Parse("\"He said \"\"hi\"\"\"".AsSpan());
        await Assert.That(record.Description).IsEqualTo("He said \"hi\"");
    }

    /// <summary>
    /// WR-04: embedded delimiters inside a [FieldQuoted] payload do not terminate the
    /// field. The parser must track "inside quotes" state and scan past commas that
    /// appear inside the quoted span.
    /// </summary>
    [Test]
    public async Task Parse_FieldQuoted_DelimiterInsideQuotesIsNotASplit()
    {
        var desc = FileHelperRegistry<QuotedWithTrailing>.Descriptor;
        var record = desc.Parse("\"Hello, World\",42".AsSpan());
        await Assert.That(record.Description).IsEqualTo("Hello, World");
        await Assert.That(record.Count).IsEqualTo(42);
    }

    /// <summary>
    /// WR-04: doubled quotes inside a quoted span keep the parser "inside quotes" so an
    /// adjacent delimiter is still treated as literal content until the outer quote closes.
    /// </summary>
    [Test]
    public async Task Parse_FieldQuoted_DoubledQuotesInsideDoNotFlipQuoteState()
    {
        var desc = FileHelperRegistry<QuotedWithTrailing>.Descriptor;
        var record = desc.Parse("\"He said \"\"hi, friend\"\"\",7".AsSpan());
        await Assert.That(record.Description).IsEqualTo("He said \"hi, friend\"");
        await Assert.That(record.Count).IsEqualTo(7);
    }

    /// <summary>
    /// WR-04 round-trip: write a record whose quoted field contains the record's own delimiter,
    /// then read it back with the same engine. Guards against regressions in the write-then-read
    /// quote-aware path.
    /// </summary>
    [Test]
    public async Task RoundTrip_FieldQuoted_EmbeddedDelimiter()
    {
        var desc = FileHelperRegistry<QuotedWithTrailing>.Descriptor;
        var writer = new ArrayBufferWriter<char>(64);
        desc.Format(new QuotedWithTrailing { Description = "a, b, c", Count = 99 }, writer);
        string serialized = new string(writer.WrittenSpan);
        await Assert.That(serialized).IsEqualTo("\"a, b, c\",99");
        var record = desc.Parse(serialized.AsSpan());
        await Assert.That(record.Description).IsEqualTo("a, b, c");
        await Assert.That(record.Count).IsEqualTo(99);
    }
}
