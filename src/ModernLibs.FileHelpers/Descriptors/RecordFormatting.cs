using System.Buffers;
using System.ComponentModel;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Write helpers called by source-generated <see cref="IRecordDescriptor{T}.Format"/> implementations.
/// </summary>
/// <remarks>
/// Infrastructure for generated code, not intended for direct use; generated code in consumer assemblies binds
/// to these signatures, which therefore stay stable in 4.x.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class RecordFormatting
{
    /// <summary>
    /// Writes the line break before a <see cref="FieldInNewLineAttribute"/> field: the engine's
    /// <c>NewLineForWrite</c> when <paramref name="writer"/> is an engine write buffer, otherwise <c>"\r\n"</c>.
    /// </summary>
    /// <param name="writer">The buffer the record is being formatted into.</param>
    public static void WriteFieldNewLine(IBufferWriter<char> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        string newLine = writer is RentedBufferWriter engineBuffer ? engineBuffer.NewLine : "\r\n";
        if (newLine.Length == 0)
            return;

        newLine.AsSpan().CopyTo(writer.GetSpan(newLine.Length));
        writer.Advance(newLine.Length);
    }
}
