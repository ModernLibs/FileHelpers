// CA1711: Name kept for FileHelpers migration compatibility.
#pragma warning disable CA1711

namespace ModernLibs.FileHelpers;

/// <summary>
/// Data for the <c>BeforeWriteRecord</c> engine event and <see cref="INotifyWrite{T}.BeforeWrite"/>,
/// raised before a record is formatted (FileHelpers 3.x).
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
public sealed class BeforeWriteEventArgs<T>
{
    internal BeforeWriteEventArgs(object engine, T record, int recordNumber)
    {
        _ = engine;
        Record = record;
        RecordNumber = recordNumber;
    }

    /// <summary>The record about to be written. For reference types, changes made to it are written.</summary>
    public T Record { get; }

    /// <summary>1-based number of the record within the current write operation.</summary>
    public int RecordNumber { get; }

    /// <summary>Set to <see langword="true"/> to leave the record out of the output.</summary>
    public bool SkipThisRecord { get; set; }
}
