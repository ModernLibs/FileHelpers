// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using ModernLibs.FileHelpers.Tests.MasterDetail;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.MultiRecord;

/// <summary>
/// The errors of <see cref="MultiRecordEngine"/> and <see cref="MasterDetailEngine{TMaster, TDetail}"/> carry the
/// text and line of the failing record, for conversion errors and for a selector that throws, in every error mode.
/// </summary>
[Category("MultiRecord")]
public sealed class MultiReaderErrorTextTests
{
    private const string MultiInput = "H|B001\nD|x|1\nBOOM\nD|10|42.5";

    private static MultiRecordEngine Multi(ErrorMode mode) => new(
        MultiRecordType.For<MultiHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
        MultiRecordType.For<MultiDetail>(line => line.StartsWith("BOOM", StringComparison.Ordinal)
            ? throw new InvalidOperationException("selector failed")
            : line.StartsWith("D|", StringComparison.Ordinal)))
    {
        ErrorMode = mode,
    };

    private const string MasterDetailInput = "M|A\nD|A|x\nBOOM\nD|A|3";

    private static MasterDetailEngine<MasterRecord, DetailRecord> MasterDetail(ErrorMode mode) => new(line =>
        line.StartsWith("BOOM", StringComparison.Ordinal) ? throw new InvalidOperationException("selector failed") :
        line.StartsWith("M|", StringComparison.Ordinal) ? RecordAction.Master :
        line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
        RecordAction.Skip)
    {
        ErrorMode = mode,
    };

    [Test]
    [DisplayName("MultiRecordEngine SaveAndContinue: conversion and selector errors keep the record text and line")]
    public async Task Multi_SaveAndContinue_RecordText()
    {
        // Arrange
        var engine = Multi(ErrorMode.SaveAndContinue);

        // Act
        var records = engine.ReadString(MultiInput);
        var errors = engine.ErrorManager.Errors;

        // Assert: the header and the good detail are read; the bad detail and the selector failure are saved
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(errors.Count).IsEqualTo(2);
        await Assert.That(errors[0].LineNumber).IsEqualTo(2);
        await Assert.That(errors[0].RecordString).IsEqualTo("D|x|1");
        await Assert.That(errors[0].ExceptionInfo).IsTypeOf<ConvertException>();
        await Assert.That(errors[1].LineNumber).IsEqualTo(3);
        await Assert.That(errors[1].RecordString).IsEqualTo("BOOM");
        await Assert.That(errors[1].ExceptionInfo!.Message).Contains("Selector failed to process correctly");
    }

    [Test]
    [DisplayName("MultiRecordEngine IgnoreAndContinue skips both errors; ThrowException reports the selector failure with its line")]
    public async Task Multi_IgnoreAndThrow()
    {
        // Arrange
        var ignoring = Multi(ErrorMode.IgnoreAndContinue);
        var throwing = Multi(ErrorMode.ThrowException);

        // Act
        var records = ignoring.ReadString(MultiInput);
        var thrown = Assert.Throws<FileHelpersException>(() => throwing.ReadString("H|B001\nBOOM"));

        // Assert
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(ignoring.ErrorManager.ErrorCount).IsEqualTo(0);
        await Assert.That(thrown.LineNumber).IsEqualTo(2);
        await Assert.That(thrown.Message).Contains("Selector failed to process correctly");
    }

    [Test]
    [DisplayName("MasterDetailEngine SaveAndContinue: conversion and selector errors keep the record text and line")]
    public async Task MasterDetail_SaveAndContinue_RecordText()
    {
        // Arrange
        var engine = MasterDetail(ErrorMode.SaveAndContinue);

        // Act
        var groups = engine.ReadString(MasterDetailInput);
        var errors = engine.ErrorManager.Errors;

        // Assert: one group with the good detail; the bad detail and the selector failure are saved
        await Assert.That(groups.Length).IsEqualTo(1);
        await Assert.That(groups[0].Details.Count).IsEqualTo(1);
        await Assert.That(errors.Count).IsEqualTo(2);
        await Assert.That(errors[0].LineNumber).IsEqualTo(2);
        await Assert.That(errors[0].RecordString).IsEqualTo("D|A|x");
        await Assert.That(errors[0].ExceptionInfo).IsTypeOf<ConvertException>();
        await Assert.That(errors[1].LineNumber).IsEqualTo(3);
        await Assert.That(errors[1].RecordString).IsEqualTo("BOOM");
        await Assert.That(errors[1].ExceptionInfo!.Message).Contains("Supplied Record selector failed to process record");
    }

    [Test]
    [DisplayName("MasterDetailEngine IgnoreAndContinue skips both errors; ThrowException reports the selector failure with its line")]
    public async Task MasterDetail_IgnoreAndThrow()
    {
        // Arrange
        var ignoring = MasterDetail(ErrorMode.IgnoreAndContinue);
        var throwing = MasterDetail(ErrorMode.ThrowException);

        // Act
        var groups = ignoring.ReadString(MasterDetailInput);
        var thrown = Assert.Throws<FileHelpersException>(() => throwing.ReadString("M|A\nBOOM"));

        // Assert
        await Assert.That(groups.Length).IsEqualTo(1);
        await Assert.That(ignoring.ErrorManager.ErrorCount).IsEqualTo(0);
        await Assert.That(thrown.LineNumber).IsEqualTo(2);
        await Assert.That(thrown.Message).Contains("Supplied Record selector failed to process record");
    }
}
