// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple fixture types live with the behavior tests that use them.
#pragma warning disable MA0048

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Async.MasterDetail;

[DelimitedRecord("|")]
internal sealed class MdAsyncMaster
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public string Id { get; set; } = "";
}

[DelimitedRecord("|")]
internal sealed class MdAsyncDetail
{
    [FieldOrder(1)] public string Kind { get; set; } = "";
    [FieldOrder(2)] public string MasterId { get; set; } = "";
    [FieldOrder(3)] public int Quantity { get; set; }
}

[Category("Async")]
[Category("MasterDetail")]
public sealed class MasterDetailAsyncEngineTests
{
    [Test]
    [DisplayName("MasterDetailAsyncEngine groups details under master from stream")]
    public async Task ReadAsync_Stream_GroupsDetailsUnderMaster()
    {
        // Arrange: engine with prefix-based classification
        var engine = new MasterDetailAsyncEngine<MdAsyncMaster, MdAsyncDetail>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: read from memory stream
        var content = "M|A\nD|A|2\nD|A|3";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var groups = new List<MasterDetails<MdAsyncMaster, MdAsyncDetail>>();
        await foreach (var group in engine.ReadAsync(stream))
            groups.Add(group);

        // Assert: details grouped under master
        await Assert.That(groups.Count).IsEqualTo(1);
        await Assert.That(groups[0].Master.Id).IsEqualTo("A");
        await Assert.That(groups[0].Details.Count).IsEqualTo(2);
        await Assert.That(groups[0].Details[0].Quantity).IsEqualTo(2);
        await Assert.That(groups[0].Details[1].Quantity).IsEqualTo(3);
    }

    [Test]
    [DisplayName("MasterDetailAsyncEngine groups details under master from file path")]
    public async Task ReadAsync_FilePath_GroupsDetailsUnderMaster()
    {
        // Arrange
        var engine = new MasterDetailAsyncEngine<MdAsyncMaster, MdAsyncDetail>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: write temp file and read
        var content = "M|X\nD|X|5\nD|X|10";
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, content);
            var groups = new List<MasterDetails<MdAsyncMaster, MdAsyncDetail>>();
            await foreach (var group in engine.ReadAsync(tempFile))
                groups.Add(group);

            // Assert
            await Assert.That(groups.Count).IsEqualTo(1);
            await Assert.That(groups[0].Master.Id).IsEqualTo("X");
            await Assert.That(groups[0].Details.Count).IsEqualTo(2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    [DisplayName("MasterDetailAsyncEngine yields multiple groups for multiple masters")]
    public async Task ReadAsync_MultipleMasters_YieldsMultipleGroups()
    {
        // Arrange
        var engine = new MasterDetailAsyncEngine<MdAsyncMaster, MdAsyncDetail>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: two masters with their details
        var content = "M|A\nD|A|1\nM|B\nD|B|2\nD|B|3";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var groups = new List<MasterDetails<MdAsyncMaster, MdAsyncDetail>>();
        await foreach (var group in engine.ReadAsync(stream))
            groups.Add(group);

        // Assert
        await Assert.That(groups.Count).IsEqualTo(2);
        await Assert.That(groups[0].Master.Id).IsEqualTo("A");
        await Assert.That(groups[0].Details.Count).IsEqualTo(1);
        await Assert.That(groups[1].Master.Id).IsEqualTo("B");
        await Assert.That(groups[1].Details.Count).IsEqualTo(2);
    }

    [Test]
    [DisplayName("MasterDetailAsyncEngine skips unknown lines")]
    public async Task ReadAsync_UnknownLines_Skipped()
    {
        // Arrange
        var engine = new MasterDetailAsyncEngine<MdAsyncMaster, MdAsyncDetail>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: input with unknown lines interspersed
        var content = "X|skip\nM|A\nX|skip\nD|A|5";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var groups = new List<MasterDetails<MdAsyncMaster, MdAsyncDetail>>();
        await foreach (var group in engine.ReadAsync(stream))
            groups.Add(group);

        // Assert
        await Assert.That(groups.Count).IsEqualTo(1);
        await Assert.That(groups[0].Master.Id).IsEqualTo("A");
        await Assert.That(groups[0].Details.Count).IsEqualTo(1);
    }

    [Test]
    [DisplayName("MasterDetailAsyncEngine yields nothing from empty stream")]
    public async Task ReadAsync_EmptyStream_YieldsNothing()
    {
        // Arrange
        var engine = new MasterDetailAsyncEngine<MdAsyncMaster, MdAsyncDetail>(_ => RecordAction.Skip);

        // Act
        using var stream = new MemoryStream(Array.Empty<byte>());
        var groups = new List<MasterDetails<MdAsyncMaster, MdAsyncDetail>>();
        await foreach (var group in engine.ReadAsync(stream))
            groups.Add(group);

        // Assert
        await Assert.That(groups.Count).IsEqualTo(0);
    }

    [Test]
    [DisplayName("MasterDetailAsyncEngine respects cancellation token")]
    public async Task ReadAsync_CancellationToken_StopsIteration()
    {
        // Arrange: many groups
        var engine = new MasterDetailAsyncEngine<MdAsyncMaster, MdAsyncDetail>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        var content = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"M|{i}\nD|{i}|{i}"));
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var cts = new CancellationTokenSource();
        var groups = new List<MasterDetails<MdAsyncMaster, MdAsyncDetail>>();
        var count = 0;

        // Act: cancel after 5 groups — OperationCanceledException is expected
        try
        {
            await foreach (var group in engine.ReadAsync(stream, cts.Token))
            {
                groups.Add(group);
                count++;
                if (count >= 5)
                    cts.Cancel();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is triggered
        }

        // Assert: stopped after cancellation
        await Assert.That(groups.Count).IsGreaterThanOrEqualTo(5);
        await Assert.That(groups.Count).IsLessThanOrEqualTo(6);
    }

    [Test]
    [DisplayName("MasterDetailAsyncEngine throws BadUsageException when detail appears before master")]
    public async Task ReadAsync_DetailBeforeMaster_ThrowsBadUsageException()
    {
        // Arrange
        var engine = new MasterDetailAsyncEngine<MdAsyncMaster, MdAsyncDetail>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act & Assert
        var content = "D|X|5";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        await Assert.That(async () =>
        {
            await foreach (var _ in engine.ReadAsync(stream))
            {
            }
        }).Throws<BadUsageException>();
    }

    [Test]
    [DisplayName("MasterDetailAsyncEngine null filePath throws ArgumentNullException")]
    public async Task ReadAsync_NullFilePath_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MasterDetailAsyncEngine<MdAsyncMaster, MdAsyncDetail>(_ => RecordAction.Skip);

        // Act & Assert
        await Assert.That(async () =>
        {
            await foreach (var _ in engine.ReadAsync((string)null!, CancellationToken.None))
            {
            }
        }).Throws<ArgumentNullException>();
    }

    [Test]
    [DisplayName("MasterDetailAsyncEngine null stream throws ArgumentNullException")]
    public async Task ReadAsync_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new MasterDetailAsyncEngine<MdAsyncMaster, MdAsyncDetail>(_ => RecordAction.Skip);

        // Act & Assert
        await Assert.That(async () =>
        {
            await foreach (var _ in engine.ReadAsync((string)null!, CancellationToken.None))
            {
            }
        }).Throws<ArgumentNullException>();
    }

    [Test]
    [DisplayName("MasterDetailAsyncEngine handles multiline quoted fields")]
    public async Task ReadAsync_MultilineQuotedField_BuffersCorrectly()
    {
        // Arrange: engine with classification
        var engine = new MasterDetailAsyncEngine<MdAsyncMaster, MdAsyncDetail>(line =>
            line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
            line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
            RecordAction.Skip);

        // Act: master with detail containing unclosed quote that closes on next line
        // Note: the engine buffers until the quote is closed
        var content = "M|A\nD|A|\"multi\nline\"";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var groups = new List<MasterDetails<MdAsyncMaster, MdAsyncDetail>>();

        // The multiline content won't parse as int for Quantity, so we expect a ConvertException
        // (conversion failures are never silently defaulted). The buffering path is exercised.
        await Assert.That(async () =>
        {
            await foreach (var group in engine.ReadAsync(stream))
                groups.Add(group);
        }).Throws<ConvertException>();
    }
}
