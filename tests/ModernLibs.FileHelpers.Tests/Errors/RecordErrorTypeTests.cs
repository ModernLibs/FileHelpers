// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Attributes;
using ModernLibs.FileHelpers.Tests.Parsing;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Errors;

/// <summary>
/// Record-level data errors raise the FileHelpers 3.x exception types instead of
/// <see cref="FormatException"/>: missing delimiters → <see cref="FileHelpersException"/>, extra delimiters,
/// wrong fixed-length width, incomplete multi-line records and unclosed quotes →
/// <see cref="BadUsageException"/>, <c>[FieldNotEmpty]</c> violations → <see cref="ConvertException"/>.
/// </summary>
[Category("Errors")]
public sealed class RecordErrorTypeTests
{
    [Test]
    [DisplayName("Delimited: fewer fields than expected throws FileHelpersException with line and record text")]
    public async Task Delimited_FewerFields_ThrowsFileHelpersException()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvClassRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString("1,a,10\n2,b"));

        // Assert: base library type (3.x "Delimiter not found"), positioned on line 2
        await Assert.That(ex).IsNotNull();
        await Assert.That(ex!.GetType()).IsEqualTo(typeof(FileHelpersException));
        await Assert.That(((FileHelpersException)ex).LineNumber).IsEqualTo(2);
        await Assert.That(ex.Message).StartsWith("Line: 2");
        await Assert.That(ex.Message).Contains("Expected 3 fields but found 2");
        await Assert.That(ex.Message).Contains("'2,b'");
    }

    [Test]
    [DisplayName("Delimited: more fields than expected throws BadUsageException")]
    public async Task Delimited_ExtraFields_ThrowsBadUsageException()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvClassRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString("1,a,10,extra"));

        // Assert
        await Assert.That(ex).IsTypeOf<BadUsageException>();
        await Assert.That(((BadUsageException)ex!).LineNumber).IsEqualTo(1);
        await Assert.That(ex.Message).Contains("Expected 3 fields but found 4");
    }

    [Test]
    [DisplayName("Field-count errors are typed on the streaming and async-engine paths too")]
    public async Task FieldCount_StreamingPaths()
    {
        // Arrange
        var streaming = new FileHelperEngine<ConvClassRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        using var asyncEngine = new FileHelperAsyncEngine<ConvClassRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        asyncEngine.BeginReadString("1,a,10\n2,b\n3,c,30,x");

        // Act
        var streamed = streaming.ReadSync("1,a,10\n2,b\n3,c,30,x").ToList();
        var asyncRecords = asyncEngine.ReadRemaining();

        // Assert
        await Assert.That(streamed.Count).IsEqualTo(1);
        await Assert.That(streaming.ErrorManager.Errors[0].ExceptionInfo!.GetType()).IsEqualTo(typeof(FileHelpersException));
        await Assert.That(streaming.ErrorManager.Errors[1].ExceptionInfo).IsTypeOf<BadUsageException>();
        await Assert.That(asyncRecords.Length).IsEqualTo(1);
        await Assert.That(asyncEngine.ErrorManager.Errors[0].ExceptionInfo!.GetType()).IsEqualTo(typeof(FileHelpersException));
        await Assert.That(asyncEngine.ErrorManager.Errors[1].ExceptionInfo).IsTypeOf<BadUsageException>();
        await Assert.That(asyncEngine.ErrorManager.Errors[1].LineNumber).IsEqualTo(3);
    }

    [Test]
    [DisplayName("Fixed-length: line with the wrong width throws BadUsageException with line number")]
    public async Task Fixed_WrongWidth_ThrowsBadUsageException()
    {
        // Arrange
        var engine = new FileHelperEngine<ConvFixedRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString("00001abc\n00002ab"));

        // Assert
        await Assert.That(ex).IsTypeOf<BadUsageException>();
        await Assert.That(((BadUsageException)ex!).LineNumber).IsEqualTo(2);
        await Assert.That(ex.Message).Contains("has length 7 but expected 8 characters");
    }

    [Test]
    [DisplayName("[FieldNotEmpty] violation throws ConvertException naming the field (3.x)")]
    public async Task FieldNotEmpty_ThrowsConvertException()
    {
        // Arrange
        var engine = new FileHelperEngine<NotEmptyDelimitedRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString("ok,1\n,2"));

        // Assert
        await Assert.That(ex).IsTypeOf<ConvertException>();
        var convert = (ConvertException)ex!;
        await Assert.That(convert.FieldName).IsEqualTo("Name");
        await Assert.That(convert.LineNumber).IsEqualTo(2);
        await Assert.That(convert.MessageExtra).IsEqualTo("The value is empty and must be populated.");
    }

    [Test]
    [DisplayName("[FieldInNewLine] record missing its continuation lines throws BadUsageException")]
    public async Task IncompleteMultiLineRecord_ThrowsBadUsageException()
    {
        // Arrange: the record needs three physical lines but only two remain
        var engine = new FileHelperEngine<MultiNewLineFixedRecord>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString("ABCD\nNAME01"));

        // Assert
        await Assert.That(ex).IsTypeOf<BadUsageException>();
        await Assert.That(((BadUsageException)ex!).LineNumber).IsEqualTo(1);
    }

    [Test]
    [DisplayName("Unclosed quoted field at end of input throws BadUsageException")]
    public async Task UnclosedQuote_ThrowsBadUsageException()
    {
        // Arrange
        var engine = new FileHelperEngine<MultilineCustomer>();

        // Act
        var ex = Capture.Exception(() => engine.ReadString("John,\"Unclosed multiline,NYC"));

        // Assert
        await Assert.That(ex).IsTypeOf<BadUsageException>();
        await Assert.That(((BadUsageException)ex!).LineNumber).IsEqualTo(1);
        await Assert.That(ex.Message).Contains("Unclosed quoted field");
    }
}
