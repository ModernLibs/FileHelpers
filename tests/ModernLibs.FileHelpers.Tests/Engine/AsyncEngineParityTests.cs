// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests.
#pragma warning disable CA1849

using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

/// <summary>
/// Async engine parity tests — ReadRemaining and BeginAppendFile.
/// </summary>
public class AsyncEngineParityTests
{
    [Test]
    [DisplayName("ReadRemaining returns all records after partial ReadNext")]
    public async Task ReadRemaining_AfterPartialReadNext()
    {
        // Arrange: 5 records, read 2 individually
        var engine = new FileHelperAsyncEngine<SimpleRecord>();
        engine.BeginReadString("1,One\n2,Two\n3,Three\n4,Four\n5,Five");

        // Act: read 2, then read remaining
        var first = engine.ReadNext();
        var second = engine.ReadNext();
        var remaining = engine.ReadRemaining();

        // Assert: 3 remaining records
        await Assert.That(first).IsNotNull();
        await Assert.That(first.Id).IsEqualTo(1);
        await Assert.That(second).IsNotNull();
        await Assert.That(second.Id).IsEqualTo(2);
        await Assert.That(remaining.Length).IsEqualTo(3);
        await Assert.That(remaining[0].Id).IsEqualTo(3);
        await Assert.That(remaining[1].Id).IsEqualTo(4);
        await Assert.That(remaining[2].Id).IsEqualTo(5);

        engine.Close();
    }

    [Test]
    [DisplayName("ReadRemaining returns empty array at EOF")]
    public async Task ReadRemaining_AtEof_ReturnsEmpty()
    {
        // Arrange: 2 records, read all
        var engine = new FileHelperAsyncEngine<SimpleRecord>();
        engine.BeginReadString("1,One\n2,Two");

        // Act: read all, then read remaining
        _ = engine.ReadNext();
        _ = engine.ReadNext();
        var remaining = engine.ReadRemaining();

        // Assert: empty array
        await Assert.That(remaining.Length).IsEqualTo(0);

        engine.Close();
    }

    [Test]
    [DisplayName("ReadRemaining without prior ReadNext returns all records")]
    public async Task ReadRemaining_WithoutPriorReadNext()
    {
        // Arrange: 3 records, no prior ReadNext
        var engine = new FileHelperAsyncEngine<SimpleRecord>();
        engine.BeginReadString("1,One\n2,Two\n3,Three");

        // Act: read remaining immediately
        var remaining = engine.ReadRemaining();

        // Assert: all 3 records
        await Assert.That(remaining.Length).IsEqualTo(3);
        await Assert.That(remaining[0].Id).IsEqualTo(1);
        await Assert.That(remaining[1].Id).IsEqualTo(2);
        await Assert.That(remaining[2].Id).IsEqualTo(3);

        engine.Close();
    }

    [Test]
    [DisplayName("BeginAppendFile appends to existing file")]
    public async Task BeginAppendFile_AppendsToExistingFile()
    {
        // Arrange: create file with 2 records
        var filePath = Path.Combine(Path.GetTempPath(), $"append_test_{Guid.NewGuid():N}.csv");
        try
        {
            var writeEngine = new FileHelperAsyncEngine<SimpleRecord>();
            writeEngine.BeginWriteFile(filePath);
            writeEngine.WriteNext(new SimpleRecord { Id = 1, Name = "One" });
            writeEngine.WriteNext(new SimpleRecord { Id = 2, Name = "Two" });
            writeEngine.Close();

            // Act: append 1 more record
            var appendEngine = new FileHelperAsyncEngine<SimpleRecord>();
            appendEngine.BeginAppendFile(filePath);
            appendEngine.WriteNext(new SimpleRecord { Id = 3, Name = "Three" });
            appendEngine.Close();

            // Assert: file has 3 records
            var readEngine = new FileHelperEngine<SimpleRecord>();
            var result = readEngine.ReadFile(filePath);

            await Assert.That(result.Length).IsEqualTo(3);
            await Assert.That(result[0].Id).IsEqualTo(1);
            await Assert.That(result[1].Id).IsEqualTo(2);
            await Assert.That(result[2].Id).IsEqualTo(3);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Test]
    [DisplayName("BeginAppendFile creates file if not exists")]
    public async Task BeginAppendFile_CreatesFileIfNotExists()
    {
        // Arrange: non-existent file path
        var filePath = Path.Combine(Path.GetTempPath(), $"append_new_{Guid.NewGuid():N}.csv");
        try
        {
            // Act: append to non-existent file
            var engine = new FileHelperAsyncEngine<SimpleRecord>();
            engine.BeginAppendFile(filePath);
            engine.WriteNext(new SimpleRecord { Id = 1, Name = "One" });
            engine.Close();

            // Assert: file exists with 1 record
            var readEngine = new FileHelperEngine<SimpleRecord>();
            var result = readEngine.ReadFile(filePath);

            await Assert.That(result.Length).IsEqualTo(1);
            await Assert.That(result[0].Id).IsEqualTo(1);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Test]
    [DisplayName("ReadRemaining respects IgnoreEmptyLines filter")]
    public async Task ReadRemaining_RespectsIgnoreEmptyLines()
    {
        // Arrange: records with empty lines
        var engine = new FileHelperAsyncEngine<SimpleWithIgnoreEmpty>();
        engine.BeginReadString("1,One\n\n2,Two\n\n3,Three\n\n4,Four");

        // Act: read 1, then read remaining
        var first = engine.ReadNext();
        var remaining = engine.ReadRemaining();

        // Assert: empty lines filtered out
        await Assert.That(first).IsNotNull();
        await Assert.That(first.Id).IsEqualTo(1);
        await Assert.That(remaining.Length).IsEqualTo(3);
        await Assert.That(remaining[0].Id).IsEqualTo(2);
        await Assert.That(remaining[1].Id).IsEqualTo(3);
        await Assert.That(remaining[2].Id).IsEqualTo(4);

        engine.Close();
    }
}
