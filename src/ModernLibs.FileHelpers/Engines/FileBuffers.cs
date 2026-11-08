using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Opens the readers and writers of the file-path overloads with buffers sized for record files. The framework
/// defaults (1 KB to 4 KB) cost a system call every few records.
/// </summary>
/// <remarks>
/// The buffers stay below the large object heap threshold (85,000 bytes): a <see cref="StreamReader"/> holds a
/// byte buffer of its size and a char buffer of about the same length, and a <see cref="StreamWriter"/> a char
/// buffer of its size and a byte buffer of up to three times that for UTF-8. A reader of a small file gets a
/// buffer of the file's size, so opening one costs no more than it did with the defaults.
/// </remarks>
internal static class FileBuffers
{
    /// <summary>Buffer size, in bytes for readers and in characters for writers.</summary>
    public const int Size = 16 * 1024;

    /// <summary>The smallest buffer a <see cref="StreamReader"/> uses.</summary>
    private const int MinimumReaderSize = 128;

    /// <summary>A reader over <paramref name="filePath"/>; a byte order mark wins over <paramref name="encoding"/>.</summary>
    public static StreamReader OpenReader(string filePath, Encoding encoding) =>
        OpenReader(filePath, encoding, FileOptions.SequentialScan);

    /// <summary>
    /// A reader over <paramref name="filePath"/> opened for asynchronous I/O where the platform supports it (on
    /// Windows asynchronous reads are then overlapped I/O instead of synchronous reads on the thread pool). Same
    /// sharing and encoding detection as <see cref="OpenReader(string, Encoding)"/>.
    /// </summary>
    public static StreamReader OpenAsyncReader(string filePath, Encoding encoding) =>
        OpenReader(filePath, encoding, FileOptions.Asynchronous | FileOptions.SequentialScan);

    /// <summary>A writer to <paramref name="filePath"/>.</summary>
    public static StreamWriter OpenWriter(string filePath, bool append, Encoding encoding) =>
        new(filePath, append, encoding, bufferSize: Size);

    private static StreamReader OpenReader(string filePath, Encoding encoding, FileOptions options)
    {
        // Sharing as StreamReader(string) opens files. bufferSize 1: the StreamReader buffers, so the FileStream
        // does not need to.
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, options);
        try
        {
            // A pipe or device has no length: use the full buffer.
            int bufferSize = stream.CanSeek ? (int)Math.Clamp(stream.Length, MinimumReaderSize, Size) : Size;
            return new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true, bufferSize, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }
}
