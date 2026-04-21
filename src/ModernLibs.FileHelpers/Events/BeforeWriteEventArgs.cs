// CA1711: Name kept for FileHelpers migration compatibility.
#pragma warning disable CA1711

namespace ModernLibs.FileHelpers;

public sealed class BeforeWriteEventArgs<T>
{
    internal BeforeWriteEventArgs(object engine, T record, int recordNumber)
    {
        _ = engine;
        Record = record;
        RecordNumber = recordNumber;
    }

    public T Record { get; }
    public int RecordNumber { get; }
    public bool SkipThisRecord { get; set; }
}
