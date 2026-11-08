// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using System.Text;
using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Errors;

[DelimitedRecord(",")]
public sealed class IeaRow
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Text { get; set; } = string.Empty;
}

/// <summary>
/// <see cref="ErrorMode.IgnoreAndContinue"/> does not keep the text of a failing record, so it does not copy it;
/// the other modes still report it.
/// </summary>
[Category("Errors")]
public sealed class IgnoredErrorAllocationTests
{
    private const int TextLength = 4000;

    private static string BadLines(int count)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
            sb.Append("x,").Append('t', TextLength).Append("\r\n");
        return sb.ToString();
    }

    [Test]
    [DisplayName("IgnoreAndContinue: a record that fails to convert costs less than a copy of its text")]
    public async Task IgnoreAndContinue_DoesNotCopyRecordText()
    {
        // Arrange: 200 records whose int field fails; warm up
        var engine = new FileHelperEngine<IeaRow> { ErrorMode = ErrorMode.IgnoreAndContinue };
        var content = BadLines(200);
        _ = engine.ReadString(content);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();
        var records = engine.ReadString(content);
        long perRecord = (GC.GetAllocatedBytesForCurrentThread() - before) / 200;

        // Assert: no records, and less per failing record than the 8 KB a string of its text takes
        await Assert.That(records.Length).IsEqualTo(0);
        await Assert.That(perRecord).IsLessThan(TextLength * sizeof(char));
    }

    [Test]
    [DisplayName("SaveAndContinue and ThrowException still report the text of the failing record")]
    public async Task OtherModes_ReportRecordText()
    {
        // Arrange
        var content = BadLines(1);
        var expected = content.TrimEnd('\r', '\n');
        var saving = new FileHelperEngine<IeaRow> { ErrorMode = ErrorMode.SaveAndContinue };
        var throwing = new FileHelperEngine<IeaRow>();

        // Act
        saving.ReadString(content);
        var thrown = Assert.Throws<ConvertException>(() => throwing.ReadString(content));

        // Assert
        await Assert.That(saving.ErrorManager.Errors[0].RecordString).IsEqualTo(expected);
        await Assert.That(saving.ErrorManager.Errors[0].LineNumber).IsEqualTo(1);
        await Assert.That(thrown.LineNumber).IsEqualTo(1);
    }
}
