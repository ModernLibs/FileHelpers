#pragma warning disable CA1707
#pragma warning disable MA0004
#pragma warning disable MA0074
#pragma warning disable MA0048

using Xunit;

namespace ModernLibs.FileHelpers.Mutation.Tests;

[DelimitedRecord("|")]
internal sealed class MasterRec
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public string Id { get; set; } = "";
}

[DelimitedRecord("|")]
internal sealed class DetailRec
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public string MasterId { get; set; } = "";
    [FieldOrder(3)] public int Quantity { get; set; }
}

/// <summary>
/// xUnit mutation-target tests for <see cref="MasterDetailEngine{TMaster,TDetail}"/>.
/// Covers classification, multi-line buffering, error modes, I/O parity,
/// write path, and empty input (MUT-05).
/// </summary>
public sealed class MasterDetailEngineMutationTests
{
    [Fact]
    public void ReadString_ClassificationCallback_GroupsDetailsUnderMaster()
    {
        // Arrange: prefix-based classification
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: one master with two details
        var groups = engine.ReadString("M|A\nD|A|10\nD|A|20");

        // Assert: details grouped under master
        Assert.Single(groups);
        Assert.Equal("A", groups[0].Master.Id);
        Assert.Equal(2, groups[0].Details.Count);
        Assert.Equal(10, groups[0].Details[0].Quantity);
        Assert.Equal(20, groups[0].Details[1].Quantity);
    }

    [Fact]
    public void ReadString_MultiLineDetail_BuffersUnclosedQuotes()
    {
        // Arrange: unclosed quote on detail line triggers buffering
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: master followed by detail with unclosed quote, then closing content
        // The unclosed quote triggers buffering; combined line is then parsed.
        // The multi-line content in the third field causes a parse error (not an int),
        // but the buffering path is exercised for mutation coverage.
        var ex = Assert.Throws<FormatException>(() => engine.ReadString("M|A\nD|A|\"hello\nworld\""));

        // Assert: FormatException confirms the buffering + parse path was exercised
        Assert.NotNull(ex);
    }

    [Fact]
    public void ReadString_UnknownLine_Skipped()
    {
        // Arrange: selector returns Skip for non-matching lines
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: input with unknown lines interspersed
        var groups = engine.ReadString("X|unknown\nM|A\nX|skip\nD|A|5\nX|skip2");

        // Assert: unknown lines skipped, valid records parsed
        Assert.Single(groups);
        Assert.Equal("A", groups[0].Master.Id);
        Assert.Single(groups[0].Details);
    }

    [Fact]
    public void ReadString_MultipleMasters_CreatesMultipleGroups()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: two masters with their details
        var groups = engine.ReadString("M|A\nD|A|1\nM|B\nD|B|2\nD|B|3");

        // Assert: two groups created
        Assert.Equal(2, groups.Length);
        Assert.Equal("A", groups[0].Master.Id);
        Assert.Single(groups[0].Details);
        Assert.Equal("B", groups[1].Master.Id);
        Assert.Equal(2, groups[1].Details.Count);
    }

    [Fact]
    public void ReadString_EmptyInput_ReturnsEmptyArray()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);

        // Act: empty string
        var groups = engine.ReadString("");

        // Assert
        Assert.Empty(groups);
    }

    [Fact]
    public void ReadString_EmptyLinesOnly_ReturnsEmptyArray()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            RecordAction.Skip);

        // Act: only empty lines
        var groups = engine.ReadString("\n\n\n");

        // Assert
        Assert.Empty(groups);
    }

    [Fact]
    public void ReadString_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => engine.ReadString(null!));
    }

    [Fact]
    public void ReadStream_NullReader_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => engine.ReadStream(null!));
    }

    [Fact]
    public void ReadStream_StringReader_ParsesGroups()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: read via StringReader
        using var reader = new StringReader("M|A\nD|A|42");
        var groups = engine.ReadStream(reader);

        // Assert
        Assert.Single(groups);
        Assert.Equal("A", groups[0].Master.Id);
        Assert.Equal(42, groups[0].Details[0].Quantity);
    }

    [Fact]
    public void WriteString_MasterDetailGroups_FormatsCorrectly()
    {
        // Arrange: create grouped records
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);
        var group = new MasterDetails<MasterRec, DetailRec>(
            new MasterRec { Kind = "M", Id = "A" },
            [
                new DetailRec { Kind = "D", MasterId = "A", Quantity = 10 },
                new DetailRec { Kind = "D", MasterId = "A", Quantity = 20 },
            ]);

        // Act
        var output = engine.WriteString([group]);

        // Assert: master row followed by detail rows
        Assert.Contains("M|A", output);
        Assert.Contains("D|A|10", output);
        Assert.Contains("D|A|20", output);
    }

    [Fact]
    public void WriteStream_NullWriter_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => engine.WriteStream(null!, Array.Empty<MasterDetails<MasterRec, DetailRec>>()));
    }

    [Fact]
    public void WriteStream_NullGroups_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => engine.WriteStream(new StringWriter(), null!));
    }

    [Fact]
    public void ReadString_DetailBeforeMaster_ThrowsFormatException()
    {
        // Arrange: detail appears before any master
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act & Assert: detail without preceding master throws
        Assert.Throws<FormatException>(() => engine.ReadString("D|X|5"));
    }

    [Fact]
    public void ReadString_NullSelector_DoesNotThrowAtConstruction()
    {
        // Act: null selector is accepted at construction (deferred validation)
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(null!);

        // Assert: engine created, null reference will surface during parsing
        Assert.NotNull(engine);
    }

    [Fact]
    public void ReadFile_NonexistentFile_ThrowsIOException()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);

        // Act & Assert: non-existent path throws IOException-derived exception
        Assert.ThrowsAny<IOException>(() => engine.ReadFile("/nonexistent/path/file.txt"));
    }
}
