// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.MultiRecord;

[Category("MultiRecord")]
public sealed class MultiRecordErrorSemanticsTests
{
    [Test]
    [DisplayName("Default mode skips a line no selector matches (FileHelpers 3.x)")]
    public async Task SelectorMiss_DefaultMode_SkipsLine()
    {
        // Arrange: build an engine with one unmatched line in the mixed payload.
        var engine = MultiRecordIoFixtures.CreateEngine();
        string input = "H|B001\nX|UNMATCHED\nD|10|42.5";

        // Act: parse with default ErrorMode.ThrowException semantics.
        object[] records = engine.ReadString(input);

        // Assert: the unmatched line is skipped without an exception.
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0]).IsTypeOf<MultiRecordIoHeader>();
        await Assert.That(records[1]).IsTypeOf<MultiRecordIoDetail>();
    }

    [Test]
    [DisplayName("IgnoreAndContinue skips unmatched selector lines and keeps matched records ordered")]
    public async Task SelectorMiss_IgnoreAndContinue_SkipsAndParsesMatches()
    {
        // Arrange: configure non-throw mode with one unmatched line between valid records.
        var engine = MultiRecordIoFixtures.CreateEngine();
        engine.ErrorMode = ErrorMode.IgnoreAndContinue;
        string input = "H|B001\nX|UNMATCHED\nD|10|42.5";

        // Act: parse while ignoring selector misses.
        object[] records = engine.ReadString(input);

        // Assert: matched records are preserved in order and no error is stored.
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0]).IsTypeOf<MultiRecordIoHeader>();
        await Assert.That(records[1]).IsTypeOf<MultiRecordIoDetail>();
        await Assert.That(((MultiRecordIoHeader)records[0]).BatchId).IsEqualTo("B001");
        await Assert.That(((MultiRecordIoDetail)records[1]).ItemId).IsEqualTo(10);
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(0);
    }

    [Test]
    [DisplayName("SaveAndContinue skips a selector miss without recording an error (FileHelpers 3.x)")]
    public async Task SelectorMiss_SaveAndContinue_SkipsWithoutError()
    {
        // Arrange: configure save mode with one unmatched line between valid records.
        var engine = MultiRecordIoFixtures.CreateEngine();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        string input = "H|B001\nX|UNMATCHED\nD|10|42.5";

        // Act: parse while saving selector misses in ErrorManager.
        object[] records = engine.ReadString(input);

        // Assert: matched records are kept and no error is recorded.
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0]).IsTypeOf<MultiRecordIoHeader>();
        await Assert.That(records[1]).IsTypeOf<MultiRecordIoDetail>();
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(0);
    }
}
