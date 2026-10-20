// CA1711: Name kept for FileHelpers migration compatibility.
#pragma warning disable CA1711

namespace ModernLibs.FileHelpers;

/// <summary>
/// Data for the <c>BeforeReadRecord</c> engine event and <see cref="INotifyRead{T}.BeforeRead"/>,
/// raised before the text of a record is parsed (FileHelpers 3.x).
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
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

    /// <summary>
    /// The record text about to be parsed (all physical lines of a multi-line record, without the final
    /// line terminator). Assign a new value to parse that text instead.
    /// </summary>
    public string RecordLine { get; set; }

    /// <summary>1-based line number of the first physical line of the record.</summary>
    public int LineNumber { get; }

    /// <summary>Set to <see langword="true"/> to skip the record without parsing it; reading continues.</summary>
    public bool SkipThisRecord { get; set; }

    /// <summary>Set to <see langword="true"/> to end the read; this record and all later ones are not read.</summary>
    public bool StopReading { get; set; }

    /// <summary><see langword="true"/> when <see cref="RecordLine"/> has been assigned a different string.</summary>
    public bool RecordLineChanged => !ReferenceEquals(RecordLine, _originalLine);
}
