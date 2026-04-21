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
public sealed class ErrorModeTests
{
    [Test]
    [DisplayName("ThrowException mode propagates FormatException immediately on malformed data line")]
    public async Task ThrowException_MalformedRecord_ThrowsImmediately()
    {
        // Arrange: Create engine with ErrorMode set to ThrowException
        var engine = new FileHelperEngine<CsvOrder>();
        engine.ErrorMode = ErrorMode.ThrowException;

        // Act & Assert: Malformed input throws exception
        var input = "Header\r\nBAD";
        await Assert.That(() => engine.ReadString(input)).ThrowsException();
    }

    [Test]
    [DisplayName("ThrowException mode includes line content and line number in exception message")]
    public async Task ThrowException_MalformedRecord_IncludesLineAndRecordContext()
    {
        // Arrange: Create engine with ErrorMode set to ThrowException
        var engine = new FileHelperEngine<CsvOrder>();
        engine.ErrorMode = ErrorMode.ThrowException;
        bool threw = false;

        try
        {
            _ = engine.ReadString("Header\r\nBAD");
        }
        catch (Exception ex)
        {
            // Act: Capture exception details
            threw = true;

            // Assert: Exception message contains line content and line number
            await Assert.That(ex.Message).Contains("BAD");
            await Assert.That(ex.Message).Contains("2");
        }

        await Assert.That(threw).IsTrue();
    }

    [Test]
    [DisplayName("ThrowException mode on async engine includes line content and line number in exception message")]
    public async Task ThrowException_MalformedRecord_IncludesLineAndRecordContext_AsyncEngine()
    {
        // Arrange: Create async engine with ErrorMode set to ThrowException
        using var engine = new FileHelperAsyncEngine<CsvOrder>();
        engine.ErrorMode = ErrorMode.ThrowException;
        engine.BeginReadString("Header\r\nBAD");
        bool threw = false;

        try
        {
            _ = engine.ReadNext();
        }
        catch (Exception ex)
        {
            // Act: Capture exception details
            threw = true;

            // Assert: Exception message contains line content and line number
            await Assert.That(ex.Message).Contains("BAD");
            await Assert.That(ex.Message).Contains("2");
        }

        await Assert.That(threw).IsTrue();
    }

    [Test]
    [DisplayName("IgnoreAndContinue mode skips malformed record and returns only valid records")]
    public async Task IgnoreAndContinue_MalformedRecord_ReturnsValidRecords()
    {
        // Arrange: Create engine with ErrorMode set to IgnoreAndContinue
        var engine = new FileHelperEngine<CsvOrder>();
        engine.ErrorMode = ErrorMode.IgnoreAndContinue;
        var input = "Header\r\nBAD\r\n2,Bob,200.00";

        // Act: Parse input with malformed line
        var records = engine.ReadString(input);

        // Assert: Only valid record returned (malformed line skipped)
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(records[0].OrderId).IsEqualTo(2);
    }

    [Test]
    [DisplayName("SaveAndContinue mode captures error in ErrorManager and continues parsing")]
    public async Task SaveAndContinue_MalformedRecord_CapturesErrorAndContinues()
    {
        // Arrange: Create engine with ErrorMode set to SaveAndContinue
        var engine = new FileHelperEngine<CsvOrder>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        var input = "Header\r\nBAD\r\n2,Bob,200.00";

        // Act: Parse input with malformed line
        var records = engine.ReadString(input);

        // Assert: Valid record returned and error captured in ErrorManager
        await Assert.That(records.Length).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.HasErrors).IsTrue();
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
    }

    [Test]
    [DisplayName("Engine ErrorMode property delegates to ErrorManager.ErrorMode")]
    public async Task ErrorModeProperty_DelegatesToErrorManagerMode()
    {
        // Arrange: Create engine
        var engine = new FileHelperEngine<CsvOrder>();

        // Act: Set ErrorMode property on engine
        engine.ErrorMode = ErrorMode.SaveAndContinue;

        // Assert: ErrorManager reflects the same ErrorMode value
        await Assert.That(engine.ErrorManager.ErrorMode).IsEqualTo(ErrorMode.SaveAndContinue);
    }
}
