// CA1711: Name kept for FileHelpers migration compatibility.
#pragma warning disable CA1711

namespace ModernLibs.FileHelpers;

public sealed class BeforeReadEventArgs<T>
{
    private readonly string _originalLine;

    internal BeforeReadEventArgs(object engine, string recordLine, int lineNumber)
    {
        _ = engine;
        _originalLine = recordLine;
        RecordLine = recordLine;
        LineNumber = lineNumber;
    }

    public string RecordLine { get; set; }
    public int LineNumber { get; }
    public bool SkipThisRecord { get; set; }
    public bool StopReading { get; set; }
    public bool RecordLineChanged => !ReferenceEquals(RecordLine, _originalLine);
}
