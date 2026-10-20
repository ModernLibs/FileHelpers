// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test fixtures per file — intentional for per-field delimiter tests.
#pragma warning disable MA0048
// MA0025: Fixture bodies intentionally empty — engine populates fields via source-generated code.
#pragma warning disable MA0025
// CS0649: Public fields/properties are assigned by the source-generated descriptor.
#pragma warning disable CS0649

using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Errors;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engine;

// ----- Test Fixtures for Per-Field Delimiter Tests -----
// FileHelpers 3.x: [FieldDelimiter] sets the delimiter that FOLLOWS the field.

/// <summary>Comma record, Field2 followed by a semicolon: <c>a,b;c</c>.</summary>
[DelimitedRecord(",")]
internal sealed class MixedDelimiterRecord
{
    [FieldOrder(1)] public string Field1 = "";
    [FieldOrder(2)] [FieldDelimiter(";")] public string Field2 = "";
    [FieldOrder(3)] public string Field3 = "";
}

/// <summary>Override on the last field: only checked as an extra delimiter after it (3.x).</summary>
[DelimitedRecord(",")]
internal sealed class SingleFieldDelimiterOverride
{
    [FieldOrder(1)] public string Field1 = "";
    [FieldOrder(2)] [FieldDelimiter(";")] public string Field2 = "";
}

/// <summary>Every boundary different: <c>a,b;c</c>, and a <c>|</c> after Field3 is an extra delimiter.</summary>
[DelimitedRecord(",")]
internal sealed class AllUniqueDelimitersRecord
{
    [FieldOrder(1)] public string Field1 = "";
    [FieldOrder(2)] [FieldDelimiter(";")] public string Field2 = "";
    [FieldOrder(3)] [FieldDelimiter("|")] public string Field3 = "";
}

/// <summary>Multi-character override: <c>a|~|b,c</c>.</summary>
[DelimitedRecord(",")]
internal sealed class MultiCharFieldDelimiterRecord
{
    [FieldOrder(1)] [FieldDelimiter("|~|")] public string Field1 = "";
    [FieldOrder(2)] public string Field2 = "";
    [FieldOrder(3)] public string Field3 = "";
}

/// <summary>Override before an optional field: <c>a,b</c> or <c>a,b|c</c>.</summary>
[DelimitedRecord(",")]
internal sealed class FieldDelimiterOptionalRecord
{
    [FieldOrder(1)] public string A = "";
    [FieldOrder(2)] [FieldDelimiter("|")] public string B = "";
    [FieldOrder(3)] [FieldOptional] public string C = "";
}

/// <summary>Quoted field with an override: quoted on write when the value contains its own delimiter.</summary>
[DelimitedRecord(",")]
internal sealed class FieldDelimiterQuotedRecord
{
    [FieldOrder(1)] public string A = "";
    [FieldOrder(2)] [FieldDelimiter("|")] [FieldQuoted(QuoteMode.OptionalForBoth)] public string B = "";
    [FieldOrder(3)] public string C = "";
}

/// <summary>Override on the last field of a physical line: <c>a,b|</c> then <c>c,d</c> (3.x layout).</summary>
[DelimitedRecord(",")]
internal sealed class FieldDelimiterInNewLineRecord
{
    [FieldOrder(1)] public string A = "";
    [FieldOrder(2)] [FieldDelimiter("|")] public string B = "";
    [FieldOrder(3)] [FieldInNewLine] public string C = "";
    [FieldOrder(4)] public string D = "";
}

/// <summary>Overrides on both lines of a [FieldInNewLine] record: <c>a,b|</c> then <c>c;d</c> (3.x layout).</summary>
[DelimitedRecord(",")]
internal sealed class FieldDelimiterOnBothLinesRecord
{
    [FieldOrder(1)] public string A = "";
    [FieldOrder(2)] [FieldDelimiter("|")] public string B = "";
    [FieldOrder(3)] [FieldInNewLine] [FieldDelimiter(";")] public string C = "";
    [FieldOrder(4)] public string D = "";
}

/// <summary>Struct record with an override (zero-allocation path).</summary>
[DelimitedRecord(",")]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal record struct FieldDelimiterStruct
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldDelimiter("|")] public int Value { get; set; }
    [FieldOrder(3)] public int Qty { get; set; }
}

/// <summary>
/// End-to-end engine tests for per-field delimiters, with FileHelpers 3.x semantics: the
/// <see cref="FieldDelimiterAttribute"/> delimiter follows its field, and the field count is validated with
/// the 3.x exception types.
/// </summary>
[Category("Engine")]
public sealed class PerFieldDelimiterEngineTests
{
    [Test]
    [DisplayName("FileHelperEngine reads the delimiter that follows the [FieldDelimiter] field")]
    public async Task ReadString_MixedDelimiters_ParsesCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<MixedDelimiterRecord>();

        // Act: Field1 is followed by the record ",", Field2 by its own ";"
        var records = engine.ReadString("a,b;c\nd,e;f");

        // Assert
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Field1).IsEqualTo("a");
        await Assert.That(records[0].Field2).IsEqualTo("b");
        await Assert.That(records[0].Field3).IsEqualTo("c");
        await Assert.That(records[1].Field1).IsEqualTo("d");
        await Assert.That(records[1].Field2).IsEqualTo("e");
        await Assert.That(records[1].Field3).IsEqualTo("f");
    }

    [Test]
    [DisplayName("WriteString writes the [FieldDelimiter] delimiter after its field (3.x)")]
    public async Task WriteString_MixedDelimiters_FormatsCorrectly()
    {
        // Arrange
        var engine = new FileHelperEngine<MixedDelimiterRecord>();

        // Act
        string output = engine.WriteString([new MixedDelimiterRecord { Field1 = "x", Field2 = "y", Field3 = "z" }]);

        // Assert
        await Assert.That(output).IsEqualTo("x,y;z\r\n");
    }

    [Test]
    [DisplayName("Round trip keeps the mixed-delimiter layout")]
    public async Task RoundTrip_MixedDelimiters_PreservesDataAndFormat()
    {
        // Arrange
        var engine = new FileHelperEngine<MixedDelimiterRecord>();

        // Act
        var records = engine.ReadString("hello,world;test");
        string output = engine.WriteString(records);
        var again = engine.ReadString(output);

        // Assert
        await Assert.That(output).IsEqualTo("hello,world;test\r\n");
        await Assert.That(again[0].Field2).IsEqualTo("world");
        await Assert.That(again[0].Field3).IsEqualTo("test");
    }

    [Test]
    [DisplayName("The old 4.x milestone layout (delimiter before the field) is rejected, not misread")]
    public async Task ReadString_DelimiterBeforeField_IsAnError()
    {
        // Arrange
        var engine = new FileHelperEngine<MixedDelimiterRecord>();

        // Act: 3.x reads "a;b" as Field1 and then misses the ";" after Field2
        var ex = Capture.Exception(() => engine.ReadString("a;b,c"));

        // Assert: 3.x "Delimiter not found after field" is a FileHelpersException
        await Assert.That(ex).IsNotNull();
        await Assert.That(ex!.GetType()).IsEqualTo(typeof(FileHelpersException));
        await Assert.That(((FileHelpersException)ex).LineNumber).IsEqualTo(1);
        await Assert.That(ex.Message).Contains("Expected 3 fields but found 2");
        await Assert.That(ex.Message).Contains("Delimiter ';' not found after field 'Field2'");
    }

    [Test]
    [DisplayName("Missing fields of a per-field-delimiter record raise FileHelpersException")]
    public async Task ReadString_MissingFields_Throws()
    {
        // Arrange
        var engine = new FileHelperEngine<MixedDelimiterRecord>();

        // Act
        var onlyFirst = Capture.Exception(() => engine.ReadString("a"));
        var twoFields = Capture.Exception(() => engine.ReadString("a,b"));

        // Assert
        await Assert.That(onlyFirst!.GetType()).IsEqualTo(typeof(FileHelpersException));
        await Assert.That(onlyFirst.Message).Contains("Delimiter ',' not found after field 'Field1'");
        await Assert.That(twoFields!.GetType()).IsEqualTo(typeof(FileHelpersException));
        await Assert.That(twoFields.Message).Contains("Delimiter ';' not found after field 'Field2'");
    }

    [Test]
    [DisplayName("An extra delimiter after the last field raises BadUsageException; other text is part of the value")]
    public async Task ReadString_ExtraDelimiter_Throws()
    {
        // Arrange
        var engine = new FileHelperEngine<MixedDelimiterRecord>();
        var lastOverride = new FileHelperEngine<AllUniqueDelimitersRecord>();

        // Act
        var extra = Capture.Exception(() => engine.ReadString("a,b;c,d"));
        var semicolonInLast = engine.ReadString("a,b;c;d");
        var extraPipe = Capture.Exception(() => lastOverride.ReadString("a,b;c|d"));
        var commaInLast = lastOverride.ReadString("a,b;c,d");

        // Assert: the last field checks its own delimiter only (3.x)
        await Assert.That(extra).IsTypeOf<BadUsageException>();
        await Assert.That(extra!.Message).Contains("Expected 3 fields but found 4");
        await Assert.That(extra.Message).Contains("Delimiter ',' found after the last field 'Field3'");
        await Assert.That(semicolonInLast[0].Field3).IsEqualTo("c;d");
        await Assert.That(extraPipe).IsTypeOf<BadUsageException>();
        await Assert.That(extraPipe!.Message).Contains("Delimiter '|' found after the last field 'Field3'");
        await Assert.That(commaInLast[0].Field3).IsEqualTo("c,d");
    }

    [Test]
    [DisplayName("[FieldDelimiter] on the last field: the record delimiter separates it, its own one is an extra delimiter")]
    public async Task SingleFieldDelimiter_OnLastField()
    {
        // Arrange
        var engine = new FileHelperEngine<SingleFieldDelimiterOverride>();

        // Act
        var records = engine.ReadString("first,second");
        string output = engine.WriteString(records);
        var oldLayout = Capture.Exception(() => engine.ReadString("first;second"));
        var extra = Capture.Exception(() => engine.ReadString("first,second;third"));

        // Assert
        await Assert.That(records[0].Field1).IsEqualTo("first");
        await Assert.That(records[0].Field2).IsEqualTo("second");
        await Assert.That(output).IsEqualTo("first,second\r\n");
        await Assert.That(oldLayout!.GetType()).IsEqualTo(typeof(FileHelpersException));
        await Assert.That(extra).IsTypeOf<BadUsageException>();
    }

    [Test]
    [DisplayName("Every boundary with its own delimiter, single- and multi-character")]
    public async Task AllUniqueDelimiters_Works()
    {
        // Arrange
        var unique = new FileHelperEngine<AllUniqueDelimitersRecord>();
        var multiChar = new FileHelperEngine<MultiCharFieldDelimiterRecord>();

        // Act
        var a = unique.ReadString("a,b;c");
        var b = multiChar.ReadString("a,x|~|b,c");
        string written = multiChar.WriteString(b);

        // Assert
        await Assert.That(a[0].Field1).IsEqualTo("a");
        await Assert.That(a[0].Field2).IsEqualTo("b");
        await Assert.That(a[0].Field3).IsEqualTo("c");
        await Assert.That(b[0].Field1).IsEqualTo("a,x");
        await Assert.That(b[0].Field2).IsEqualTo("b");
        await Assert.That(b[0].Field3).IsEqualTo("c");
        await Assert.That(unique.WriteString(a)).IsEqualTo("a,b;c\r\n");
        await Assert.That(written).IsEqualTo("a,x|~|b,c\r\n");
    }

    [Test]
    [DisplayName("[FieldOptional] after a [FieldDelimiter] field: the record may end after it")]
    public async Task OptionalAfterOverride_MayBeAbsent()
    {
        // Arrange
        var engine = new FileHelperEngine<FieldDelimiterOptionalRecord>();

        // Act
        var records = engine.ReadString("a,b\na,b|c");
        var missing = Capture.Exception(() => engine.ReadString("a"));

        // Assert
        await Assert.That(records[0].B).IsEqualTo("b");
        await Assert.That(records[0].C).IsEqualTo(string.Empty);
        await Assert.That(records[1].B).IsEqualTo("b");
        await Assert.That(records[1].C).IsEqualTo("c");
        await Assert.That(missing!.GetType()).IsEqualTo(typeof(FileHelpersException));
    }

    [Test]
    [DisplayName("[FieldQuoted] with [FieldDelimiter]: the value is quoted when it contains its own delimiter")]
    public async Task QuotedOverride_QuotesOnItsOwnDelimiter()
    {
        // Arrange
        var engine = new FileHelperEngine<FieldDelimiterQuotedRecord>();

        // Act
        string written = engine.WriteString(
        [
            new FieldDelimiterQuotedRecord { A = "a", B = "x|y", C = "c" },
            new FieldDelimiterQuotedRecord { A = "a", B = "x,y", C = "c" },
        ]);
        var read = engine.ReadString(written);

        // Assert
        await Assert.That(written).IsEqualTo("a,\"x|y\"|c\r\na,x,y|c\r\n");
        await Assert.That(read[0].B).IsEqualTo("x|y");
        await Assert.That(read[0].C).IsEqualTo("c");
        await Assert.That(read[1].A).IsEqualTo("a");
        await Assert.That(read[1].B).IsEqualTo("x,y");
        await Assert.That(read[1].C).IsEqualTo("c");
    }

    [Test]
    [DisplayName("[FieldDelimiter] on the last field of a [FieldInNewLine] line: written and read in the 3.x layout")]
    public async Task InNewLine_OverrideBeforeLineBreak()
    {
        // Arrange
        var engine = new FileHelperEngine<FieldDelimiterInNewLineRecord>();

        // Act
        string written = engine.WriteString([new FieldDelimiterInNewLineRecord { A = "a", B = "b", C = "c", D = "d" }]);
        var roundTrip = engine.ReadString(written);
        var withoutDelimiter = Capture.Exception(() => engine.ReadString("a,b\nc,d"));
        var extraField = Capture.Exception(() => engine.ReadString("a,b|x|\nc,d"));

        // Assert: the delimiter after B is its own "|", it is not part of B, and like 3.x it is required
        await Assert.That(written).IsEqualTo("a,b|\r\nc,d\r\n");
        await Assert.That(roundTrip[0].B).IsEqualTo("b");
        await Assert.That(roundTrip[0].C).IsEqualTo("c");
        await Assert.That(roundTrip[0].D).IsEqualTo("d");
        await Assert.That(withoutDelimiter!.GetType()).IsEqualTo(typeof(FileHelpersException));
        await Assert.That(withoutDelimiter.Message).Contains("Delimiter '|' not found after field 'B'");
        await Assert.That(extraField).IsTypeOf<BadUsageException>();
    }

    [Test]
    [DisplayName("[FieldInNewLine] line ending in the record delimiter instead of its own [FieldDelimiter] is an error, like 3.x")]
    public async Task InNewLine_RecordDelimiterInsteadOfOwn_IsError()
    {
        // Arrange
        var engine = new FileHelperEngine<FieldDelimiterOnBothLinesRecord> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: B must be followed by "|"; "b," is not B followed by its delimiter
        var rows = engine.ReadString("a,b,\nc;d\n");

        // Assert: 3.x reports line 1, then reads "c;d" as the start of the next record and reports it too
        await Assert.That(rows.Length).IsEqualTo(0);
        await Assert.That(string.Join(",", engine.ErrorManager.Errors.Select(e => e.LineNumber))).IsEqualTo("1,2");
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo!.GetType()).IsEqualTo(typeof(FileHelpersException));
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo!.Message).Contains("Delimiter '|' not found after field 'B'");
    }

    [Test]
    [Arguments("a|b;c,d\r\ne|f;g,h\r\n")]
    [Arguments("a|b,\r\nc;d\r\ne|f,\r\ng;h\r\n")]
    [DisplayName("Files in the 4.x milestone [FieldDelimiter] layout are rejected, not misread, also with [FieldInNewLine]")]
    public async Task InNewLine_MilestoneLayout_IsRejected(string input)
    {
        // Arrange: the milestones wrote [FieldDelimiter] before its field, and [FieldInNewLine] records on one line
        var engine = new FileHelperEngine<FieldDelimiterOnBothLinesRecord> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = engine.ReadString(input);

        // Assert: no record is read; every record is reported
        await Assert.That(rows.Length).IsEqualTo(0);
        await Assert.That(engine.ErrorManager.ErrorCount).IsGreaterThanOrEqualTo(2);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo!.Message).Contains("Delimiter '|' not found after field 'B'");
    }

    [Test]
    [DisplayName("Struct record with [FieldDelimiter]: read, validated and written")]
    public async Task StructRecord_Override()
    {
        // Arrange
        var engine = new FileHelperEngine<FieldDelimiterStruct>();

        // Act
        var records = engine.ReadString("1,2|3");
        var bad = Capture.Exception(() => engine.ReadString("1|2,3"));

        // Assert
        await Assert.That(records[0]).IsEqualTo(new FieldDelimiterStruct { Id = 1, Value = 2, Qty = 3 });
        await Assert.That(engine.WriteString(records)).IsEqualTo("1,2|3\r\n");
        await Assert.That(bad!.GetType()).IsEqualTo(typeof(FileHelpersException));
    }

    [Test]
    [DisplayName("FileHelperAsyncEngine reads per-field-delimiter records and reports their errors")]
    public async Task AsyncEngine_ReadNext_MixedDelimiters_Works()
    {
        // Arrange
        using var engine = new FileHelperAsyncEngine<MixedDelimiterRecord>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;
        engine.BeginReadString("a,b;c\na;b,c\nd,e;f");

        // Act
        var records = engine.ReadRemaining();

        // Assert: the bad line is reported, not misread
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Field3).IsEqualTo("c");
        await Assert.That(records[1].Field1).IsEqualTo("d");
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
    }
}
