// CA1711: Name kept for FileHelpers migration compatibility.
#pragma warning disable CA1711

namespace ModernLibs.FileHelpers;

/// <summary>
/// Data for the <c>AfterReadRecord</c> engine event and <see cref="INotifyRead{T}.AfterRead"/>,
/// raised after a record has been parsed and before it is returned to the caller (FileHelpers 3.x).
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
public sealed class AfterReadEventArgs<T>
{
    internal AfterReadEventArgs(object engine, T record, string recordLine, int lineNumber)
    {
        _ = engine;
        Record = record;
        RecordLine = recordLine;
        LineNumber = lineNumber;
    }

    /// <summary>The parsed record. For reference types, changes made to it are kept.</summary>
    public T Record { get; }

    /// <summary>The record text that was parsed (after any replacement made in <c>BeforeReadRecord</c>).</summary>
    public string RecordLine { get; }

    /// <summary>1-based line number of the last physical line of the record.</summary>
    public int LineNumber { get; }

    /// <summary>Set to <see langword="true"/> to drop the record from the results; reading continues.</summary>
    public bool SkipThisRecord { get; set; }
}
