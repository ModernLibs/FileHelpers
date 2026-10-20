// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait is not needed in test bodies.
#pragma warning disable MA0004

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.MasterDetail;

[Category("MasterDetail")]
public sealed class MasterDetailReadParityTests
{
    [Test]
    [DisplayName("MasterDetailEngine read wrappers return equivalent grouped results for multiline payloads")]
    public async Task ReadWrappers_MultilinePayload_ReturnEquivalentGroups()
    {
        // Arrange: create the same grouped payload for all wrappers.
        var engine = MasterDetailIoFixtures.CreateEngine();
        string payload = MasterDetailIoFixtures.CreateReadPayload(Environment.NewLine);
        string tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, payload);

        try
        {
            // Act: read through each public sync wrapper.
            var fromString = engine.ReadString(payload);
            using var reader = new StringReader(payload);
            var fromReader = engine.ReadStream(reader);
            var fromFile = engine.ReadFile(tempFile);

            // Assert: all wrappers produce the same grouped model.
            await Assert.That(fromString.Length).IsEqualTo(2);
            await Assert.That(fromReader.Length).IsEqualTo(fromString.Length);
            await Assert.That(fromFile.Length).IsEqualTo(fromString.Length);

            await Assert.That(fromString[0].Master.Id).IsEqualTo("A");
            await Assert.That(fromString[0].Master.Notes).IsEqualTo($"Master line 1{Environment.NewLine}Master line 2");
            await Assert.That(fromString[0].Details.Count).IsEqualTo(1);
            await Assert.That(fromString[0].Details[0].Quantity).IsEqualTo(2);

            await Assert.That(fromString[1].Master.Id).IsEqualTo("B");
            await Assert.That(fromString[1].Details.Count).IsEqualTo(1);
            await Assert.That(fromString[1].Details[0].Notes).IsEqualTo($"Detail B line 1{Environment.NewLine}Detail B line 2");

            await Assert.That(fromReader[0].Master.Notes).IsEqualTo(fromString[0].Master.Notes);
            await Assert.That(fromReader[1].Details[0].Notes).IsEqualTo(fromString[1].Details[0].Notes);
            await Assert.That(fromFile[0].Master.Notes).IsEqualTo(fromString[0].Master.Notes);
            await Assert.That(fromFile[1].Details[0].Notes).IsEqualTo(fromString[1].Details[0].Notes);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    [DisplayName("MasterDetailEngine keeps strict detail before master behavior with multiline detail records")]
    public async Task ReadString_DetailBeforeMaster_WithMultilineDetail_ThrowsBadUsageException()
    {
        // Arrange: start with a detail record spread across two physical lines.
        var engine = MasterDetailIoFixtures.CreateEngine();
        string payload = string.Join(
            Environment.NewLine,
            [
                "D|A|2|\"Detail line 1",
                "Detail line 2\"",
                "M|A|\"Master\"",
            ]);

        // Act + Assert: strict grouping still rejects details before first master.
        await Assert.ThrowsAsync<BadUsageException>(() =>
        {
            engine.ReadString(payload);
            return Task.CompletedTask;
        });
    }
}
