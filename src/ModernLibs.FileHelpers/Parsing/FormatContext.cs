namespace ModernLibs.FileHelpers;

/// <summary>
/// Stack-allocated format state carrier passed by the batch engine during write operations.
/// A ref struct — never heap-allocated (D-07, PIPE-06).
/// </summary>
public ref struct FormatContext
{
    /// <summary>The number of records written so far in the current write session.</summary>
    public int RecordsWritten { get; set; }
}
