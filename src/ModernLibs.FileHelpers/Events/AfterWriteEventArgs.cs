// CA1711: Name kept for FileHelpers migration compatibility.
#pragma warning disable CA1711

namespace ModernLibs.FileHelpers;

public sealed class AfterWriteEventArgs<T>
{
    internal AfterWriteEventArgs(object engine, T record, int recordNumber, string recordLine)
    {
        _ = engine;
        Record = record;
        RecordNumber = recordNumber;
        RecordLine = recordLine;
    }

    public T Record { get; }
    public string RecordLine { get; set; }
    public int RecordNumber { get; }
}
