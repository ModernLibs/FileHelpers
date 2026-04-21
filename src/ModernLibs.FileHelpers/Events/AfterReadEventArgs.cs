// CA1711: Name kept for FileHelpers migration compatibility.
#pragma warning disable CA1711

namespace ModernLibs.FileHelpers;

public sealed class AfterReadEventArgs<T>
{
    internal AfterReadEventArgs(object engine, T record, string recordLine, int lineNumber)
    {
        _ = engine;
        Record = record;
        RecordLine = recordLine;
        LineNumber = lineNumber;
    }

    public T Record { get; }
    public string RecordLine { get; }
    public int LineNumber { get; }
    public bool SkipThisRecord { get; set; }
}
