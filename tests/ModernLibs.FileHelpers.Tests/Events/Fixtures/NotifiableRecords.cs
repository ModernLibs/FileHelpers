// CA1707: Test fixture identifiers use underscores for readability.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not applicable to fixture declarations.
#pragma warning disable MA0004
// MA0025: Fixture bodies intentionally empty — engine populates fields via source-generated code.
#pragma warning disable MA0025
// MA0048: Multiple fixture types per file — intentional; groups related notification fixtures.
#pragma warning disable MA0048
// CS0649: Public fields/properties are assigned by the source-generated descriptor, not in user code.
#pragma warning disable CS0649
// CA1000: Generic interface is intentional for type-safe record notifications.
#pragma warning disable CA1000
// MA0006: String equality comparison in test fixtures is acceptable.
#pragma warning disable MA0006

using ModernLibs.FileHelpers;

namespace ModernLibs.FileHelpers.Tests.Events.Fixtures;

/// <summary>
/// Fixture implementing INotifyRead for testing read notifications.
/// Tracks method call counts and captures event args for verification.
/// </summary>
[DelimitedRecord(",")]
[IgnoreFirst(1)]
internal sealed class NotifiableCsvOrder : INotifyRead<NotifiableCsvOrder>
{
    [FieldOrder(1)] public int OrderId;
    [FieldOrder(2)] public string CustomerName = string.Empty;
    [FieldOrder(3)] public decimal Amount;

    // Tracking properties (not mapped as fields - hidden from FileHelpers)
    [FieldHidden] internal int BeforeReadCount { get; set; }
    [FieldHidden] internal int AfterReadCount { get; set; }
    [FieldHidden] internal BeforeReadEventArgs<NotifiableCsvOrder>? LastBeforeReadArgs { get; set; }
    [FieldHidden] internal AfterReadEventArgs<NotifiableCsvOrder>? LastAfterReadArgs { get; set; }

    /// <summary>
    /// Called before parsing. Can modify line, skip record, or stop reading.
    /// </summary>
    public void BeforeRead(BeforeReadEventArgs<NotifiableCsvOrder> e)
    {
        BeforeReadCount++;
        LastBeforeReadArgs = e;

        // Example: Skip lines containing "SKIP" in the raw line
        if (e.RecordLine.Contains("SKIP", StringComparison.Ordinal))
        {
            e.SkipThisRecord = true;
        }

        // Example: Stop reading at line 100 (for testing early termination)
        if (e.LineNumber >= 100)
        {
            e.StopReading = true;
        }
    }

    /// <summary>
    /// Called after successful parsing. Can filter record from results.
    /// </summary>
    public void AfterRead(AfterReadEventArgs<NotifiableCsvOrder> e)
    {
        AfterReadCount++;
        LastAfterReadArgs = e;

        // Example: Skip records with negative amounts
        if (e.Record.Amount < 0)
        {
            e.SkipThisRecord = true;
        }
    }
}

/// <summary>
/// Fixture implementing INotifyWrite for testing write notifications.
/// Tracks method call counts and captures event args for verification.
/// </summary>
[DelimitedRecord(",")]
internal sealed class NotifiableWriteRecord : INotifyWrite<NotifiableWriteRecord>
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public string Name = string.Empty;

    // Tracking properties (not mapped as fields - hidden from FileHelpers)
    [FieldHidden] internal int BeforeWriteCount { get; set; }
    [FieldHidden] internal int AfterWriteCount { get; set; }
    [FieldHidden] internal BeforeWriteEventArgs<NotifiableWriteRecord>? LastBeforeWriteArgs { get; set; }
    [FieldHidden] internal AfterWriteEventArgs<NotifiableWriteRecord>? LastAfterWriteArgs { get; set; }

    /// <summary>
    /// Called before formatting. Can skip record or perform validation.
    /// </summary>
    public void BeforeWrite(BeforeWriteEventArgs<NotifiableWriteRecord> e)
    {
        BeforeWriteCount++;
        LastBeforeWriteArgs = e;

        // Example: Skip records with negative IDs
        if (e.Record.Id < 0)
        {
            e.SkipThisRecord = true;
        }
    }

    /// <summary>
    /// Called after formatting. Can modify the formatted line.
    /// </summary>
    public void AfterWrite(AfterWriteEventArgs<NotifiableWriteRecord> e)
    {
        AfterWriteCount++;
        LastAfterWriteArgs = e;

        // Example: Add comment suffix to certain records
        if (e.Record.Id == 42)
        {
            e.RecordLine = e.RecordLine + " # Special record";
        }
    }
}

/// <summary>
/// Fixture implementing both interfaces for combined testing.
/// </summary>
[DelimitedRecord(",")]
[IgnoreFirst(1)]
internal sealed class DualNotifiableRecord : INotifyRead<DualNotifiableRecord>, INotifyWrite<DualNotifiableRecord>
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public string Value = string.Empty;

    // Read notification tracking (not mapped as fields - [FieldHidden]: internal members are mapped like public ones)
    [FieldHidden] internal int BeforeReadCount { get; set; }
    [FieldHidden] internal int AfterReadCount { get; set; }
    [FieldHidden] internal BeforeReadEventArgs<DualNotifiableRecord>? LastBeforeReadArgs { get; set; }
    [FieldHidden] internal AfterReadEventArgs<DualNotifiableRecord>? LastAfterReadArgs { get; set; }

    // Write notification tracking (not mapped as fields - [FieldHidden]: internal members are mapped like public ones)
    [FieldHidden] internal int BeforeWriteCount { get; set; }
    [FieldHidden] internal int AfterWriteCount { get; set; }
    [FieldHidden] internal BeforeWriteEventArgs<DualNotifiableRecord>? LastBeforeWriteArgs { get; set; }
    [FieldHidden] internal AfterWriteEventArgs<DualNotifiableRecord>? LastAfterWriteArgs { get; set; }

    public void BeforeRead(BeforeReadEventArgs<DualNotifiableRecord> e)
    {
        BeforeReadCount++;
        LastBeforeReadArgs = e;
    }

    public void AfterRead(AfterReadEventArgs<DualNotifiableRecord> e)
    {
        AfterReadCount++;
        LastAfterReadArgs = e;
    }

    public void BeforeWrite(BeforeWriteEventArgs<DualNotifiableRecord> e)
    {
        BeforeWriteCount++;
        LastBeforeWriteArgs = e;
    }

    public void AfterWrite(AfterWriteEventArgs<DualNotifiableRecord> e)
    {
        AfterWriteCount++;
        LastAfterWriteArgs = e;
    }
}

/// <summary>
/// Fixture that conditionally skips records based on runtime state.
/// Used to test SkipThisRecord behavior from interface methods.
/// </summary>
[DelimitedRecord(",")]
[IgnoreFirst(1)]
internal sealed class ConditionalSkipRecord : INotifyRead<ConditionalSkipRecord>, INotifyWrite<ConditionalSkipRecord>
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public string Category = string.Empty;

    // Static shared state for conditional skipping (set by tests before reading/writing)
    internal static string _sharedSkipCategory = string.Empty;
    internal static int _sharedSkipBeforeLineNumber;
    internal static int _sharedStopAtLineNumber;

    /// <summary>
    /// Test helper to configure skip behavior before operations.
    /// </summary>
    public static void ConfigureSkip(string skipCategory, int skipBeforeLine = 0, int stopAtLine = 0)
    {
        _sharedSkipCategory = skipCategory;
        _sharedSkipBeforeLineNumber = skipBeforeLine;
        _sharedStopAtLineNumber = stopAtLine;
    }

    /// <summary>
    /// Test helper to clear skip configuration.
    /// </summary>
    public static void ClearConfiguration()
    {
        _sharedSkipCategory = string.Empty;
        _sharedSkipBeforeLineNumber = 0;
        _sharedStopAtLineNumber = 0;
    }

    public void BeforeRead(BeforeReadEventArgs<ConditionalSkipRecord> e)
    {
        if (e.LineNumber == _sharedSkipBeforeLineNumber && _sharedSkipBeforeLineNumber > 0)
        {
            e.SkipThisRecord = true;
        }
        if (e.LineNumber >= _sharedStopAtLineNumber && _sharedStopAtLineNumber > 0)
        {
            e.StopReading = true;
        }
    }

    public void AfterRead(AfterReadEventArgs<ConditionalSkipRecord> e)
    {
        if (string.Equals(e.Record.Category, _sharedSkipCategory, StringComparison.Ordinal) && !string.IsNullOrEmpty(_sharedSkipCategory))
        {
            e.SkipThisRecord = true;
        }
    }

    public void BeforeWrite(BeforeWriteEventArgs<ConditionalSkipRecord> e)
    {
        if (string.Equals(e.Record.Category, _sharedSkipCategory, StringComparison.Ordinal) && !string.IsNullOrEmpty(_sharedSkipCategory))
        {
            e.SkipThisRecord = true;
        }
    }

    public void AfterWrite(AfterWriteEventArgs<ConditionalSkipRecord> e)
    {
        // No-op for this fixture
    }
}
