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
/// Mutation-target tests for <see cref="MasterDetailEngine{TMaster,TDetail}"/> (moved from the former xUnit project).
/// Covers classification, multi-line buffering, error modes, I/O parity,
/// write path, and empty input.
/// </summary>
[Category("Mutation")]
public sealed class MasterDetailEngineMutationTests
{
    [Test]
    public async Task ReadString_ClassificationCallback_GroupsDetailsUnderMaster()
    {
        // Arrange: prefix-based classification
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: one master with two details
        var groups = engine.ReadString("M|A\nD|A|10\nD|A|20");

        // Assert: details grouped under master
        await Assert.That(groups).HasSingleItem();
        await Assert.That(groups[0].Master.Id).IsEqualTo("A");
        await Assert.That(groups[0].Details.Count).IsEqualTo(2);
        await Assert.That(groups[0].Details[0].Quantity).IsEqualTo(10);
        await Assert.That(groups[0].Details[1].Quantity).IsEqualTo(20);
    }

    [Test]
    public async Task ReadString_MultiLineDetail_BuffersUnclosedQuotes()
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
        var ex = await Assert.That(() => engine.ReadString("M|A\nD|A|\"hello\nworld\"")).ThrowsExactly<ConvertException>();

        // Assert: ConvertException (a failed conversion is never silently defaulted for the int field) confirms the
        // buffering + parse path was exercised
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task ReadString_UnknownLine_Skipped()
    {
        // Arrange: selector returns Skip for non-matching lines
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: input with unknown lines interspersed
        var groups = engine.ReadString("X|unknown\nM|A\nX|skip\nD|A|5\nX|skip2");

        // Assert: unknown lines skipped, valid records parsed
        await Assert.That(groups).HasSingleItem();
        await Assert.That(groups[0].Master.Id).IsEqualTo("A");
        await Assert.That(groups[0].Details).HasSingleItem();
    }

    [Test]
    public async Task ReadString_MultipleMasters_CreatesMultipleGroups()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: two masters with their details
        var groups = engine.ReadString("M|A\nD|A|1\nM|B\nD|B|2\nD|B|3");

        // Assert: two groups created
        await Assert.That(groups.Length).IsEqualTo(2);
        await Assert.That(groups[0].Master.Id).IsEqualTo("A");
        await Assert.That(groups[0].Details).HasSingleItem();
        await Assert.That(groups[1].Master.Id).IsEqualTo("B");
        await Assert.That(groups[1].Details.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ReadString_EmptyInput_ReturnsEmptyArray()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);

        // Act: empty string
        var groups = engine.ReadString("");

        // Assert
        await Assert.That(groups).IsEmpty();
    }

    [Test]
    public async Task ReadString_EmptyLinesOnly_ReturnsEmptyArray()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            RecordAction.Skip);

        // Act: only empty lines
        var groups = engine.ReadString("\n\n\n");

        // Assert
        await Assert.That(groups).IsEmpty();
    }

    [Test]
    public async Task ReadString_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);

        // Act & Assert
        await Assert.That(() => engine.ReadString(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task ReadStream_NullReader_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);

        // Act & Assert
        await Assert.That(() => engine.ReadStream(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task ReadStream_StringReader_ParsesGroups()
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
        await Assert.That(groups).HasSingleItem();
        await Assert.That(groups[0].Master.Id).IsEqualTo("A");
        await Assert.That(groups[0].Details[0].Quantity).IsEqualTo(42);
    }

    [Test]
    public async Task WriteString_MasterDetailGroups_FormatsCorrectly()
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
        await Assert.That(output).Contains("M|A");
        await Assert.That(output).Contains("D|A|10");
        await Assert.That(output).Contains("D|A|20");
    }

    [Test]
    public async Task WriteStream_NullWriter_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);

        // Act & Assert
        await Assert.That(() => engine.WriteStream(null!, Array.Empty<MasterDetails<MasterRec, DetailRec>>())).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task WriteStream_NullGroups_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);

        // Act & Assert
        await Assert.That(() => engine.WriteStream(new StringWriter(), null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task ReadString_DetailBeforeMaster_ThrowsBadUsageException()
    {
        // Arrange: detail appears before any master
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act & Assert: detail without preceding master throws
        await Assert.That(() => engine.ReadString("D|X|5")).ThrowsExactly<BadUsageException>();
    }

    [Test]
    public async Task ReadString_NullSelector_DoesNotThrowAtConstruction()
    {
        // Act: null selector is accepted at construction (deferred validation)
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(null!);

        // Assert: engine created, null reference will surface during parsing
        await Assert.That(engine).IsNotNull();
    }

    [Test]
    public async Task ReadFile_NonexistentFile_ThrowsIOException()
    {
        // Arrange
        var engine = new MasterDetailEngine<MasterRec, DetailRec>(_ => RecordAction.Skip);

        // Act & Assert: non-existent path throws IOException-derived exception
        await Assert.That(() => engine.ReadFile("/nonexistent/path/file.txt")).Throws<IOException>();
    }
}
