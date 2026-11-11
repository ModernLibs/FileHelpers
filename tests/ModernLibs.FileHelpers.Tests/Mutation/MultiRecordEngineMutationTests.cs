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
#pragma warning disable MA0074
#pragma warning disable MA0048

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Mutation;

[DelimitedRecord("|")]
internal sealed class HeaderRecord
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public string Batch { get; set; } = "";
}

[DelimitedRecord("|")]
internal sealed class DetailRecord
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public int Id { get; set; }
    [FieldOrder(3)] public decimal Amount { get; set; }
}

internal sealed class UnknownRecord { }

/// <summary>
/// Mutation-target tests for <see cref="MultiRecordEngine"/> (moved from the former xUnit project).
/// Covers selector dispatch, error modes, multi-type parsing, empty input,
/// and sync I/O parity.
/// </summary>
[Category("Mutation")]
public sealed class MultiRecordEngineMutationTests
{
    [Test]
    public async Task ReadString_MatchingSelectors_ParsesMixedRecords()
    {
        // Arrange: two record types with prefix-based selectors
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<DetailRecord>(line => line.StartsWith("D|", StringComparison.Ordinal)));

        // Act: read mixed content
        var records = engine.ReadString("H|BATCH1\nD|1|10.5\nH|BATCH2\nD|2|20.0");

        // Assert: all records parsed with correct types
        await Assert.That(records.Length).IsEqualTo(4);
        await Assert.That(records[0]).IsTypeOf<HeaderRecord>();
        await Assert.That(records[1]).IsTypeOf<DetailRecord>();
        await Assert.That(records[2]).IsTypeOf<HeaderRecord>();
        await Assert.That(records[3]).IsTypeOf<DetailRecord>();
        await Assert.That(((HeaderRecord)records[0]).Batch).IsEqualTo("BATCH1");
        await Assert.That(((DetailRecord)records[1]).Id).IsEqualTo(1);
    }

    [Test]
    public async Task ReadString_NoSelectorMatch_ThrowException_SkipsLine()
    {
        // Arrange: engine with narrow selector that won't match
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));
        engine.ErrorMode = ErrorMode.ThrowException;

        // Act: FileHelpers 3.x skips a line no selector matches, even in ThrowException mode
        var records = engine.ReadString("X|unknown");

        // Assert: nothing read, nothing thrown
        await Assert.That(records).IsEmpty();
    }

    [Test]
    public async Task ReadString_NoSelectorMatch_IgnoreAndContinue_SkipsLine()
    {
        // Arrange: engine with IgnoreAndContinue error mode
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));
        engine.ErrorMode = ErrorMode.IgnoreAndContinue;

        // Act: input with one matching and one non-matching line
        var records = engine.ReadString("H|BATCH\nX|unknown\nH|BATCH2");

        // Assert: non-matching line skipped, matching lines returned
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0]).IsTypeOf<HeaderRecord>();
        await Assert.That(records[1]).IsTypeOf<HeaderRecord>();
    }

    [Test]
    public async Task ReadString_NoSelectorMatch_SaveAndContinue_SkipsWithoutError()
    {
        // Arrange: engine with SaveAndContinue error mode
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));
        engine.ErrorMode = ErrorMode.SaveAndContinue;

        // Act: input with matching and non-matching lines
        var records = engine.ReadString("H|BATCH\nX|unknown\nH|BATCH2");

        // Assert: matching records returned; a selector miss is not an error (3.x)
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.Errors).IsEmpty();
    }

    [Test]
    public async Task ReadString_EmptyInput_ReturnsEmptyArray()
    {
        // Arrange: engine with valid record types
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act: empty string input
        var records = engine.ReadString("");

        // Assert: no records produced
        await Assert.That(records).IsEmpty();
    }

    [Test]
    public async Task ReadString_EmptyLinesOnly_SkipsAll()
    {
        // Arrange
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act: input with only empty lines
        var records = engine.ReadString("\n\n\n");

        // Assert: empty lines are skipped
        await Assert.That(records).IsEmpty();
    }

    [Test]
    public async Task ReadString_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert
        await Assert.That(() => engine.ReadString(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task ReadStream_NullReader_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert
        await Assert.That(() => engine.ReadStream(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task ReadStream_StringReader_ParsesMixedRecords()
    {
        // Arrange: engine with two record types
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<DetailRecord>(line => line.StartsWith("D|", StringComparison.Ordinal)));

        // Act: read via StringReader
        using var reader = new StringReader("H|B001\nD|5|99.9");
        var records = engine.ReadStream(reader);

        // Assert
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0]).IsTypeOf<HeaderRecord>();
        await Assert.That(records[1]).IsTypeOf<DetailRecord>();
    }

    [Test]
    public async Task WriteString_MixedRecords_FormatsCorrectly()
    {
        // Arrange: engine with two record types
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<DetailRecord>(line => line.StartsWith("D|", StringComparison.Ordinal)));
        object[] records =
        [
            new HeaderRecord { Kind = "H", Batch = "B001" },
            new DetailRecord { Kind = "D", Id = 5, Amount = 99.9m },
        ];

        // Act: write mixed records
        var output = engine.WriteString(records);

        // Assert: output contains both formatted records
        await Assert.That(output).Contains("H|B001");
        await Assert.That(output).Contains("D|5|99.9");
    }

    [Test]
    public async Task WriteString_UnknownRecordType_ThrowsArgumentException()
    {
        // Arrange: engine with one record type
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert: writing a non-registered type throws
        var records = new object[] { new UnknownRecord() };
        await Assert.That(() => engine.WriteString(records)).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task WriteStream_NullWriter_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert
        await Assert.That(() => engine.WriteStream(null!, Array.Empty<object>())).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task WriteStream_NullRecords_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert
        await Assert.That(() => engine.WriteStream(new StringWriter(), null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_EmptyArray_ThrowsArgumentException()
    {
        // Act & Assert: no record types provided
        await Assert.That(() => new MultiRecordEngine(Array.Empty<IMultiRecordType>())).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task ReadFile_NonexistentFile_ThrowsIOException()
    {
        // Arrange
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert: non-existent path throws IOException-derived exception
        await Assert.That(() => engine.ReadFile("/nonexistent/path/file.txt")).Throws<IOException>();
    }
}
