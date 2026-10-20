#pragma warning disable CA1707
#pragma warning disable MA0004
#pragma warning disable MA0074
#pragma warning disable MA0048

using Xunit;

namespace ModernLibs.FileHelpers.Mutation.Tests;

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
/// xUnit mutation-target tests for <see cref="MultiRecordEngine"/>.
/// Covers selector dispatch, error modes, multi-type parsing, empty input,
/// and sync I/O parity.
/// </summary>
public sealed class MultiRecordEngineMutationTests
{
    [Fact]
    public void ReadString_MatchingSelectors_ParsesMixedRecords()
    {
        // Arrange: two record types with prefix-based selectors
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<DetailRecord>(line => line.StartsWith("D|", StringComparison.Ordinal)));

        // Act: read mixed content
        var records = engine.ReadString("H|BATCH1\nD|1|10.5\nH|BATCH2\nD|2|20.0");

        // Assert: all records parsed with correct types
        Assert.Equal(4, records.Length);
        Assert.IsType<HeaderRecord>(records[0]);
        Assert.IsType<DetailRecord>(records[1]);
        Assert.IsType<HeaderRecord>(records[2]);
        Assert.IsType<DetailRecord>(records[3]);
        Assert.Equal("BATCH1", ((HeaderRecord)records[0]).Batch);
        Assert.Equal(1, ((DetailRecord)records[1]).Id);
    }

    [Fact]
    public void ReadString_NoSelectorMatch_ThrowException_SkipsLine()
    {
        // Arrange: engine with narrow selector that won't match
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));
        engine.ErrorMode = ErrorMode.ThrowException;

        // Act: FileHelpers 3.x skips a line no selector matches, even in ThrowException mode
        var records = engine.ReadString("X|unknown");

        // Assert: nothing read, nothing thrown
        Assert.Empty(records);
    }

    [Fact]
    public void ReadString_NoSelectorMatch_IgnoreAndContinue_SkipsLine()
    {
        // Arrange: engine with IgnoreAndContinue error mode
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));
        engine.ErrorMode = ErrorMode.IgnoreAndContinue;

        // Act: input with one matching and one non-matching line
        var records = engine.ReadString("H|BATCH\nX|unknown\nH|BATCH2");

        // Assert: non-matching line skipped, matching lines returned
        Assert.Equal(2, records.Length);
        Assert.IsType<HeaderRecord>(records[0]);
        Assert.IsType<HeaderRecord>(records[1]);
    }

    [Fact]
    public void ReadString_NoSelectorMatch_SaveAndContinue_SkipsWithoutError()
    {
        // Arrange: engine with SaveAndContinue error mode
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));
        engine.ErrorMode = ErrorMode.SaveAndContinue;

        // Act: input with matching and non-matching lines
        var records = engine.ReadString("H|BATCH\nX|unknown\nH|BATCH2");

        // Assert: matching records returned; a selector miss is not an error (3.x)
        Assert.Equal(2, records.Length);
        Assert.Empty(engine.ErrorManager.Errors);
    }

    [Fact]
    public void ReadString_EmptyInput_ReturnsEmptyArray()
    {
        // Arrange: engine with valid record types
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act: empty string input
        var records = engine.ReadString("");

        // Assert: no records produced
        Assert.Empty(records);
    }

    [Fact]
    public void ReadString_EmptyLinesOnly_SkipsAll()
    {
        // Arrange
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act: input with only empty lines
        var records = engine.ReadString("\n\n\n");

        // Assert: empty lines are skipped
        Assert.Empty(records);
    }

    [Fact]
    public void ReadString_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => engine.ReadString(null!));
    }

    [Fact]
    public void ReadStream_NullReader_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => engine.ReadStream(null!));
    }

    [Fact]
    public void ReadStream_StringReader_ParsesMixedRecords()
    {
        // Arrange: engine with two record types
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<DetailRecord>(line => line.StartsWith("D|", StringComparison.Ordinal)));

        // Act: read via StringReader
        using var reader = new StringReader("H|B001\nD|5|99.9");
        var records = engine.ReadStream(reader);

        // Assert
        Assert.Equal(2, records.Length);
        Assert.IsType<HeaderRecord>(records[0]);
        Assert.IsType<DetailRecord>(records[1]);
    }

    [Fact]
    public void WriteString_MixedRecords_FormatsCorrectly()
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
        Assert.Contains("H|B001", output);
        Assert.Contains("D|5|99.9", output);
    }

    [Fact]
    public void WriteString_UnknownRecordType_ThrowsArgumentException()
    {
        // Arrange: engine with one record type
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert: writing a non-registered type throws
        var records = new object[] { new UnknownRecord() };
        Assert.Throws<ArgumentException>(() => engine.WriteString(records));
    }

    [Fact]
    public void WriteStream_NullWriter_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => engine.WriteStream(null!, Array.Empty<object>()));
    }

    [Fact]
    public void WriteStream_NullRecords_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => engine.WriteStream(new StringWriter(), null!));
    }

    [Fact]
    public void Constructor_EmptyArray_ThrowsArgumentException()
    {
        // Act & Assert: no record types provided
        Assert.Throws<ArgumentException>(() => new MultiRecordEngine(Array.Empty<IMultiRecordType>()));
    }

    [Fact]
    public void ReadFile_NonexistentFile_ThrowsIOException()
    {
        // Arrange
        var engine = new MultiRecordEngine(
            MultiRecordType.For<HeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)));

        // Act & Assert: non-existent path throws IOException-derived exception
        Assert.ThrowsAny<IOException>(() => engine.ReadFile("/nonexistent/path/file.txt"));
    }
}
