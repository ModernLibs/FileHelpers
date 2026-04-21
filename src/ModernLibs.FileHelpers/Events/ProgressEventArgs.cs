namespace ModernLibs.FileHelpers;

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

    public int CurrentRecord { get; }
    public int TotalRecords { get; }
    public long CurrentBytes { get; }
    public long TotalBytes { get; }
    public double Percent { get; }
}
