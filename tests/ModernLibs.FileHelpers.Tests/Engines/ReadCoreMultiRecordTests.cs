// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Fixture types kept next to the tests that use them.
#pragma warning disable MA0048

using System.Text;
using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Errors;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

[DelimitedRecord("|")]
public sealed class RcmHeader
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2)] public string Batch { get; set; } = string.Empty;
}

[DelimitedRecord("|", MultilineMode = MultilineMode.AllowForRead)]
public sealed class RcmDetail
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2)] public int Qty { get; set; }
    [FieldOrder(3)] [FieldQuoted] public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Multi-record and master-detail engines run each record type through the shared pipeline: errors honour
/// <see cref="ErrorMode"/> with the record's own line number, multiline records follow the type's settings,
/// a line no selector matches is skipped (3.x), and an unclosed quote at end of input is reported.
/// </summary>
[Category("ReadCore")]
public sealed class ReadCoreMultiRecordTests
{
    private const string Mixed = "H|B1\nD|1|\"one\n\ntwo\"\nD|oops|\"x\"\nZ|ignored\nH|B2\nD|3|\"three\"";

    private static MultiRecordEngine CreateMulti() => new(
        MultiRecordType.For<RcmHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
        MultiRecordType.For<RcmDetail>(line => line.StartsWith("D|", StringComparison.Ordinal)));

    private static MasterDetailEngine<RcmHeader, RcmDetail> CreateMasterDetail() => new(line =>
        line.StartsWith("H|", StringComparison.Ordinal) ? RecordAction.Master :
        line.StartsWith("D|", StringComparison.Ordinal) ? RecordAction.Detail :
        RecordAction.Skip);

    [Test]
    [DisplayName("MultiRecordEngine SaveAndContinue records a parse error at its own line and keeps reading")]
    public async Task MultiRecord_SaveAndContinue_ErrorAtRecordLine()
    {
        // Arrange: bad Qty in the record starting at line 5
        var engine = CreateMulti();
        engine.ErrorMode = ErrorMode.SaveAndContinue;

        // Act: read
        var records = engine.ReadString(Mixed);

        // Assert: four records, multiline detail kept its blank line, error at line 5
        await Assert.That(records.Length).IsEqualTo(4);
        await Assert.That(((RcmDetail)records[1]).Notes).IsEqualTo("one\n\ntwo");
        await Assert.That(engine.ErrorManager.ErrorCount).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(5);
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsTypeOf<ConvertException>();
    }

    [Test]
    [DisplayName("MultiRecordEngine ThrowException reports the failing record's line, not line 1")]
    public async Task MultiRecord_Throw_ReportsRecordLine()
    {
        // Arrange: default ThrowException
        var engine = CreateMulti();

        // Act: read
        var ex = Capture.Exception(() => engine.ReadString(Mixed));

        // Assert: ConvertException located at line 5
        await Assert.That(ex).IsTypeOf<ConvertException>();
        await Assert.That(((ConvertException)ex!).LineNumber).IsEqualTo(5);
    }

    [Test]
    [DisplayName("MultiRecordAsyncEngine matches MultiRecordEngine: multiline blank line kept, error line, unclosed quote")]
    public async Task MultiRecordAsync_MatchesSync()
    {
        // Arrange: same records plus a detail whose quote never closes
        var engine = new MultiRecordAsyncEngine(
            MultiRecordType.For<RcmHeader>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<RcmDetail>(line => line.StartsWith("D|", StringComparison.Ordinal)))
        {
            ErrorMode = ErrorMode.SaveAndContinue,
        };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Mixed + "\nD|4|\"open"));

        // Act: enumerate
        var records = new List<object>();
        await foreach (var record in engine.ReadAsync(stream))
            records.Add(record);

        // Assert: same records as sync, errors at lines 5 and 9
        await Assert.That(records.Count).IsEqualTo(4);
        await Assert.That(((RcmDetail)records[1]).Notes).IsEqualTo("one\n\ntwo");
        await Assert.That(string.Join(",", engine.ErrorManager.Errors.Select(e => e.LineNumber))).IsEqualTo("5,9");
        await Assert.That(engine.ErrorManager.Errors[1].ExceptionInfo).IsTypeOf<BadUsageException>();
    }

    [Test]
    [DisplayName("MasterDetailEngine SaveAndContinue skips a bad detail with its line number and keeps grouping")]
    public async Task MasterDetail_SaveAndContinue_GroupsAndRecordsError()
    {
        // Arrange: master/detail engine in SaveAndContinue
        var engine = CreateMasterDetail();
        engine.ErrorMode = ErrorMode.SaveAndContinue;

        // Act: read
        var groups = engine.ReadString(Mixed);

        // Assert: two groups, first with one good detail, error at line 5
        await Assert.That(groups.Length).IsEqualTo(2);
        await Assert.That(groups[0].Details.Count).IsEqualTo(1);
        await Assert.That(groups[0].Details[0].Notes).IsEqualTo("one\n\ntwo");
        await Assert.That(groups[1].Master.Batch).IsEqualTo("B2");
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(5);
    }

    [Test]
    [DisplayName("MasterDetailAsyncEngine reports an unclosed quote at end of input through ErrorMode")]
    public async Task MasterDetailAsync_UnclosedQuote_IsReported()
    {
        // Arrange: last detail never closes its quote
        var engine = new MasterDetailAsyncEngine<RcmHeader, RcmDetail>(line =>
            line.StartsWith("H|", StringComparison.Ordinal) ? RecordAction.Master : RecordAction.Detail)
        {
            ErrorMode = ErrorMode.SaveAndContinue,
        };
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("H|B1\nD|1|\"a\"\nD|2|\"never"));

        // Act: enumerate
        var groups = new List<MasterDetails<RcmHeader, RcmDetail>>();
        await foreach (var group in engine.ReadAsync(stream))
            groups.Add(group);

        // Assert: one group with one detail, BadUsageException at line 3
        await Assert.That(groups.Count).IsEqualTo(1);
        await Assert.That(groups[0].Details.Count).IsEqualTo(1);
        await Assert.That(engine.ErrorManager.Errors[0].LineNumber).IsEqualTo(3);
        await Assert.That(engine.ErrorManager.Errors[0].ExceptionInfo).IsTypeOf<BadUsageException>();
    }
}
