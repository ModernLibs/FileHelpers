// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>3.x <c>InNewLineType2</c>: two fields, a field on a new line, one more field.</summary>
[DelimitedRecord(",")]
public sealed class InlDelimited
{
    [FieldOrder(1)] public byte Field1 { get; set; }
    [FieldOrder(2)] public long Field2 { get; set; }
    [FieldOrder(3)] [FieldInNewLine] public string IpAddress { get; set; } = string.Empty;
    [FieldOrder(4)] public int FieldLast { get; set; }
}

/// <summary>3.x <c>InNewLineFixedType2</c>.</summary>
[FixedLengthRecord]
public sealed class InlFixed
{
    [FieldOrder(1)] [FieldFixedLength(3)] public byte Field1 { get; set; }
    [FieldOrder(2)] [FieldFixedLength(8)] public long Field2 { get; set; }
    [FieldOrder(3)] [FieldFixedLength(15)] [FieldInNewLine] public string IpAddress { get; set; } = string.Empty;
    [FieldOrder(4)] [FieldFixedLength(3)] public int FieldLast { get; set; }
}

/// <summary>Fixed-length two-line record with comment lines between records.</summary>
[FixedLengthRecord]
[IgnoreCommentedLines('#')]
public sealed class InlFixedCommented
{
    [FieldOrder(1)] [FieldFixedLength(3)] public string A { get; set; } = string.Empty;
    [FieldOrder(2)] [FieldFixedLength(3)] [FieldInNewLine] public string B { get; set; } = string.Empty;
}

/// <summary>Delimited two-line record with every record-level filter.</summary>
[DelimitedRecord(",")]
[IgnoreEmptyLines]
[IgnoreCommentedLines('#')]
[ConditionalRecord(RecordCondition.ExcludeIfBegins, "SKIP")]
public sealed class InlDelimitedFiltered
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldInNewLine] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public int Qty { get; set; }
}

/// <summary>Two-line record of strings; a record whose first line contains Z is excluded.</summary>
[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.ExcludeIfContains, "Z")]
public sealed class InlConditional
{
    [FieldOrder(1)] public string A { get; set; } = string.Empty;
    [FieldOrder(2)] [FieldInNewLine] public string B { get; set; } = string.Empty;
}

/// <summary>Delimited record whose first field is on a new line: each record starts with a blank line (3.x).</summary>
[DelimitedRecord(",")]
public sealed class InlFirstDelimited
{
    [FieldOrder(1)] [FieldInNewLine] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
}

/// <summary>Quote-aware variant: first field on a new line, quoted second field.</summary>
[DelimitedRecord(",")]
public sealed class InlFirstQuoted
{
    [FieldOrder(1)] [FieldInNewLine] public int Id { get; set; }
    [FieldOrder(2)] [FieldQuoted] public string Name { get; set; } = string.Empty;
}

/// <summary>Fixed-length record whose first field is on a new line, plus a second new-line field.</summary>
[FixedLengthRecord]
public sealed class InlFirstFixed
{
    [FieldOrder(1)] [FieldInNewLine] [FieldFixedLength(3)] public int Id { get; set; }
    [FieldOrder(2)] [FieldFixedLength(5)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldInNewLine] [FieldFixedLength(2)] public string Code { get; set; } = string.Empty;
}

/// <summary>Quoted value that may span lines, before a new-line field.</summary>
[DelimitedRecord(",")]
public sealed class InlQuotedMultiline
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.AllowForBoth)] public string Notes { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldInNewLine] public string Tail { get; set; } = string.Empty;
    [FieldOrder(4)] public int Qty { get; set; }
}

/// <summary>Quoted value that may span lines, as the new-line field itself.</summary>
[DelimitedRecord(",")]
public sealed class InlQuotedMultilineOnNewLine
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldInNewLine] [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.AllowForBoth)] public string Notes { get; set; } = string.Empty;
    [FieldOrder(3)] public int Qty { get; set; }
}

/// <summary>The new-line field must be quoted.</summary>
[DelimitedRecord(",")]
public sealed class InlAlwaysQuotedOnNewLine
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldInNewLine] [FieldQuoted('"', QuoteMode.AlwaysQuoted, MultilineMode.AllowForBoth)] public string Notes { get; set; } = string.Empty;
    [FieldOrder(3)] public int Qty { get; set; }
}

/// <summary>Quoted value that must stay on one line, before a new-line field.</summary>
[DelimitedRecord(",")]
public sealed class InlQuotedSingleLine
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.NotAllow)] public string Notes { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldInNewLine] public string Tail { get; set; } = string.Empty;
}

/// <summary>Only the new-line field is quoted: a quote on the first line is literal text.</summary>
[DelimitedRecord(",")]
public sealed class InlQuotedSecondLineOnly
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Raw { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldInNewLine] [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.AllowForBoth)] public string Notes { get; set; } = string.Empty;
}

/// <summary>The new-line field is optional: the line before it may end without the delimiter (3.x).</summary>
[DelimitedRecord(",")]
public sealed class InlOptionalOnNewLine
{
    [FieldOrder(1)] public string A { get; set; } = string.Empty;
    [FieldOrder(2)] public string B { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldInNewLine] [FieldOptional] public string C { get; set; } = string.Empty;
}

/// <summary>[FieldDelimiter] before the line break, for recovery checks.</summary>
[DelimitedRecord(",")]
public sealed class InlOverrideRecovery
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2)] [FieldDelimiter("|")] public string X { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldInNewLine] public string Y { get; set; } = string.Empty;
    [FieldOrder(4)] public int N { get; set; }
}

/// <summary>Three physical lines, the last with a quoted value that may span lines.</summary>
[DelimitedRecord(",")]
public sealed class InlThreeLines
{
    [FieldOrder(1)] public string A { get; set; } = string.Empty;
    [FieldOrder(2)] [FieldInNewLine] public string B { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldInNewLine] [FieldQuoted('"', QuoteMode.OptionalForBoth, MultilineMode.AllowForBoth)] public string C { get; set; } = string.Empty;
    [FieldOrder(4)] public int D { get; set; }
}

/// <summary>
/// <see cref="FieldInNewLineAttribute"/> read and write: the FileHelpers 3.x layout (delimiter before the line
/// break) is read and written, records filtered by comment / empty / condition rules are skipped before
/// grouping (3.x filters the first line of a record only), and written output reads back.
/// </summary>
[Category("FieldInNewLine")]
public sealed class FieldInNewLineReadWriteTests
{
    // 3.x test data Good/InNewLine2.txt
    private const string ThreeXDelimited =
        "107,37052652033,\r\n166.90.252.2,111\r\n82,37052150133,\r\n67.105.166.35,222\r\n181,37052343840,\r\n67.105.166.35,333\r\n";

    private static string Summary(InlDelimited[] rows) =>
        string.Join("|", rows.Select(r => $"{r.Field1}/{r.Field2}/{r.IpAddress}/{r.FieldLast}"));

    private const string ExpectedRows =
        "107/37052652033/166.90.252.2/111|82/37052150133/67.105.166.35/222|181/37052343840/67.105.166.35/333";

    [Test]
    [DisplayName("Delimited: the 3.x layout (delimiter before the line break) is read")]
    public async Task Delimited_ThreeXLayout_IsRead()
    {
        // Arrange
        var engine = new FileHelperEngine<InlDelimited>();

        // Act
        var rows = engine.ReadString(ThreeXDelimited);

        // Assert
        await Assert.That(Summary(rows)).IsEqualTo(ExpectedRows);
    }

    [Test]
    [DisplayName("Delimited: a line break without the delimiter before it is an error, like 3.x")]
    public async Task Delimited_LineBreakWithoutDelimiter_IsError()
    {
        // Arrange
        var engine = new FileHelperEngine<InlDelimited> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = engine.ReadString(ThreeXDelimited.Replace(",\r\n", "\r\n", StringComparison.Ordinal));

        // Assert: 3.x requires the delimiter after Field2 ("Delimiter ',' not found after field")
        await Assert.That(rows.Length).IsEqualTo(0);
        var first = engine.ErrorManager.Errors[0];
        await Assert.That(first.LineNumber).IsEqualTo(1);
        await Assert.That(first.ExceptionInfo!.GetType()).IsEqualTo(typeof(FileHelpersException));
        await Assert.That(first.ExceptionInfo.Message).Contains("Delimiter ',' not found after field 'Field2'");
    }

    [Test]
    [DisplayName("Delimited: the delimiter before the line break may be omitted when the new-line field is optional (3.x)")]
    public async Task Delimited_LineBreakWithoutDelimiter_BeforeOptionalField_IsRead()
    {
        // Arrange
        var engine = new FileHelperEngine<InlOptionalOnNewLine>();

        // Act
        var rows = engine.ReadString("1,2\r\nx\r\n3,4,\r\ny\r\n");

        // Assert
        await Assert.That(string.Join("|", rows.Select(r => $"{r.A}/{r.B}/{r.C}"))).IsEqualTo("1/2/x|3/4/y");
    }

    [Test]
    [DisplayName("Delimited: a quoted value may end the line without the delimiter after it (3.x)")]
    public async Task Delimited_QuotedValueAtLineEnd_WithoutDelimiter_IsRead()
    {
        // Arrange: 3.x accepts the end of the line right after a closing quote instead of the delimiter
        var engine = new FileHelperEngine<InlQuotedMultiline>();

        // Act
        var rows = engine.ReadString("1,\"x\ny\"\nc,5\n2,\"z\"\nd,6\n");

        // Assert
        await Assert.That(Summary(rows)).IsEqualTo("1/x\\ny/c/5|2/z/d/6");
    }

    [Test]
    [DisplayName("After a line without its delimiter, the next lines start the next record (3.x recovery)")]
    public async Task MissingLineDelimiter_RecoversLike3x()
    {
        // Arrange
        var engine = new FileHelperEngine<InlOverrideRecovery> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: line 1 has an extra field, line 2 lacks the "|" after X, lines 3-4 are a good record
        var rows = engine.ReadString("N,z|q|\r\nw,2\r\nN,c|\r\nd,3\r\n");

        // Assert
        await Assert.That(string.Join("|", rows.Select(r => $"{r.Kind}/{r.X}/{r.Y}/{r.N}"))).IsEqualTo("N/c/d/3");
        await Assert.That(string.Join(",", engine.ErrorManager.Errors.Select(e => e.LineNumber))).IsEqualTo("1,2");
        await Assert.That(engine.ErrorManager.Errors[1].ExceptionInfo!.Message).Contains("Delimiter '|' not found after field 'X'");
    }

    [Test]
    [DisplayName("A line without its delimiter is not completed by a following quoted line (no garbage record)")]
    public async Task MissingLineDelimiter_BeforeQuotedLine_NoGarbageRecord()
    {
        // Arrange
        var engine = new FileHelperEngine<InlThreeLines> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = engine.ReadString("a,x\nb,\n\"p\nq\",1\nz,\ny,\n\"r\",2\n");

        // Assert: same errors and record as 3.x
        await Assert.That(string.Join("|", rows.Select(r => $"{r.A}/{r.B}/{r.C}/{r.D}"))).IsEqualTo("z/y/r/2");
        await Assert.That(string.Join(",", engine.ErrorManager.Errors.Select(e => e.LineNumber))).IsEqualTo("1,3,4");
    }

    [Test]
    [DisplayName("Delimited: write puts the field on a new line (3.x layout) and reads back")]
    public async Task Delimited_Write_UsesNewLineAndRoundTrips()
    {
        // Arrange
        var engine = new FileHelperEngine<InlDelimited>();
        var rows = engine.ReadString(ThreeXDelimited);

        // Act
        string written = engine.WriteString(rows);
        var back = engine.ReadString(written);

        // Assert: byte-identical to the 3.x file, and the round trip keeps the values
        await Assert.That(written).IsEqualTo(ThreeXDelimited);
        await Assert.That(Summary(back)).IsEqualTo(ExpectedRows);
    }

    [Test]
    [DisplayName("Delimited: the line break inside a record follows NewLineForWrite")]
    public async Task Delimited_Write_HonoursNewLineForWrite()
    {
        // Arrange
        var engine = new FileHelperEngine<InlDelimited> { NewLineForWrite = "\n" };
        var row = new InlDelimited { Field1 = 1, Field2 = 2, IpAddress = "ip", FieldLast = 3 };

        // Act
        string written = engine.WriteString([row]);

        // Assert
        await Assert.That(written).IsEqualTo("1,2,\nip,3\n");
    }

    [Test]
    [DisplayName("Delimited: async engine WriteNext writes the 3.x layout")]
    public async Task Delimited_AsyncEngine_WriteNext()
    {
        // Arrange
        using var engine = new FileHelperAsyncEngine<InlDelimited>();
        engine.BeginWriteString();

        // Act
        engine.WriteNext(new InlDelimited { Field1 = 1, Field2 = 2, IpAddress = "ip", FieldLast = 3 });
        string written = engine.EndWriteString();

        // Assert
        await Assert.That(written).IsEqualTo("1,2,\r\nip,3\r\n");
    }

    [Test]
    [Arguments("1,2,x\r\nip,3", "1,2")]
    [Arguments("1\r\nip,3", "1,2")]
    [Arguments("1,2,\r\nip,3,4", "2")]
    [Arguments("1,2,\r\nip", "2")]
    [DisplayName("Delimited: a physical line with the wrong number of fields is an error on that line")]
    public async Task Delimited_WrongFieldCountPerLine_IsError(string input, string errorLines)
    {
        // Arrange
        var engine = new FileHelperEngine<InlDelimited> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = engine.ReadString(input);

        // Assert: nothing read; the error is on the failing line, and after a bad first line the second line
        // starts the next (incomplete) record, as in 3.x
        await Assert.That(rows.Length).IsEqualTo(0);
        await Assert.That(string.Join(",", engine.ErrorManager.Errors.Select(e => e.LineNumber))).IsEqualTo(errorLines);
    }

    [Test]
    [DisplayName("Fixed: write puts the field on a new line and reads back")]
    public async Task Fixed_Write_UsesNewLineAndRoundTrips()
    {
        // Arrange
        var engine = new FileHelperEngine<InlFixed>();
        var row = new InlFixed { Field1 = 107, Field2 = 37052652, IpAddress = "166.090.252.002", FieldLast = 111 };

        // Act
        string written = engine.WriteString([row]);
        var back = engine.ReadString(written);

        // Assert
        await Assert.That(written).IsEqualTo("10737052652\r\n166.090.252.002111\r\n");
        await Assert.That(back.Length).IsEqualTo(1);
        await Assert.That(back[0].IpAddress).IsEqualTo("166.090.252.002");
        await Assert.That(back[0].FieldLast).IsEqualTo(111);
    }

    [Test]
    [DisplayName("Fixed: a comment line between multi-line records is skipped, not grouped (3.x)")]
    public async Task Fixed_CommentBetweenRecords_IsSkipped()
    {
        // Arrange: a comment line between two multi-line records
        var engine = new FileHelperEngine<InlFixedCommented> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = engine.ReadString("aaa\nbbb\n#cc\nccc\nddd\n");

        // Assert: two records, no error
        await Assert.That(string.Join("|", rows.Select(r => r.A + "/" + r.B))).IsEqualTo("aaa/bbb|ccc/ddd");
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(0);
        await Assert.That(engine.LineNumber).IsEqualTo(5);
    }

    [Test]
    [DisplayName("Delimited: empty, comment and condition-excluded lines before a record are skipped")]
    public async Task Delimited_FilteredLinesBetweenRecords_AreSkipped()
    {
        // Arrange
        var engine = new FileHelperEngine<InlDelimitedFiltered> { ErrorMode = ErrorMode.SaveAndContinue };
        const string input = "# head\r\n1,\r\nalpha,10\r\n\r\nSKIP me\r\n# c\r\n2,\r\nbeta,20\r\n";

        // Act
        var rows = engine.ReadString(input);

        // Assert
        await Assert.That(string.Join("|", rows.Select(r => $"{r.Id}/{r.Name}/{r.Qty}"))).IsEqualTo("1/alpha/10|2/beta/20");
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(0);
    }

    [Test]
    [DisplayName("Delimited, first field on a new line: the record starts with a blank line (3.x layout)")]
    public async Task FirstFieldInNewLine_Delimited_ReadsAfterBlankLine()
    {
        // Arrange: 3.x reads the field from the line after the record's (blank) first line
        var engine = new FileHelperEngine<InlFirstDelimited>();

        // Act
        var rows = engine.ReadString("\r\n1,alpha\r\n \t\r\n2,beta\r\n");

        // Assert: a whitespace-only first line is accepted as blank, like 3.x
        await Assert.That(string.Join("|", rows.Select(r => $"{r.Id}/{r.Name}"))).IsEqualTo("1/alpha|2/beta");
    }

    [Test]
    [DisplayName("Delimited, first field on a new line: write starts each record with a line break and reads back")]
    public async Task FirstFieldInNewLine_Delimited_WriteRoundTrips()
    {
        // Arrange
        var engine = new FileHelperEngine<InlFirstDelimited>();
        InlFirstDelimited[] rows = [new() { Id = 1, Name = "alpha" }, new() { Id = 2, Name = "beta" }];

        // Act
        string written = engine.WriteString(rows);
        var back = engine.ReadString(written);

        // Assert
        await Assert.That(written).IsEqualTo("\r\n1,alpha\r\n\r\n2,beta\r\n");
        await Assert.That(string.Join("|", back.Select(r => $"{r.Id}/{r.Name}"))).IsEqualTo("1/alpha|2/beta");
    }

    [Test]
    [DisplayName("Delimited, first field on a new line, quoted field: write and read back")]
    public async Task FirstFieldInNewLine_DelimitedQuoted_WriteRoundTrips()
    {
        // Arrange
        var engine = new FileHelperEngine<InlFirstQuoted>();
        InlFirstQuoted[] rows = [new() { Id = 1, Name = "a,b" }, new() { Id = 2, Name = "c" }];

        // Act
        string written = engine.WriteString(rows);
        var back = engine.ReadString(written);

        // Assert
        await Assert.That(written).IsEqualTo("\r\n1,\"a,b\"\r\n\r\n2,\"c\"\r\n");
        await Assert.That(string.Join("|", back.Select(r => $"{r.Id}/{r.Name}"))).IsEqualTo("1/a,b|2/c");
    }

    [Test]
    [DisplayName("Fixed, first field on a new line: write starts each record with a line break and reads back")]
    public async Task FirstFieldInNewLine_Fixed_WriteRoundTrips()
    {
        // Arrange
        var engine = new FileHelperEngine<InlFirstFixed>();
        InlFirstFixed[] rows = [new() { Id = 1, Name = "alpha", Code = "AB" }, new() { Id = 22, Name = "bravo", Code = "CD" }];

        // Act
        string written = engine.WriteString(rows);
        var back = engine.ReadString(written);

        // Assert
        await Assert.That(written).IsEqualTo("\r\n1  alpha\r\nAB\r\n\r\n22 bravo\r\nCD\r\n");
        await Assert.That(string.Join("|", back.Select(r => $"{r.Id}/{r.Name}/{r.Code}"))).IsEqualTo("1/alpha/AB|22/bravo/CD");
    }

    [Test]
    [Arguments("x\r\n1,alpha\r\n")]
    [Arguments("1,alpha\r\n2,beta\r\n")]
    [DisplayName("Delimited, first field on a new line: text before the field's line is an error (3.x message)")]
    public async Task FirstFieldInNewLine_Delimited_TextBeforeField_IsError(string input)
    {
        // Arrange
        var engine = new FileHelperEngine<InlFirstDelimited> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = engine.ReadString(input);

        // Assert: like 3.x, line 2 then starts the next record, which is incomplete
        await Assert.That(rows.Length).IsEqualTo(0);
        await Assert.That(string.Join(",", engine.ErrorManager.Errors.Select(e => e.LineNumber))).IsEqualTo("1,2");
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsTypeOf<BadUsageException>();
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo!.Message).Contains("found before the new line of the field: Id");
    }

    [Test]
    [DisplayName("Fixed, first field on a new line: text before the field's line is an error (3.x message)")]
    public async Task FirstFieldInNewLine_Fixed_TextBeforeField_IsError()
    {
        // Arrange
        var engine = new FileHelperEngine<InlFirstFixed> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = engine.ReadString("1  alpha\r\nAB\r\n\r\n");

        // Assert
        await Assert.That(rows.Length).IsEqualTo(0);
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsTypeOf<BadUsageException>();
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo!.Message).Contains("found before the new line of the field: Id");
    }

    private static string Summary(InlQuotedMultiline[] rows) =>
        string.Join("|", rows.Select(r => $"{r.Id}/{r.Notes.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal)}/{r.Tail}/{r.Qty}"));

    [Test]
    [DisplayName("Quoted multiline value before a new-line field: written in the 3.x layout and read back")]
    public async Task QuotedMultiline_BeforeNewLineField_RoundTrips()
    {
        // Arrange
        var engine = new FileHelperEngine<InlQuotedMultiline>();
        var rows = new[]
        {
            new InlQuotedMultiline { Id = 1, Notes = "x\r\ny", Tail = "c", Qty = 5 },
            new InlQuotedMultiline { Id = 2, Notes = "plain", Tail = "d", Qty = 6 },
        };

        // Act
        string written = engine.WriteString(rows);
        var back = engine.ReadString(written);

        // Assert
        await Assert.That(written).IsEqualTo("1,\"x\r\ny\",\r\nc,5\r\n2,plain,\r\nd,6\r\n");
        await Assert.That(Summary(back)).IsEqualTo("1/x\\r\\ny/c/5|2/plain/d/6");
    }

    [Test]
    [DisplayName("Quoted multiline value before a new-line field: 3.x files are read (several lines, delimiters and quotes inside)")]
    public async Task QuotedMultiline_BeforeNewLineField_ThreeXInput()
    {
        // Arrange: the value spans three lines; another contains the delimiter, a line break and an escaped quote
        var engine = new FileHelperEngine<InlQuotedMultiline>();
        const string input = "1,\"x\ny\nz\",\nc,5\n2,\"a,\n\"\"b\"\"\",\nd,6\n";

        // Act
        var rows = engine.ReadString(input);

        // Assert
        await Assert.That(Summary(rows)).IsEqualTo("1/x\\ny\\nz/c/5|2/a,\\n\"b\"/d/6");
        await Assert.That(engine.LineNumber).IsEqualTo(7);
    }

    [Test]
    [DisplayName("Quoted multiline value on the new-line field: written and read back")]
    public async Task QuotedMultiline_OnNewLineField_RoundTrips()
    {
        // Arrange
        var engine = new FileHelperEngine<InlQuotedMultilineOnNewLine>();
        var row = new InlQuotedMultilineOnNewLine { Id = 1, Notes = "x\r\ny", Qty = 2 };

        // Act
        string written = engine.WriteString([row, new InlQuotedMultilineOnNewLine { Id = 3, Notes = "z", Qty = 4 }]);
        var back = engine.ReadString(written);

        // Assert
        await Assert.That(written).IsEqualTo("1,\r\n\"x\r\ny\",2\r\n3,\r\nz,4\r\n");
        await Assert.That(string.Join("|", back.Select(r => $"{r.Id}/{r.Notes.Replace("\r\n", "\\n", StringComparison.Ordinal)}/{r.Qty}"))).IsEqualTo("1/x\\ny/2|3/z/4");
    }

    [Test]
    [DisplayName("Quoted value that must stay on one line: a line break inside the quotes is an unclosed quote")]
    public async Task QuotedSingleLine_LineBreakInsideQuotes_IsError()
    {
        // Arrange
        var engine = new FileHelperEngine<InlQuotedSingleLine> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = engine.ReadString("1,\"x\ny\",\nc\n");

        // Assert: reported, never read as a record
        await Assert.That(rows.Length).IsEqualTo(0);
        await Assert.That(engine.ErrorManager.ErrorCount).IsGreaterThanOrEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsTypeOf<BadUsageException>();
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(1);
    }

    [Test]
    [DisplayName("A conversion error in the second line of a record is reported on that physical line, with its column (3.x)")]
    public async Task ConversionErrorInSecondLine_ReportsPhysicalLine()
    {
        // Arrange
        var engine = new FileHelperEngine<InlDelimited> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: FieldLast of the second record is not a number
        var rows = engine.ReadString("1,2,\r\nip,3\r\n4,5,\r\nip,X\r\n6,7,\r\nip,8\r\n");

        // Assert
        await Assert.That(rows.Length).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        var error = engine.ErrorManager.Errors[0];
        await Assert.That(error.LineNumber).IsEqualTo(4);
        await Assert.That(error.ColumnIndex).IsEqualTo(3);
        await Assert.That(error.ExceptionInfo!.Message).StartsWith("Line: 4. Column: 4.");
        await Assert.That(error.RecordString).IsEqualTo("4,5,\nip,X");
    }

    [Test]
    [DisplayName("An empty value-type field in the second line is reported on that physical line, with its column (3.x)")]
    public async Task NullValueInSecondLine_ReportsPhysicalLine()
    {
        // Arrange
        var engine = new FileHelperEngine<InlDelimited> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: FieldLast of the second record is empty
        var rows = engine.ReadString("1,2,\r\nip,3\r\n4,5,\r\nip,\r\n6,7,\r\nip,8\r\n");

        // Assert
        await Assert.That(rows.Length).IsEqualTo(2);
        var error = engine.ErrorManager.Errors.Single();
        await Assert.That(error.ExceptionInfo).IsTypeOf<NullValueNotFoundException>();
        await Assert.That(error.LineNumber).IsEqualTo(4);
        await Assert.That(((FileHelpersException)error.ExceptionInfo!).ColumnNumber).IsEqualTo(4);
    }

    [Test]
    [Arguments("1,\r\n\"x\"y,2\r\n", "is not just before the separator")]
    [Arguments("1,\r\nx,2\r\n", "does not begin with the quote char")]
    [DisplayName("A quote error in the second line is reported on that physical line, with its column (3.x)")]
    public async Task QuoteErrorInSecondLine_ReportsPhysicalLine(string input, string message)
    {
        // Arrange
        var engine = new FileHelperEngine<InlAlwaysQuotedOnNewLine> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        _ = engine.ReadString(input);

        // Assert
        var error = engine.ErrorManager.Errors.Single();
        await Assert.That(error.ExceptionInfo).IsTypeOf<BadUsageException>();
        await Assert.That(error.ExceptionInfo!.Message).Contains(message);
        await Assert.That(error.LineNumber).IsEqualTo(2);
        await Assert.That(((FileHelpersException)error.ExceptionInfo).ColumnNumber).IsEqualTo(1);
    }

    [Test]
    [DisplayName("A conversion error after a multiline quoted value is reported on the physical line of the field")]
    public async Task ConversionErrorAfterMultilineValue_ReportsPhysicalLine()
    {
        // Arrange
        var engine = new FileHelperEngine<InlQuotedMultiline> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: Qty is on physical line 4 (the quoted value spans lines 1 to 3)
        _ = engine.ReadString("1,\"x\ny\nz\",\nc,oops\n");

        // Assert
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsTypeOf<ConvertException>();
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(4);
    }

    [Test]
    [DisplayName("A wrong second line is reported on its physical line and the record is skipped")]
    public async Task LayoutErrorInSecondLine_ReportsPhysicalLine()
    {
        // Arrange
        var engine = new FileHelperEngine<InlDelimited> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: the second line of the first record has an extra field
        var rows = engine.ReadString("1,2,\r\nip,3,extra\r\n4,5,\r\nip,6\r\n");

        // Assert
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Field1).IsEqualTo((byte)4);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsTypeOf<BadUsageException>();
    }

    [Test]
    [DisplayName("After a wrong first line, the next lines start the next record, like 3.x")]
    public async Task LayoutErrorInFirstLine_NextLinesStartNextRecord()
    {
        // Arrange
        var engine = new FileHelperEngine<InlDelimited> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: "4,x,y," has too many fields, so 3.x reads "ip,6" as the first line of the next record
        var rows = engine.ReadString("1,2,\r\nip,3\r\n4,x,y,\r\n7,8,\r\nip,9\r\n");

        // Assert: the error is on line 3 and line 4 starts the next record
        await Assert.That(Summary(rows)).IsEqualTo("1/2/ip/3|7/8/ip/9");
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(3);
    }

    [Test]
    [DisplayName("A record after a filtered first line recovers like 3.x: the error is on the unfiltered line, the next lines form a record")]
    public async Task ConditionFilteredFirstLine_RecoversLike3x()
    {
        // Arrange: "Z," is excluded, so 3.x reads "a" as the first line of a record; without the delimiter
        // before the line break 3.x reports it and reads "b," / "c" as the next record
        var engine = new FileHelperEngine<InlConditional> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act
        var rows = engine.ReadString("x,\ny\nZ,\na\nb,\nc\n");

        // Assert
        await Assert.That(string.Join("|", rows.Select(r => $"{r.A}/{r.B}"))).IsEqualTo("x/y|b/c");
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        var error = engine.ErrorManager.Errors[0];
        await Assert.That(error.LineNumber).IsEqualTo(4);
        await Assert.That(error.ExceptionInfo!.GetType()).IsEqualTo(typeof(FileHelpersException));
        await Assert.That(error.ExceptionInfo.Message).Contains("Delimiter ',' not found after field 'A'");
    }

    [Test]
    [DisplayName("The recovery after a wrong line works the same with ThrowException and the async engine")]
    public async Task Recovery_AsyncEngineThrowException()
    {
        // Arrange
        using var engine = new FileHelperAsyncEngine<InlConditional>();
        engine.BeginReadString("x,\ny\nZ,\na\nb,\nc\n");
        var got = new List<string>();

        // Act: pull with ReadNext, catching the error
        for (int i = 0; i < 4; i++)
        {
            try
            {
                got.Add(engine.ReadNext() is { } r ? $"{r.A}/{r.B}" : "end");
            }
            catch (FileHelpersException ex)
            {
                got.Add("error@" + ex.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        // Assert
        await Assert.That(string.Join(",", got)).IsEqualTo("x/y,error@4,b/c,end");
    }

    [Test]
    [DisplayName("A quote at the start of a field without [FieldQuoted] is literal text, also in [FieldInNewLine] records")]
    public async Task QuoteInUnquotedField_IsLiteral()
    {
        // Arrange: Raw is not quoted, so its quote does not open a value spanning the next line (3.x)
        var engine = new FileHelperEngine<InlQuotedSecondLineOnly>();

        // Act
        var rows = engine.ReadString("1,\"x,\n\"multi\nline\"\n2,y,\nz\n");

        // Assert
        await Assert.That(rows.Length).IsEqualTo(2);
        await Assert.That(rows[0].Raw).IsEqualTo("\"x");
        await Assert.That(rows[0].Notes).IsEqualTo("multi\nline");
        await Assert.That(rows[1].Raw).IsEqualTo("y");
        await Assert.That(rows[1].Notes).IsEqualTo("z");
    }
}
