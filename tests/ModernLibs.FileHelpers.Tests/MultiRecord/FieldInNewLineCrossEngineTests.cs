// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using System.Text;
using ModernLibs.FileHelpers.Tests.Errors;
using ModernLibs.FileHelpers.Tests.Engines;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.MultiRecord;

/// <summary>Single-line record selected by lines starting with <c>A,</c>.</summary>
[DelimitedRecord(",")]
public sealed class XeSingle
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2)] [FieldDelimiter("|")] public string X { get; set; } = string.Empty;
    [FieldOrder(3)] public string Y { get; set; } = string.Empty;
}

/// <summary>Three physical lines, selected by lines starting with <c>T,</c>.</summary>
[DelimitedRecord(",")]
public sealed class XeThreeLines
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2)] [FieldInNewLine] public string B { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldInNewLine] public string C { get; set; } = string.Empty;
}

/// <summary>Master record selected by lines starting with <c>M,</c>.</summary>
[DelimitedRecord(",")]
public sealed class XeMaster
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2)] public string Id { get; set; } = string.Empty;
}

/// <summary>
/// [FieldInNewLine] records read by <see cref="MultiRecordEngine"/> and <see cref="MasterDetailEngine{TMaster, TDetail}"/>
/// give the same records and errors as <see cref="FileHelperEngine{T}"/>: errors on the failing physical line,
/// and after a layout error the following lines are read as the start of the next record (3.x), going
/// through the selector again.
/// </summary>
[Category("FieldInNewLine")]
public sealed class FieldInNewLineCrossEngineTests
{
    // Line 4: conversion error; line 5: extra field; line 6 (read again as a record start): no "|" after X.
    private const string Input = "N,x|\r\ny,1\r\nN,x|\r\ny,bad\r\nN,z|q|\r\nw,2\r\nN,c|\r\nd,3\r\n";

    private static string Format(object record) => record switch
    {
        InlOverrideRecovery r => $"{r.Kind}/{r.X}/{r.Y}/{r.N}",
        XeSingle r => $"{r.Kind}/{r.X}/{r.Y}",
        XeThreeLines r => $"{r.Kind}/{r.B}/{r.C}",
        XeMaster r => $"{r.Kind}/{r.Id}",
        _ => record.ToString() ?? string.Empty,
    };

    private static string Errors(ErrorManager errors, int lineShift = 0) =>
        string.Join(",", errors.Errors.Select(e => $"{e.LineNumber + lineShift}:{e.ExceptionInfo!.GetType().Name}"));

    [Test]
    [DisplayName("FileHelperEngine reference: errors on the failing lines, recovery like 3.x")]
    public async Task FileHelperEngine_Reference()
    {
        // Arrange
        var engine = new FileHelperEngine<InlOverrideRecovery> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = engine.ReadString(Input);

        // Assert
        await Assert.That(string.Join("|", rows.Select(r => Format(r)))).IsEqualTo("N/x/y/1|N/c/d/3");
        await Assert.That(Errors(engine.ErrorManager)).IsEqualTo("4:ConvertException,5:BadUsageException,6:FileHelpersException");
    }

    [Test]
    [DisplayName("MultiRecordEngine and MultiRecordAsyncEngine give the same records and errors as FileHelperEngine")]
    public async Task MultiRecord_SameAsFileHelperEngine()
    {
        // Arrange
        var reference = new FileHelperEngine<InlOverrideRecovery> { ErrorMode = ErrorMode.SaveAndContinue };
        var expectedRows = string.Join("|", reference.ReadString(Input).Select(r => Format(r)));
        var expectedErrors = Errors(reference.ErrorManager);
        var multi = new MultiRecordEngine(MultiRecordType.For<InlOverrideRecovery>(_ => true)) { ErrorMode = ErrorMode.SaveAndContinue };
        await using var multiAsync = new MultiRecordAsyncEngine(MultiRecordType.For<InlOverrideRecovery>(_ => true)) { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = multi.ReadString(Input);
        var asyncRows = new List<object>();
        await foreach (var r in multiAsync.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(Input))))
            asyncRows.Add(r);

        // Assert
        await Assert.That(string.Join("|", rows.Select(Format))).IsEqualTo(expectedRows);
        await Assert.That(Errors(multi.ErrorManager)).IsEqualTo(expectedErrors);
        await Assert.That(string.Join("|", asyncRows.Select(Format))).IsEqualTo(expectedRows);
        await Assert.That(Errors(multiAsync.ErrorManager)).IsEqualTo(expectedErrors);
    }

    [Test]
    [DisplayName("MasterDetailEngine and MasterDetailAsyncEngine read [FieldInNewLine] details like FileHelperEngine")]
    public async Task MasterDetail_SameAsFileHelperEngine()
    {
        // Arrange: a master line before the same detail lines (every line number shifts by one)
        var reference = new FileHelperEngine<InlOverrideRecovery> { ErrorMode = ErrorMode.SaveAndContinue };
        var expectedDetails = string.Join("|", reference.ReadString(Input).Select(r => Format(r)));
        var expectedErrors = Errors(reference.ErrorManager, lineShift: 1);
        static RecordAction Select(ReadOnlySpan<char> line) => line.StartsWith("M,") ? RecordAction.Master : RecordAction.Detail;
        var engine = new MasterDetailEngine<XeMaster, InlOverrideRecovery>(Select) { ErrorMode = ErrorMode.SaveAndContinue };
        await using var asyncEngine = new MasterDetailAsyncEngine<XeMaster, InlOverrideRecovery>(Select) { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var groups = engine.ReadString("M,1\r\n" + Input);
        var asyncGroups = new List<MasterDetails<XeMaster, InlOverrideRecovery>>();
        await foreach (var g in asyncEngine.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes("M,1\r\n" + Input))))
            asyncGroups.Add(g);

        // Assert
        await Assert.That(groups.Length).IsEqualTo(1);
        await Assert.That(Format(groups[0].Master)).IsEqualTo("M/1");
        await Assert.That(string.Join("|", groups[0].Details.Select(d => Format(d)))).IsEqualTo(expectedDetails);
        await Assert.That(Errors(engine.ErrorManager)).IsEqualTo(expectedErrors);
        await Assert.That(asyncGroups.Count).IsEqualTo(1);
        await Assert.That(string.Join("|", asyncGroups[0].Details.Select(d => Format(d)))).IsEqualTo(expectedDetails);
        await Assert.That(Errors(asyncEngine.ErrorManager)).IsEqualTo(expectedErrors);
    }

    [Test]
    [DisplayName("MultiRecordEngine: lines read again after a failing line go through the selector, and each may be a record")]
    public async Task MultiRecord_LinesReadAgain_AreSelectedAgain()
    {
        // Arrange: the first line of a three-line record has an extra field; the next two lines are other records
        var multi = new MultiRecordEngine(
            MultiRecordType.For<XeThreeLines>(l => l.StartsWith("T,")),
            MultiRecordType.For<XeSingle>(l => l.StartsWith("A,")))
        { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = multi.ReadString("T,x,y\r\nA,a|b\r\nA,c|d\r\nT,\r\nb,\r\nc\r\n");

        // Assert: 3.x reads lines 2 and 3 again, the selector makes them single-line records
        await Assert.That(string.Join("|", rows.Select(Format))).IsEqualTo("A/a/b|A/c/d|T/b/c");
        await Assert.That(Errors(multi.ErrorManager)).IsEqualTo("1:BadUsageException");
    }

    [Test]
    [DisplayName("MultiRecordEngine with ThrowException throws the error of the failing line")]
    public async Task MultiRecord_ThrowException_ThrowsFailingLine()
    {
        // Arrange
        var multi = new MultiRecordEngine(MultiRecordType.For<InlOverrideRecovery>(_ => true));

        // Act
        var ex = Capture.Exception(() => multi.ReadString("N,z|q|\r\nw,2\r\nN,c|\r\nd,3\r\n"));

        // Assert
        await Assert.That(ex).IsTypeOf<BadUsageException>();
        await Assert.That(((FileHelpersException)ex!).LineNumber).IsEqualTo(1);
    }
}
