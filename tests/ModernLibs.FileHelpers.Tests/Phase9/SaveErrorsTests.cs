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
public sealed class SaveErrorsTests
{
    [Test]
    [DisplayName("ErrorManager.SaveErrors writes error details to specified file path")]
    public async Task SaveErrors_WritesErrorsToFile()
    {
        // Arrange: Create engine with SaveAndContinue mode, parse malformed input
        var engine = new FileHelperEngine<CsvOrder>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        engine.ReadString("Header\r\nBAD\r\n2,Bob,200.00");
        var path = Path.GetTempFileName();
        try
        {
            engine.ErrorManager.SaveErrors(path);

            // Act: Read the saved errors file
            var content = File.ReadAllText(path);

            // Assert: File contains the error record string "BAD"
            await Assert.That(content).Contains("BAD");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
