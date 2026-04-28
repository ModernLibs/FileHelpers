// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase26;

[Category("Phase26")]
[Category("MultiRecord")]
public sealed class MultiRecordErrorSemanticsTests
{
    [Test]
    [DisplayName("Default selector miss behavior throws with line number and raw line context")]
    public async Task SelectorMiss_DefaultMode_ThrowsWithContext()
    {
        // Arrange: build an engine with one unmatched line in the mixed payload.
        var engine = MultiRecordIoFixtures.CreateEngine();
        string input = "H|B001\nX|UNMATCHED\nD|10|42.5";
        bool threw = false;

        // Act: parse with default ErrorMode.ThrowException semantics.
        try
        {
            _ = engine.ReadString(input);
        }
        catch (FormatException ex)
        {
            threw = true;

            // Assert: exception contains deterministic selector-miss context.
            await Assert.That(ex.Message).Contains("line 2");
            await Assert.That(ex.Message).Contains("X|UNMATCHED");
        }

        await Assert.That(threw).IsTrue();
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
        await Assert.That(records[0]).IsTypeOf<Phase26HeaderRecord>();
        await Assert.That(records[1]).IsTypeOf<Phase26DetailRecord>();
        await Assert.That(((Phase26HeaderRecord)records[0]).BatchId).IsEqualTo("B001");
        await Assert.That(((Phase26DetailRecord)records[1]).ItemId).IsEqualTo(10);
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(0);
    }

    [Test]
    [DisplayName("SaveAndContinue records selector miss with line number and raw line text")]
    public async Task SelectorMiss_SaveAndContinue_CapturesErrorAndContinues()
    {
        // Arrange: configure save mode with one unmatched line between valid records.
        var engine = MultiRecordIoFixtures.CreateEngine();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        string input = "H|B001\nX|UNMATCHED\nD|10|42.5";

        // Act: parse while saving selector misses in ErrorManager.
        object[] records = engine.ReadString(input);

        // Assert: matched records are kept and one selector miss is captured.
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0]).IsTypeOf<Phase26HeaderRecord>();
        await Assert.That(records[1]).IsTypeOf<Phase26DetailRecord>();
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.Errors[0].RecordString).IsEqualTo("X|UNMATCHED");
    }
}
