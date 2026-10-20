namespace ModernLibs.FileHelpers;

/// <summary>
/// Data for the <c>Progress</c> engine event, raised after each record a read returns.
/// </summary>
/// <remarks>
/// Reads are streamed, so the totals are not known in advance: <see cref="TotalRecords"/> is <c>-1</c>,
/// the byte counts are <c>0</c> and <see cref="Percent"/> is <c>0</c>.
/// </remarks>
public sealed class ProgressEventArgs : EventArgs
{
    internal ProgressEventArgs(int currentRecord, int totalRecords, long currentBytes, long totalBytes)
    {
        CurrentRecord = currentRecord;
        TotalRecords = totalRecords;
        CurrentBytes = currentBytes;
        TotalBytes = totalBytes;
        Percent = totalRecords > 0 ? (double)currentRecord / totalRecords * 100.0 : 0.0;
    }

    /// <summary>Number of records read so far in the current operation.</summary>
    public int CurrentRecord { get; }

    /// <summary>Total number of records, or <c>-1</c> when unknown.</summary>
    public int TotalRecords { get; }

    /// <summary>Bytes processed so far, or <c>0</c> when not tracked.</summary>
    public long CurrentBytes { get; }

    /// <summary>Total bytes to process, or <c>0</c> when unknown.</summary>
    public long TotalBytes { get; }

    /// <summary>Completion percentage (0–100), or <c>0</c> when <see cref="TotalRecords"/> is unknown.</summary>
    public double Percent { get; }
}
