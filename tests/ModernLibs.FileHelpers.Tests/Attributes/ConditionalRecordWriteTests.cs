// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// MA0048: Multiple test classes per file — intentional; groups related tests.
#pragma warning disable MA0048
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Attributes;

// ---- Test record types ----

[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.IncludeIfBegins, "ORD")]
internal sealed class ConditionalOrderRecord
{
    [FieldOrder(1)]
    public string Id { get; set; } = "";

    [FieldOrder(2)]
    public string Name { get; set; } = "";
}

[DelimitedRecord(",")]
[ConditionalRecord(RecordCondition.IncludeIfBegins, "ORD")]
internal sealed class ConditionalNotifyWriteRecord : INotifyWrite<ConditionalNotifyWriteRecord>
{
    [FieldOrder(1)]
    public string Id { get; set; } = "";

    [FieldOrder(2)]
    public string Name { get; set; } = "";

    [FieldOrder(3)]
    [FieldHidden]
    public int BeforeWriteCount { get; set; }
    [FieldOrder(4)]
    [FieldHidden]
    public int AfterWriteCount { get; set; }

    public void BeforeWrite(BeforeWriteEventArgs<ConditionalNotifyWriteRecord> e) => BeforeWriteCount++;
    public void AfterWrite(AfterWriteEventArgs<ConditionalNotifyWriteRecord> e) => AfterWriteCount++;
}

/// <summary>ConditionalRecord write-side and interaction tests.</summary>
[Category("ConditionalRecord")]
public sealed class ConditionalRecordWriteTests
{
    /// <summary>WriteString writes all records regardless of ConditionalRecord.</summary>
    [Test]
    [DisplayName("WriteString writes all records even with ConditionalRecord")]
    public async Task WriteString_WritesAllRecords()
    {
        // Arrange: Mix of matching and non-matching records
        var engine = new FileHelperEngine<ConditionalOrderRecord>();
        var records = new[]
        {
            new ConditionalOrderRecord { Id = "ORD001", Name = "Alice" },
            new ConditionalOrderRecord { Id = "INV001", Name = "Bob" },
            new ConditionalOrderRecord { Id = "ORD002", Name = "Charlie" },
        };

        // Act: Write all
        var result = engine.WriteString(records);

        // Assert: All three records written (condition only affects reading)
        await Assert.That(result).Contains("ORD001");
        await Assert.That(result).Contains("INV001");
        await Assert.That(result).Contains("ORD002");
    }

    /// <summary>WriteFile with ConditionalRecord writes all records.</summary>
    [Test]
    [DisplayName("WriteFile writes all records even with ConditionalRecord")]
    public async Task WriteFile_WritesAllRecords()
    {
        // Arrange: Temp file and mixed records
        var engine = new FileHelperEngine<ConditionalOrderRecord>();
        var records = new[]
        {
            new ConditionalOrderRecord { Id = "ORD001", Name = "Alice" },
            new ConditionalOrderRecord { Id = "X001", Name = "Bob" },
        };
        var tempPath = Path.Combine(Path.GetTempPath(), $"crw_test_{Guid.NewGuid():N}.txt");

        try
        {
            // Act: Write to file
            engine.WriteFile(tempPath, records);
            var content = await File.ReadAllTextAsync(tempPath);

            // Assert: Both records written
            await Assert.That(content).Contains("ORD001");
            await Assert.That(content).Contains("X001");
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    /// <summary>Async engine write with ConditionalRecord.</summary>
    [Test]
    [DisplayName("Async engine write works with ConditionalRecord")]
    public async Task AsyncEngine_Write_WithConditionalRecord()
    {
        // Arrange: Async engine with conditional record
        var engine = new FileHelperAsyncEngine<ConditionalOrderRecord>();
        var records = new[]
        {
            new ConditionalOrderRecord { Id = "ORD001", Name = "Alice" },
            new ConditionalOrderRecord { Id = "SKIP", Name = "Bob" },
        };

        // Act: Write via async engine
        engine.BeginWriteString();
        foreach (var record in records)
            engine.WriteNext(record);
        var result = engine.EndWriteString();

        // Assert: All records written
        await Assert.That(result).Contains("ORD001");
        await Assert.That(result).Contains("SKIP");
    }

    /// <summary>ConditionalRecord + INotifyWrite interaction.</summary>
    [Test]
    [DisplayName("ConditionalRecord + INotifyWrite: interface fires for all written records")]
    public async Task ConditionalPlusNotifyWrite_FiresForAllWritten()
    {
        // Arrange: Records with INotifyWrite
        var engine = new FileHelperEngine<ConditionalNotifyWriteRecord>();
        var records = new[]
        {
            new ConditionalNotifyWriteRecord { Id = "ORD001", Name = "Alice" },
            new ConditionalNotifyWriteRecord { Id = "X001", Name = "Bob" },
        };

        // Act: Write
        engine.WriteString(records);

        // Assert: Interface fired for both records (write ignores condition)
        await Assert.That(records[0].BeforeWriteCount).IsEqualTo(1);
        await Assert.That(records[0].AfterWriteCount).IsEqualTo(1);
        await Assert.That(records[1].BeforeWriteCount).IsEqualTo(1);
        await Assert.That(records[1].AfterWriteCount).IsEqualTo(1);
    }

    /// <summary>Read with ConditionalRecord filters, then round-trip write preserves all.</summary>
    [Test]
    [DisplayName("Read filters with ConditionalRecord, round-trip write preserves filtered results")]
    public async Task ReadFilters_WritePreservesFiltered()
    {
        // Arrange: Input with mix of matching and non-matching lines
        var engine = new FileHelperEngine<ConditionalOrderRecord>();
        var input = "ORD001,Alice\nINV001,Bob\nORD002,Charlie";

        // Act: Read (should filter out INV001)
        var records = engine.ReadString(input);

        // Assert: Only matching records read
        await Assert.That(records.Length).IsEqualTo(2);
        await Assert.That(records[0].Id).IsEqualTo("ORD001");
        await Assert.That(records[1].Id).IsEqualTo("ORD002");

        // Act: Write the filtered records
        var written = engine.WriteString(records);

        // Assert: Written output contains only the filtered records
        await Assert.That(written).Contains("ORD001");
        await Assert.That(written).Contains("ORD002");
        await Assert.That(written).DoesNotContain("INV001");
    }
}
