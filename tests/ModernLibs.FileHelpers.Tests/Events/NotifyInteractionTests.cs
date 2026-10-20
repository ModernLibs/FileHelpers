// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional.
#pragma warning disable MA0048
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Events;

[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.IncludeIfBegins, "ORD")]
internal sealed class NotifyReadConditionalRecord : INotifyRead<NotifyReadConditionalRecord>
{
    [FieldOrder(1)]
    public string Id { get; set; } = "";

    [FieldOrder(2)]
    public string Name { get; set; } = "";

    public void BeforeRead(BeforeReadEventArgs<NotifyReadConditionalRecord> e) { }
    public void AfterRead(AfterReadEventArgs<NotifyReadConditionalRecord> e) => ParsedRecords.Add(this);

    public static List<NotifyReadConditionalRecord> ParsedRecords { get; set; } = new();
}

[FixedLengthRecord(FixedMode.ExactLength)]
internal sealed class NotifyWriteAlignedRecord : INotifyWrite<NotifyWriteAlignedRecord>
{
    [FieldFixedLength(10)]
    [FieldAlign(AlignMode.Left)]
    public string Name { get; set; } = "";

    public void BeforeWrite(BeforeWriteEventArgs<NotifyWriteAlignedRecord> e) { }
    public void AfterWrite(AfterWriteEventArgs<NotifyWriteAlignedRecord> e) => WrittenLines.Add(e.RecordLine);

    public static List<string> WrittenLines { get; set; } = new();
}

[DelimitedRecord(",")]
internal sealed class NotifyWriteTrimmedRecord : INotifyWrite<NotifyWriteTrimmedRecord>
{
    [FieldOrder(1)]
    [FieldTrim(TrimMode.Both)]
    public string Name { get; set; } = "";

    public void BeforeWrite(BeforeWriteEventArgs<NotifyWriteTrimmedRecord> e) { }
    public void AfterWrite(AfterWriteEventArgs<NotifyWriteTrimmedRecord> e) => WrittenLines.Add(e.RecordLine);

    public static List<string> WrittenLines { get; set; } = new();
}

[DelimitedRecord(",")]
internal sealed class ThrowingAfterReadRecord : INotifyRead<ThrowingAfterReadRecord>
{
    [FieldOrder(1)]
    public string Value { get; set; } = "";

    public void BeforeRead(BeforeReadEventArgs<ThrowingAfterReadRecord> e) { }
    public void AfterRead(AfterReadEventArgs<ThrowingAfterReadRecord> e) => throw new InvalidOperationException("Test exception");
}

[DelimitedRecord(",")]
internal sealed class ThrowingAfterWriteRecord : INotifyWrite<ThrowingAfterWriteRecord>
{
    [FieldOrder(1)]
    public string Value { get; set; } = "";

    public void BeforeWrite(BeforeWriteEventArgs<ThrowingAfterWriteRecord> e) { }
    public void AfterWrite(AfterWriteEventArgs<ThrowingAfterWriteRecord> e) => throw new InvalidOperationException("Test exception");
}

[DelimitedRecord(",")]
internal sealed class EmptyFileNotifyRecord : INotifyRead<EmptyFileNotifyRecord>
{
    [FieldOrder(1)]
    public string Value { get; set; } = "";

    public void BeforeRead(BeforeReadEventArgs<EmptyFileNotifyRecord> e) { }
    public void AfterRead(AfterReadEventArgs<EmptyFileNotifyRecord> e) => ParsedCount++;

    public static int ParsedCount { get; set; }
}

[DelimitedRecord(",")]
internal sealed class WriteFileNotifyRecord : INotifyWrite<WriteFileNotifyRecord>
{
    [FieldOrder(1)]
    public string Value { get; set; } = "";

    public void BeforeWrite(BeforeWriteEventArgs<WriteFileNotifyRecord> e) { }
    public void AfterWrite(AfterWriteEventArgs<WriteFileNotifyRecord> e) => WriteCount++;

    public static int WriteCount { get; set; }
}

[Category("INotify")]
public sealed class NotifyInteractionTests
{
    [Test]
    [DisplayName("INotifyRead + ConditionalRecord: AfterRead fires only for matching records")]
    public async Task NotifyReadPlusConditional_FiresOnlyForMatches()
    {
        // Arrange: Input with matching and non-matching lines
        NotifyReadConditionalRecord.ParsedRecords = new();
        var engine = new FileHelperEngine<NotifyReadConditionalRecord>();
        var input = "ORD001,Alice\nINV001,Bob\nORD002,Charlie";

        // Act: Parse
        var records = engine.ReadString(input);

        // Assert: AfterRead fired only for ORD records (2 out of 3)
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(NotifyReadConditionalRecord.ParsedRecords.Count).IsEqualTo(2);
    }

    [Test]
    [DisplayName("INotifyWrite + FieldAlign: AfterWrite receives aligned line")]
    public async Task NotifyWritePlusAlign_ReceivesAlignedLine()
    {
        // Arrange: Record with left alignment
        NotifyWriteAlignedRecord.WrittenLines = new();
        var engine = new FileHelperEngine<NotifyWriteAlignedRecord>();
        var records = new[] { new NotifyWriteAlignedRecord { Name = "ABC" } };

        // Act: Write
        engine.WriteString(records);

        // Assert: AfterWrite received the aligned line (ABC + 7 spaces = 10 chars)
        await Assert.That(NotifyWriteAlignedRecord.WrittenLines[0]).IsEqualTo("ABC       ");
    }

    [Test]
    [DisplayName("INotifyWrite + FieldTrim: AfterWrite receives raw line (trim is read-side only)")]
    public async Task NotifyWritePlusTrim_ReceivesRawLine()
    {
        // Arrange: Record with trim
        NotifyWriteTrimmedRecord.WrittenLines = new();
        var engine = new FileHelperEngine<NotifyWriteTrimmedRecord>();
        var records = new[] { new NotifyWriteTrimmedRecord { Name = "  hello  " } };

        // Act: Write
        engine.WriteString(records);

        // Assert: AfterWrite received the raw line (FieldTrim is read-side only)
        await Assert.That(NotifyWriteTrimmedRecord.WrittenLines[0]).IsEqualTo("  hello  ");
    }

    [Test]
    [DisplayName("Exception in AfterRead throws with ErrorMode.ThrowException")]
    public async Task AfterRead_Throws_WithThrowException()
    {
        // Arrange: Record that throws in AfterRead
        var engine = new FileHelperEngine<ThrowingAfterReadRecord> { ErrorMode = ErrorMode.ThrowException };
        var input = "test";

        // Act/Assert: Exception propagated
        await Assert.That(async () => engine.ReadString(input)).Throws<InvalidOperationException>();
    }

    [Test]
    [DisplayName("Exception in AfterWrite throws with ErrorMode.ThrowException")]
    public async Task AfterWrite_Throws_WithThrowException()
    {
        // Arrange: Record that throws in AfterWrite
        var engine = new FileHelperEngine<ThrowingAfterWriteRecord> { ErrorMode = ErrorMode.ThrowException };
        var records = new[] { new ThrowingAfterWriteRecord { Value = "test" } };

        // Act/Assert: Exception propagated
        await Assert.That(() => engine.WriteString(records)).Throws<InvalidOperationException>();
    }

    [Test]
    [DisplayName("INotifyRead on empty file fires no callbacks")]
    public async Task NotifyRead_EmptyFile_NoCallbacks()
    {
        // Arrange: Empty input
        EmptyFileNotifyRecord.ParsedCount = 0;
        var engine = new FileHelperEngine<EmptyFileNotifyRecord>();
        var input = "";

        // Act: Parse empty
        var records = engine.ReadString(input);

        // Assert: No records, no callbacks
        await Assert.That(records.Length).IsEqualTo(0);
        await Assert.That(EmptyFileNotifyRecord.ParsedCount).IsEqualTo(0);
    }

    [Test]
    [DisplayName("INotifyWrite with WriteFile fires interface methods")]
    public async Task NotifyWrite_WriteFile_FiresInterface()
    {
        // Arrange: Temp file and records
        WriteFileNotifyRecord.WriteCount = 0;
        var engine = new FileHelperEngine<WriteFileNotifyRecord>();
        var records = new[]
        {
            new WriteFileNotifyRecord { Value = "one" },
            new WriteFileNotifyRecord { Value = "two" },
        };
        var tempPath = Path.Combine(Path.GetTempPath(), $"nit_test_{Guid.NewGuid():N}.txt");

        try
        {
            // Act: Write to file
            engine.WriteFile(tempPath, records);

            // Assert: Interface fired for both records
            await Assert.That(WriteFileNotifyRecord.WriteCount).IsEqualTo(2);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Test]
    [DisplayName("Exception in AfterRead propagates regardless of ErrorMode")]
    public async Task AfterRead_SaveAndContinue_ExceptionPropagates()
    {
        // Arrange: Record that throws in AfterRead
        var engine = new FileHelperEngine<ThrowingAfterReadRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        var input = "test";

        // Act/Assert: Exception in AfterRead propagates (SaveAndContinue only handles parse errors)
        await Assert.That(() => engine.ReadString(input)).Throws<InvalidOperationException>();
    }
}
