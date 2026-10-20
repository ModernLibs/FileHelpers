// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using System.Globalization;
using ModernLibs.FileHelpers.Tests.Errors;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// Three physical lines; the quoted value of the last line may span lines, so the lines after a failing first
/// line regroup differently and can complete a record of their own.
/// </summary>
[DelimitedRecord(",")]
public sealed class InlRegroup
{
    [FieldOrder(1)] public string A { get; set; } = string.Empty;
    [FieldOrder(2)] [FieldInNewLine] public string B { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldInNewLine] [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.AllowForBoth)] public string C { get; set; } = string.Empty;
    [FieldOrder(4)] public int D { get; set; }
}

/// <summary>
/// After a layout error in a line of a [FieldInNewLine] record the following lines are read again (3.x). The
/// error of the failing line is reported before anything those lines produce.
/// </summary>
[Category("FieldInNewLine")]
public sealed class FieldInNewLineErrorOrderTests
{
    // Line 1 has an extra field. Lines 2-4 then form a record whose D ("X", line 4) does not convert.
    private const string TwoErrors = "a,x\nb,\n\"p,\nq\",X\n";

    // Line 1 has an extra field. Lines 2-4 then form a good record.
    private const string ErrorThenRecord = "a,x\nb,\n\"p,\nq\",5\n";

    [Test]
    [DisplayName("SaveAndContinue: the error of the failing line comes before the errors of the lines read again")]
    public async Task SaveAndContinue_ErrorsInLineOrder()
    {
        // Arrange
        var engine = new FileHelperEngine<InlRegroup> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = engine.ReadString(TwoErrors);

        // Assert
        await Assert.That(rows.Length).IsEqualTo(0);
        await Assert.That(string.Join(",", engine.ErrorManager.Errors.Select(e => e.LineNumber))).IsEqualTo("1,4");
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsTypeOf<BadUsageException>();
        await Assert.That(engine.ErrorManager.Errors[1].ExceptionInfo).IsTypeOf<ConvertException>();
    }

    [Test]
    [DisplayName("ThrowException: the first error is thrown, not one from the lines read again")]
    public async Task ThrowException_ThrowsFirstError()
    {
        // Arrange
        var engine = new FileHelperEngine<InlRegroup>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString(TwoErrors));

        // Assert
        await Assert.That(ex).IsTypeOf<BadUsageException>();
        await Assert.That(((FileHelpersException)ex!).LineNumber).IsEqualTo(1);
    }

    [Test]
    [DisplayName("ThrowException with ReadNext: each pull throws the next error in line order, then reading goes on")]
    public async Task ThrowException_ReadNext_ContinuesAfterEachError()
    {
        // Arrange
        using var engine = new FileHelperAsyncEngine<InlRegroup>();
        engine.BeginReadString(TwoErrors + "z,\ny,\n\"r\",2\n");
        var got = new List<string>();

        // Act
        for (int i = 0; i < 4; i++)
        {
            try
            {
                got.Add(engine.ReadNext() is { } r ? $"{r.A}/{r.B}/{r.C}/{r.D}" : "end");
            }
            catch (FileHelpersException ex)
            {
                got.Add(ex.GetType().Name + "@" + ex.LineNumber.ToString(CultureInfo.InvariantCulture));
            }
        }

        // Assert
        await Assert.That(string.Join(",", got)).IsEqualTo("BadUsageException@1,ConvertException@4,z/y/r/2,end");
    }

    [Test]
    [DisplayName("AfterReadRecord of a record made of the lines read again fires after the error is reported")]
    public async Task AfterReadRecord_FiresAfterTheError()
    {
        // Arrange
        var engine = new FileHelperEngine<InlRegroup> { ErrorMode = ErrorMode.SaveAndContinue };
        var errorsSeen = new List<int>();
        engine.AfterReadRecord += (_, _) => errorsSeen.Add(engine.ErrorManager.ErrorCount);

        // Act
        var rows = engine.ReadString(ErrorThenRecord);

        // Assert: the record of lines 2-4 is read, and when its event fires the line 1 error is already saved
        await Assert.That(string.Join("|", rows.Select(r => $"{r.A}/{r.B}/{r.C}/{r.D}"))).IsEqualTo("b/\"p/q\"/5");
        await Assert.That(string.Join(",", errorsSeen)).IsEqualTo("1");
    }
}
