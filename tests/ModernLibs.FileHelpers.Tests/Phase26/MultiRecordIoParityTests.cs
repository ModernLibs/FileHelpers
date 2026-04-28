// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using System.Text;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase26;

[Category("Phase26")]
[Category("MultiRecord")]
public sealed class MultiRecordIoParityTests
{
    [Test]
    [DisplayName("ReadString ReadStream and ReadFile return equivalent mixed records")]
    public async Task ReadApis_ReturnEquivalentMixedRecords()
    {
        // Arrange: create equivalent readers over the same canonical mixed-record payload.
        var engine = MultiRecordIoFixtures.CreateEngine();
        string content = MultiRecordIoFixtures.CanonicalInput;
        string tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, content, Encoding.UTF8);

        try
        {
            // Act: read through all public synchronous read entry points.
            object[] fromString = engine.ReadString(content);
            using var textReader = new StringReader(content);
            object[] fromReader = engine.ReadStream(textReader);
            object[] fromFile = engine.ReadFile(tempFile);

            // Assert: all paths preserve record count, order, type, and values.
            await AssertRecordsEquivalent(fromString, fromReader);
            await AssertRecordsEquivalent(fromString, fromFile);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    [DisplayName("WriteString WriteStream and WriteFile produce equivalent serialized text")]
    public async Task WriteApis_ProduceEquivalentText()
    {
        // Arrange: parse canonical content once to ensure a representative heterogeneous record sequence.
        var engine = MultiRecordIoFixtures.CreateEngine();
        object[] records = engine.ReadString(MultiRecordIoFixtures.CanonicalInput);
        string tempFile = Path.GetTempFileName();

        try
        {
            // Act: write through all public synchronous write entry points.
            string fromString = engine.WriteString(records);
            using var writer = new StringWriter();
            engine.WriteStream(writer, records);
            string fromWriter = writer.ToString();
            engine.WriteFile(tempFile, records);
            string fromFile = await File.ReadAllTextAsync(tempFile, Encoding.UTF8);
            string normalizedFromFile = fromFile.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace('\n', Environment.NewLine[0]);
            if (Environment.NewLine.Length == 2)
            {
                normalizedFromFile = normalizedFromFile.Replace("\r", string.Empty, StringComparison.Ordinal);
            }

            // Assert: each path yields equivalent content and the expected canonical output.
            await Assert.That(fromString).IsEqualTo(fromWriter);
            await Assert.That(fromString).IsEqualTo(normalizedFromFile);
            await Assert.That(fromString).IsEqualTo(MultiRecordIoFixtures.CanonicalOutput);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    [DisplayName("Mixed records preserve order when round-tripped through stream and file paths")]
    public async Task RoundTrip_StreamAndFile_PreserveMixedOrder()
    {
        // Arrange: create a deterministic mixed-record sequence and temporary storage targets.
        var engine = MultiRecordIoFixtures.CreateEngine();
        object[] original = engine.ReadString(MultiRecordIoFixtures.CanonicalInput);
        string tempFile = Path.GetTempFileName();

        try
        {
            // Act: write to stream and file, then read back from stream and file.
            using var streamWriter = new StringWriter();
            engine.WriteStream(streamWriter, original);
            string streamContent = streamWriter.ToString();
            using var streamReader = new StringReader(streamContent);
            object[] fromStreamRoundTrip = engine.ReadStream(streamReader);

            engine.WriteFile(tempFile, original);
            object[] fromFileRoundTrip = engine.ReadFile(tempFile);

            // Assert: both round-trip paths preserve order, type routing, and values.
            await AssertRecordsEquivalent(original, fromStreamRoundTrip);
            await AssertRecordsEquivalent(original, fromFileRoundTrip);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static async Task AssertRecordsEquivalent(object[] expected, object[] actual)
    {
        await Assert.That(actual.Length).IsEqualTo(expected.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            await Assert.That(actual[i].GetType()).IsEqualTo(expected[i].GetType());

            switch (expected[i])
            {
                case Phase26HeaderRecord expectedHeader:
                    var actualHeader = (Phase26HeaderRecord)actual[i];
                    await Assert.That(actualHeader.Kind).IsEqualTo(expectedHeader.Kind);
                    await Assert.That(actualHeader.BatchId).IsEqualTo(expectedHeader.BatchId);
                    break;
                case Phase26DetailRecord expectedDetail:
                    var actualDetail = (Phase26DetailRecord)actual[i];
                    await Assert.That(actualDetail.Kind).IsEqualTo(expectedDetail.Kind);
                    await Assert.That(actualDetail.ItemId).IsEqualTo(expectedDetail.ItemId);
                    await Assert.That(actualDetail.Amount).IsEqualTo(expectedDetail.Amount);
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected record type: {expected[i].GetType().Name}");
            }
        }
    }
}
