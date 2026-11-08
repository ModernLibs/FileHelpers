// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using ModernLibs.FileHelpers;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

[DelimitedRecord(",")]
public sealed class PfcPlain
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string A { get; set; } = string.Empty;
    [FieldOrder(3)] public string B { get; set; } = string.Empty;
    [FieldOrder(4)] [FieldOptional] public string? C { get; set; }
}

[DelimitedRecord("||")]
public sealed class PfcMultiCharDelimiter
{
    [FieldOrder(1)] public string A { get; set; } = string.Empty;
    [FieldOrder(2)] public string B { get; set; } = string.Empty;
    [FieldOrder(3)] public string C { get; set; } = string.Empty;
}

/// <summary>
/// Field-count validation of delimited records without quoted fields or [FieldDelimiter]: the accepted
/// records, the errors and their exact messages are the same as the quote-aware scan produced in 4.8.0.
/// </summary>
[Category("ReadCore")]
public sealed class PlainFieldCountTests
{
    [Test]
    [DisplayName("Records without quoted fields: accepted counts, errors and messages are those of 4.8.0")]
    public async Task PlainRecord_CountsAndMessages()
    {
        // Arrange: three mandatory fields and one optional field; quote characters are literal text here
        var engine = new FileHelperEngine<PfcPlain> { ErrorMode = ErrorMode.SaveAndContinue };
        const string input = "1,a,b\n2,a,b,c\n3,a\n4,a,b,c,d\n5,\"x,y\",b\n6,,,\n7";

        // Act: batch and streaming reads
        var batch = engine.ReadString(input);
        var errors = engine.ErrorManager.Errors.ToArray();
        var streamed = engine.ReadSync(input).ToArray();

        // Assert: lines 1, 2, 5 and 6 are records; 3, 4 and 7 are errors with the 4.8.0 messages
        await Assert.That(string.Join(",", batch.Select(r => r.Id))).IsEqualTo("1,2,5,6");
        await Assert.That(string.Join(",", streamed.Select(r => r.Id))).IsEqualTo("1,2,5,6");
        await Assert.That(batch[2].A).IsEqualTo("\"x");
        await Assert.That(batch[2].B).IsEqualTo("y\"");
        await Assert.That(batch[2].C).IsEqualTo("b");
        await Assert.That(string.Join(",", errors.Select(e => e.LineNumber))).IsEqualTo("3,4,7");
        await Assert.That(errors[0].ExceptionInfo!.Message).IsEqualTo("Line: 3. Expected 3 to 4 fields but found 2 in line: '3,a'. Check that all delimiters are present and no fields contain unescaped delimiter characters.");
        await Assert.That(errors[1].ExceptionInfo!.Message).IsEqualTo("Line: 4. Expected 3 to 4 fields but found 5 in line: '4,a,b,c,d'. Check that all delimiters are present and no fields contain unescaped delimiter characters.");
        await Assert.That(errors[2].ExceptionInfo!.Message).IsEqualTo("Line: 7. Expected 3 to 4 fields but found 1 in line: '7'. Check that all delimiters are present and no fields contain unescaped delimiter characters.");
        await Assert.That(errors[0].ExceptionInfo).IsTypeOf<FileHelpersException>();
        await Assert.That(errors[1].ExceptionInfo).IsTypeOf<BadUsageException>();
    }

    [Test]
    [DisplayName("Multi-character delimiter: occurrences are counted without overlap, as in 4.8.0")]
    public async Task MultiCharDelimiter_NonOverlappingCount()
    {
        // Arrange: "||" delimiter; "|||" holds one delimiter and "||||" two
        var engine = new FileHelperEngine<PfcMultiCharDelimiter> { ErrorMode = ErrorMode.SaveAndContinue };
        const string input = "a||b||c\na|||b||c\na||||b\na||b\na||b||c||d";

        // Act
        var records = engine.ReadString(input);
        var errors = engine.ErrorManager.Errors.ToArray();

        // Assert: lines 1, 2 and 3 are records; 4 and 5 are errors with the 4.8.0 messages
        await Assert.That(records.Length).IsEqualTo(3);
        await Assert.That(records[1].B).IsEqualTo("|b");
        await Assert.That(records[2].B).IsEqualTo(string.Empty);
        await Assert.That(string.Join(",", errors.Select(e => e.LineNumber))).IsEqualTo("4,5");
        await Assert.That(errors[0].ExceptionInfo!.Message).IsEqualTo("Line: 4. Expected 3 fields but found 2 in line: 'a||b'. Check that all delimiters are present and no fields contain unescaped delimiter characters.");
        await Assert.That(errors[1].ExceptionInfo!.Message).IsEqualTo("Line: 5. Expected 3 fields but found 4 in line: 'a||b||c||d'. Check that all delimiters are present and no fields contain unescaped delimiter characters.");
    }
}
