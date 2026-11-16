// MA0048: the fixture records, their direct format bodies and the descriptor are grouped in one file.
#pragma warning disable MA0048

using System.Buffers;
using System.Globalization;

namespace ModernLibs.FileHelpers.Tests.Engines.Fixtures;

/// <summary>The body of <see cref="IRecordWriterDescriptor{T}.Format(T, ref RecordWriter)"/> of a <see cref="DirectWriterDescriptor{T}"/>.</summary>
/// <typeparam name="T">The record type.</typeparam>
/// <param name="record">The record to format.</param>
/// <param name="writer">The writer of the record.</param>
internal delegate void DirectFormat<T>(T record, ref RecordWriter writer);

/// <summary>
/// A hand-written 4.10 descriptor (ruling F29): <see cref="Format(T, ref RecordWriter)"/> writes the fields straight
/// into the <see cref="RecordWriter"/>, with no buffer of its own, and counts its calls. The legacy
/// <see cref="Format(T, IBufferWriter{char})"/> and every other member forward to the generated descriptor, so the
/// <see cref="EnginePaths.LegacyWriter"/> engine writes the text of the current generator.
/// </summary>
/// <param name="inner">The generated descriptor.</param>
/// <param name="format">The direct format body; it must write the text the generated <c>Format</c> writes.</param>
internal sealed class DirectWriterDescriptor<T>(IRecordDescriptor<T> inner, DirectFormat<T> format) : IRecordWriterDescriptor<T>
{
    private int _writerFormatCalls;

    /// <summary>Calls of <see cref="Format(T, ref RecordWriter)"/> (thread-safe).</summary>
    public int WriterFormatCalls => Volatile.Read(ref _writerFormatCalls);

    public ReadOnlySpan<FieldDescriptor> Fields => inner.Fields;

    public RecordOptions Options => inner.Options;

    public T Parse(ReadOnlySpan<char> lines, int lineCount) => inner.Parse(lines, lineCount);

    public T CreateDefaultInstance() => inner.CreateDefaultInstance();

    public void Format(T record, IBufferWriter<char> writer) => inner.Format(record, writer);

    public void Format(T record, ref RecordWriter writer)
    {
        Interlocked.Increment(ref _writerFormatCalls);
        format(record, ref writer);
    }
}

/// <summary>A one-field delimited record: its text is <see cref="Text"/>, so a test sets the record length exactly.</summary>
[DelimitedRecord(",")]
public sealed class ChunkLine
{
    [FieldOrder(1)] public string Text { get; set; } = string.Empty;
}

/// <summary>A delimited record with a converted field: <c>Id,Name</c>.</summary>
[DelimitedRecord(",")]
public sealed class ChunkPair
{
    [FieldOrder(1)] public int Id { get; set; }

    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
}

/// <summary>Direct format bodies for <see cref="ChunkLine"/> and <see cref="ChunkPair"/>, in the shape of design 5.1.</summary>
internal static class DirectFormats
{
    /// <summary>Stands for the converter of <see cref="ChunkPair.Id"/> in <see cref="RecordWriter.Grow(object)"/>.</summary>
    private static readonly object IdConverter = new();

    /// <summary>A <see cref="DirectWriterDescriptor{T}"/> for <see cref="ChunkLine"/>.</summary>
    public static DirectWriterDescriptor<ChunkLine> Line() => new(FileHelperRegistry<ChunkLine>.Descriptor, FormatLine);

    /// <summary>A <see cref="DirectWriterDescriptor{T}"/> for <see cref="ChunkPair"/>.</summary>
    public static DirectWriterDescriptor<ChunkPair> Pair() => new(FileHelperRegistry<ChunkPair>.Descriptor, FormatPair);

    private static void FormatLine(ChunkLine record, ref RecordWriter writer) => writer.Write(record.Text);

    private static void FormatPair(ChunkPair record, ref RecordWriter writer)
    {
        int written;
        while (!record.Id.TryFormat(writer.FreeSpan, out written, default, CultureInfo.InvariantCulture))
            writer.Grow(IdConverter);
        writer.Advance(written);
        writer.Write(',');
        writer.Write(record.Name);
    }
}
