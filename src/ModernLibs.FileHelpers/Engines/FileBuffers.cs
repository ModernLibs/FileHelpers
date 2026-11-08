using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Opens the readers and writers of the file-path overloads with buffers sized for record files. The framework
/// defaults (1 KB to 4 KB) cost a system call every few records.
/// </summary>
internal static class FileBuffers
{
    /// <summary>Buffer size, in bytes for streams and in characters for readers and writers.</summary>
    public const int Size = 64 * 1024;

    /// <summary>A reader over <paramref name="filePath"/>; a byte order mark wins over <paramref name="encoding"/>.</summary>
    public static StreamReader OpenReader(string filePath, Encoding encoding) =>
        new(filePath, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: Size);

    /// <summary>
    /// A reader over <paramref name="filePath"/> whose asynchronous reads are asynchronous I/O rather than
    /// synchronous reads on the thread pool. Same sharing and encoding detection as <see cref="OpenReader"/>.
    /// </summary>
    public static StreamReader OpenAsyncReader(string filePath, Encoding encoding)
    {
        // bufferSize 1: the StreamReader buffers, so the FileStream does not need to.
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
        try
        {
            return new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: Size, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>A writer to <paramref name="filePath"/>.</summary>
    public static StreamWriter OpenWriter(string filePath, bool append, Encoding encoding) =>
        new(filePath, append, encoding, bufferSize: Size);
}
