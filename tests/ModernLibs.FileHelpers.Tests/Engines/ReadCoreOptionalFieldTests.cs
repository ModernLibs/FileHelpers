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
public sealed class RcOptionalTail
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldOptional] public string? Note { get; set; }
    [FieldOrder(4)] [FieldOptional] public string? Extra { get; set; }
}

[DelimitedRecord(",", MultilineMode = MultilineMode.AllowForRead)]
public sealed class RcOptionalMultiline
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] [FieldQuoted] public string Text { get; set; } = string.Empty;
    [FieldOrder(3)] [FieldOptional] public string? Note { get; set; }
}

/// <summary>
/// [FieldOptional]: field-count validation accepts records ending before trailing optional fields on every
/// path, including multiline; fewer than the mandatory fields or more than all fields stay errors.
/// </summary>
[Category("ReadCore")]
public sealed class ReadCoreOptionalFieldTests
{
    [Test]
    [DisplayName("[FieldOptional]: lines with 2, 3 or 4 fields are accepted; 1 and 5 fields are errors")]
    public async Task OptionalTail_FieldCountRange()
    {
        // Arrange: two mandatory and two optional fields
        var engine = new FileHelperEngine<RcOptionalTail> { ErrorMode = ErrorMode.SaveAndContinue };
        const string input = "1,a\n2,b,n\n3,c,n,x\n4\n5,e,n,x,y";

        // Act: batch and streaming reads
        var batch = engine.ReadString(input);
        int batchErrors = engine.ErrorManager.ErrorCount;
        var streamed = engine.ReadSync(input).ToArray();

        // Assert: three records each, errors on lines 4 and 5
        await Assert.That(batch.Length).IsEqualTo(3);
        await Assert.That(batchErrors).IsEqualTo(2);
        await Assert.That(streamed.Length).IsEqualTo(3);
        await Assert.That(string.Join(",", engine.ErrorManager.Errors.Select(e => e.LineNumber))).IsEqualTo("4,5");
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsTypeOf<FileHelpersException>();
        await Assert.That(engine.ErrorManager.Errors[1].ExceptionInfo).IsTypeOf<BadUsageException>();
        await Assert.That(batch[0].Note ?? string.Empty).IsEqualTo(string.Empty);
        await Assert.That(batch[2].Extra).IsEqualTo("x");
    }

    [Test]
    [DisplayName("[FieldOptional]: a multiline record may omit a trailing optional field")]
    public async Task OptionalTail_Multiline()
    {
        // Arrange: multiline quoted field followed by an optional field
        var engine = new FileHelperEngine<RcOptionalMultiline>();

        // Act: one record without and one with the optional field
        var records = engine.ReadString("1,\"a\nb\"\n2,\"c\",note");

        // Assert: both accepted
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Text).IsEqualTo("a\nb");
        await Assert.That(records[1].Note).IsEqualTo("note");
    }
}
