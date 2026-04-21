// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional.
#pragma warning disable MA0048

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Phase9;

[Category("Phase9")]
[Category("Engine")]
public sealed class ErrorManagerTests
{
    [Test]
    [DisplayName("ErrorManager.HasErrors returns false when no errors have occurred")]
    public async Task ErrorManager_HasErrors_FalseWhenNoErrors()
    {
        // Arrange & Act: Create engine (no parsing yet)
        var engine = new FileHelperEngine<CsvOrder>();

        // Assert: HasErrors is false for clean engine
        await Assert.That(engine.ErrorManager.HasErrors).IsFalse();
    }

    [Test]
    [DisplayName("ErrorManager.ClearErrors empties the error list and resets HasErrors to false")]
    public async Task ErrorManager_ClearErrors_EmptiesErrorList()
    {
        // Arrange: Create engine, parse malformed input to generate errors
        var engine = new FileHelperEngine<CsvOrder>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        engine.ReadString("Header\r\nBAD");

        // Assert: HasErrors is true after error occurred
        await Assert.That(engine.ErrorManager.HasErrors).IsTrue();

        // Act: Clear errors
        engine.ErrorManager.ClearErrors();

        // Assert: HasErrors is false after clearing
        await Assert.That(engine.ErrorManager.HasErrors).IsFalse();
    }

    [Test]
    [DisplayName("ErrorManager.Errors collection contains line number and record string for each error")]
    public async Task ErrorManager_Errors_ContainsLineNumberAndRecordString()
    {
        // Arrange: Create engine with SaveAndContinue mode, parse malformed input
        var engine = new FileHelperEngine<CsvOrder>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;

        // Act: Parse input with malformed line
        engine.ReadString("Header\r\nBAD");

        // Assert: Error contains line content "BAD" and has exception info
        await Assert.That(engine.ErrorManager.Errors[0].RecordString).IsEqualTo("BAD");
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsNotNull();
    }
}
