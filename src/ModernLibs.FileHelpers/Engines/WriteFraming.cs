using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>FileHelpers 3.x rules for header/footer emission and appending to existing files.</summary>
internal static class WriteFraming
{
    /// <summary>
    /// Writes a header or footer: nothing when <paramref name="text"/> is null or empty; otherwise the text,
    /// followed by <paramref name="newLine"/> unless it already ends with it.
    /// </summary>
    public static void WriteFrame(TextWriter writer, string? text, string newLine)
    {
        if (string.IsNullOrEmpty(text))
            return;

        writer.Write(text);
        if (!text.EndsWith(newLine, StringComparison.Ordinal))
            writer.Write(newLine);
    }

    /// <summary>
    /// Opens <paramref name="filePath"/> for appending. When the existing content does not end with a line
    /// terminator, <paramref name="newLine"/> is written first so the first appended record starts on its own line
    /// (3.x <c>correctEnd</c>).
    /// </summary>
    public static StreamWriter OpenAppender(string filePath, Encoding encoding, string newLine)
    {
        bool needsNewLine = !EndsWithLineTerminator(filePath, encoding);
        var writer = new StreamWriter(filePath, append: true, encoding);
        if (needsNewLine)
            writer.Write(newLine);
        return writer;
    }

    /// <summary>
    /// <see langword="true"/> when the file is missing, empty (or holds only the encoding's byte order mark), or
    /// its last character is CR or LF.
    /// </summary>
    private static bool EndsWithLineTerminator(string filePath, Encoding encoding)
    {
        var info = new FileInfo(filePath);
        if (!info.Exists || info.Length == 0)
            return true;

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var preamble = encoding.Preamble;
        if (preamble.Length > 0 && info.Length == preamble.Length)
        {
            Span<byte> head = stackalloc byte[preamble.Length];
            stream.ReadExactly(head);
            if (head.SequenceEqual(preamble))
                return true;
        }

        int width = Math.Max(1, encoding.GetByteCount("\n"));
        if (info.Length < width)
            return false;

        stream.Seek(-width, SeekOrigin.End);
        Span<byte> tail = stackalloc byte[width];
        stream.ReadExactly(tail);
        Span<char> last = stackalloc char[encoding.GetMaxCharCount(width)];
        int chars = encoding.GetChars(tail, last);
        return chars > 0 && last[chars - 1] is '\n' or '\r';
    }
}
