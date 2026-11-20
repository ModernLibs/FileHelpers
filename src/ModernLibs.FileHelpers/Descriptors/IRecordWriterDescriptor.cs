using System.ComponentModel;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Optional extension of <see cref="IRecordDescriptor{T}"/> that formats a record straight into the write buffer of
/// the engine (<see cref="RecordWriter"/>). Infrastructure for source-generated code; not intended for direct use.
/// </summary>
/// <remarks>
/// <para>The source generator implements it from 4.10 on. The engines call it, instead of
/// <see cref="IRecordDescriptor{T}.Format(T, System.Buffers.IBufferWriter{char})"/>, unless the
/// <c>ModernLibs.FileHelpers.DisableRecordWriter</c> switch is set. The legacy <c>Format</c> of such a descriptor
/// is <see cref="RecordFormatting.Format{T}(IRecordWriterDescriptor{T}, T, System.Buffers.IBufferWriter{char})"/>,
/// so both write the same text.</para>
/// <para>Members added to this interface after 4.10.0 come with a default implementation, as for
/// <see cref="IRecordDescriptor{T}"/>.</para>
/// </remarks>
/// <typeparam name="T">The annotated record type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IRecordWriterDescriptor<T> : IRecordDescriptor<T>
{
    /// <summary>Formats <paramref name="record"/> into <paramref name="writer"/> (without a line terminator).</summary>
    /// <remarks>
    /// The runtime keeps the text only when the method returns: when it throws, nothing of the record is written.
    /// </remarks>
    /// <param name="record">The record to format.</param>
    /// <param name="writer">The writer of the record, positioned at its start (<see cref="RecordWriter.Position"/> 0).</param>
    void Format(T record, ref RecordWriter writer);
}
