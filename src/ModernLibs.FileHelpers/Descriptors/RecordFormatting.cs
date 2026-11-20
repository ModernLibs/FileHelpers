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
        string newLine = writer is RecordWriteBuffer engineBuffer ? engineBuffer.NewLine : "\r\n";
        if (newLine.Length == 0)
            return;

        newLine.AsSpan().CopyTo(writer.GetSpan(newLine.Length));
        writer.Advance(newLine.Length);
    }

    /// <summary>
    /// The legacy <see cref="IRecordDescriptor{T}.Format(T, IBufferWriter{char})"/> of a 4.10 descriptor: appends to
    /// <paramref name="writer"/> the text that <see cref="IRecordWriterDescriptor{T}.Format(T, ref RecordWriter)"/>
    /// writes for <paramref name="record"/>.
    /// </summary>
    /// <remarks>
    /// The line break of <see cref="RecordWriter.WriteNewLine"/> is the engine's <c>NewLineForWrite</c> when
    /// <paramref name="writer"/> is an engine write buffer, otherwise <c>"\r\n"</c>, as for
    /// <see cref="WriteFieldNewLine"/>. Into an engine write buffer the record is formatted in place; into any other
    /// writer it is formatted into a temporary buffer and copied with one <see cref="IBufferWriter{T}.GetSpan(int)"/>
    /// call. When the descriptor throws, nothing is written.
    /// </remarks>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="descriptor">The descriptor that formats the record.</param>
    /// <param name="record">The record to format.</param>
    /// <param name="writer">The destination writer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> or <paramref name="writer"/> is <see langword="null"/>.</exception>
    public static void Format<T>(IRecordWriterDescriptor<T> descriptor, T record, IBufferWriter<char> writer)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(writer);
        if (writer is RecordWriteBuffer engineBuffer)
        {
            engineBuffer.Format(descriptor, record);
            return;
        }

        // A descriptor is one shared instance per record type: the temporary buffer belongs to this call.
        using var temporary = new RentedBufferWriter(256);
        temporary.Format(descriptor, record);
        ReadOnlySpan<char> text = temporary.WrittenSpan;
        if (text.IsEmpty)
            return;

        text.CopyTo(writer.GetSpan(text.Length));
        writer.Advance(text.Length);
    }
}
