// CA1711: Name kept for FileHelpers migration compatibility.
#pragma warning disable CA1711

namespace ModernLibs.FileHelpers;

/// <summary>
/// Data for the <c>AfterWriteRecord</c> engine event and <see cref="INotifyWrite{T}.AfterWrite"/>,
/// raised after a record has been formatted and before its text is written (FileHelpers 3.x).
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
public sealed class AfterWriteEventArgs<T>
{
    internal AfterWriteEventArgs(object engine, T record, int recordNumber, string recordLine)
    {
        _ = engine;
        Record = record;
        RecordNumber = recordNumber;
        RecordLine = recordLine;
    }

    /// <summary>The record that was formatted.</summary>
    public T Record { get; }

    /// <summary>
    /// The formatted record text, without line terminator. Assign a new value to write that text instead.
    /// </summary>
    public string RecordLine { get; set; }

    /// <summary>1-based number of the record within the current write operation.</summary>
    public int RecordNumber { get; }
}
