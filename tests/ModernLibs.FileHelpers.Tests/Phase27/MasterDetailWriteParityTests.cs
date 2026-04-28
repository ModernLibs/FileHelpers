// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait is not needed in test bodies.
#pragma warning disable MA0004

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase27;

[Category("Phase27")]
[Category("MasterDetail")]
public sealed class MasterDetailWriteParityTests
{
    [Test]
    [DisplayName("MasterDetailEngine write wrappers emit equivalent grouped output")]
    public async Task WriteWrappers_Groups_ReturnEquivalentOutput()
    {
        // Arrange: build deterministic grouped records for all write wrappers.
        var engine = MasterDetailIoFixtures.CreateEngine();
        var groups = MasterDetailIoFixtures.CreateGroups();
        var expected = MasterDetailIoFixtures.CreateExpectedWritePayload(Environment.NewLine);
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act: write through each public sync wrapper.
            var fromString = engine.WriteString(groups);
            using var writer = new StringWriter();
            engine.WriteStream(writer, groups);
            var fromWriter = writer.ToString();
            engine.WriteFile(tempFile, groups);
            var fromFile = File.ReadAllText(tempFile);

            // Assert: all wrappers produce the same newline-aware payload.
            await Assert.That(fromString).IsEqualTo(expected);
            await Assert.That(fromWriter).IsEqualTo(expected);
            await Assert.That(fromFile).IsEqualTo(expected);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    [DisplayName("MasterDetailEngine write payload round trips back to equivalent grouped records")]
    public async Task WriteString_ThenReadString_RoundTripsGroups()
    {
        // Arrange: generate canonical grouped input.
        var engine = MasterDetailIoFixtures.CreateEngine();
        var expected = MasterDetailIoFixtures.CreateGroups();

        // Act: write and read through the same closed-set selector path.
        var payload = engine.WriteString(expected);
        var actual = engine.ReadString(payload);

        // Assert: grouped model shape and values remain stable.
        await Assert.That(actual.Length).IsEqualTo(expected.Length);
        await Assert.That(actual[0].Master.Id).IsEqualTo("A");
        await Assert.That(actual[0].Master.Notes).IsEqualTo($"Master line 1{Environment.NewLine}Master line 2");
        await Assert.That(actual[0].Details.Count).IsEqualTo(1);
        await Assert.That(actual[0].Details[0].Quantity).IsEqualTo(2);
        await Assert.That(actual[1].Master.Id).IsEqualTo("B");
        await Assert.That(actual[1].Details[0].MasterId).IsEqualTo("B");
        await Assert.That(actual[1].Details[0].Notes).IsEqualTo($"Detail B line 1{Environment.NewLine}Detail B line 2");
    }
}
