// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using ModernLibs.FileHelpers.Tests.Engines;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Errors;

/// <summary><see cref="ErrorManager.SaveErrors(string)"/> reports the column 1-based, like the exception message.</summary>
[Category("Errors")]
public sealed class SaveErrorsColumnTests
{
    [Test]
    [DisplayName("SaveErrors writes the 1-based column of the exception message; empty when unknown")]
    public async Task SaveErrors_ColumnIsOneBased()
    {
        // Arrange: a conversion error in the second field (column 4) and a field-count error (no column)
        var engine = new FileHelperEngine<RcStruct> { ErrorMode = ErrorMode.SaveAndContinue };
        _ = engine.ReadString("1,2\r\n10,xx\r\n3\r\n");
        var convert = (ConvertException)engine.ErrorManager.Errors[0].ExceptionInfo!;
        string path = Path.Combine(Path.GetTempPath(), "fh-saveerrors-" + Guid.NewGuid().ToString("N") + ".txt");

        try
        {
            // Act
            engine.ErrorManager.SaveErrors(path);
            string[] lines = await File.ReadAllLinesAsync(path);

            // Assert: same column as ConvertException.ColumnNumber and its message
            await Assert.That(convert.ColumnNumber).IsEqualTo(4);
            await Assert.That(convert.Message).Contains("Column: 4.");
            await Assert.That(lines[2]).StartsWith("2\t|\t4\t|\tValue\t|");
            await Assert.That(lines[3]).StartsWith("3\t|\t\t|\t\t|");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
