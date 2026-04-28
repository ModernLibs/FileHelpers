using System.Text;

namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// Chooses the string used to rejoin physical lines when assembling a multiline quoted record.
/// Mirrors the dominant line terminator in the source so in-field newlines match the input.
/// </summary>
internal static class MultilineLineJoin
{
    /// <summary>
    /// Detects the dominant line terminator in <paramref name="content"/> for multiline assembly.
    /// Prefers <c>\r\n</c> when present, then <c>\n</c>, then <c>\r</c>, otherwise <c>\r\n</c> (write default).
    /// </summary>
    public static string Detect(ReadOnlySpan<char> content)
    {
        if (content.IndexOf("\r\n".AsSpan(), StringComparison.Ordinal) >= 0)
            return "\r\n";
        if (content.IndexOf('\n') >= 0)
            return "\n";
        if (content.IndexOf('\r') >= 0)
            return "\r";
        return "\r\n";
    }

    /// <summary>
    /// Reads up to <paramref name="maxBytes"/> from the start of a file and runs <see cref="Detect"/>.
    /// </summary>
    public static string DetectFromFileHead(string filePath, Encoding encoding, int maxBytes = 8192)
    {
        using var fs = File.OpenRead(filePath);
        var rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes);
        try
        {
            int read = fs.Read(rented, 0, maxBytes);
            if (read == 0)
                return "\r\n";
            return Detect(encoding.GetString(rented, 0, read).AsSpan());
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
