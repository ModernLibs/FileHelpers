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
/// Integration tests for PIPE-09 (Format body round-trip).
/// Tests verify the source-generated Format method for existing fixture types.
/// Format body wired in Wave 3 (06-05-PLAN.md).
/// </summary>
[Category("Phase06")]
[Category("Format")]
public sealed class FormatBodyTests
{
    [Test]
    public async Task Format_DelimitedRecord_ProducesCorrectOutput()
    {
        var writer = new ArrayBufferWriter<char>(256);
        var desc = FileHelperRegistry<SimpleDelimitedRecord>.Descriptor;
        desc.Format(new SimpleDelimitedRecord { Symbol = "AAPL", Quantity = 10, Price = 1.23m }, writer);
        string result = new string(writer.WrittenSpan);
        await Assert.That(result).IsEqualTo("AAPL,10,1.23");
    }

    [Test]
    public async Task Format_DelimitedRecord_IntField_UsesInvariantCulture()
    {
        // PriceRecord uses decimal — must format as "1.23" (not "1,23" in locale-sensitive cultures)
        var writer = new ArrayBufferWriter<char>(256);
        var desc = FileHelperRegistry<PriceRecord>.Descriptor;
        desc.Format(new PriceRecord(5, 1.23m), writer);
        string result = new string(writer.WrittenSpan);
        // decimal formatted via InvariantCulture: 1.23 not 1,23
        await Assert.That(result).Contains("1.23");
    }

    [Test]
    public async Task Format_DelimitedRecord_StringField_CopiesChars()
    {
        var writer = new ArrayBufferWriter<char>(256);
        var desc = FileHelperRegistry<SimpleDelimitedRecord>.Descriptor;
        desc.Format(new SimpleDelimitedRecord { Symbol = "hello world", Quantity = 0, Price = 0m }, writer);
        string result = new string(writer.WrittenSpan);
        await Assert.That(result).StartsWith("hello world,");
    }

    [Test]
    public async Task Format_FixedLengthRecord_PadsToWidth()
    {
        // SimpleFixedLengthRecord: Name=10, Code=5
        // Code=42 in width-5 field → "42   " (right-padded with spaces)
        var writer = new ArrayBufferWriter<char>(256);
        var desc = FileHelperRegistry<SimpleFixedLengthRecord>.Descriptor;
        desc.Format(new SimpleFixedLengthRecord { Name = "Alice", Code = 42 }, writer);
        string result = new string(writer.WrittenSpan);
        // Total = 15 chars. Code field occupies the last 5 chars.
        await Assert.That(result.Length).IsEqualTo(15);
        // The Code=42 portion starts at position 10 (after the 10-char Name field)
        string codeField = result.Substring(10, 5);
        await Assert.That(codeField).StartsWith("42");
        await Assert.That(codeField.Length).IsEqualTo(5);
    }

    [Test]
    public async Task Format_FixedLengthRecord_TruncatesLongField()
    {
        // Name field is width 10 — "ABCDEFGHIJK" (11 chars) should be truncated to 10
        var writer = new ArrayBufferWriter<char>(256);
        var desc = FileHelperRegistry<SimpleFixedLengthRecord>.Descriptor;
        desc.Format(new SimpleFixedLengthRecord { Name = "ABCDEFGHIJK", Code = 0 }, writer);
        string result = new string(writer.WrittenSpan);
        await Assert.That(result.Length).IsEqualTo(15);
        string nameField = result.Substring(0, 10);
        await Assert.That(nameField).IsEqualTo("ABCDEFGHIJ");
    }

    [Test]
    public async Task Format_DoesNotWriteLineTerminator()
    {
        var writer = new ArrayBufferWriter<char>(256);
        var desc = FileHelperRegistry<SimpleDelimitedRecord>.Descriptor;
        desc.Format(new SimpleDelimitedRecord { Symbol = "X", Quantity = 1, Price = 1m }, writer);
        string result = new string(writer.WrittenSpan);
        await Assert.That(result.EndsWith('\n')).IsFalse();
        await Assert.That(result.EndsWith('\r')).IsFalse();
    }

    [Test]
    public async Task Format_RoundTrip_ParseThenFormat_ProducesEquivalentOutput()
    {
        string line = "MSFT,100,42.50";
        var desc = FileHelperRegistry<SimpleDelimitedRecord>.Descriptor;
        var parsed = desc.Parse(line.AsSpan());
        var w2 = new ArrayBufferWriter<char>(256);
        desc.Format(parsed, w2);
        await Assert.That(new string(w2.WrittenSpan)).IsEqualTo(line);
    }
}
