// MA0048: the checking descriptors are grouped in one file.
#pragma warning disable MA0048

using System.Buffers;

namespace ModernLibs.FileHelpers.Tests.Engines.Fixtures;

/// <summary>
/// A 4.10-style descriptor around a generated descriptor (T22): <see cref="Format(T, ref RecordWriter)"/> formats
/// through the inner legacy <c>Format</c> into a temporary buffer and copies the text with
/// <see cref="RecordWriter.Write(ReadOnlySpan{char})"/>, each <see cref="FieldInNewLineAttribute"/> line break with
/// <see cref="RecordWriter.WriteNewLine"/>. It counts the calls of each <c>Format</c>. Every other member forwards to
/// the inner descriptor.
/// </summary>
/// <param name="inner">The generated descriptor.</param>
/// <param name="legacyThroughAdapter">
/// <see langword="true"/>: the legacy <c>Format</c> is the one line a 4.10 descriptor has,
/// <see cref="RecordFormatting.Format{T}(IRecordWriterDescriptor{T}, T, IBufferWriter{char})"/> (the adapter). Otherwise
/// it forwards to the inner legacy <c>Format</c>.
/// </param>
internal sealed class WriterCheckingDescriptor<T>(IRecordDescriptor<T> inner, bool legacyThroughAdapter = false) : IRecordWriterDescriptor<T>
{
    /// <summary>Stands for the session newline in the temporary buffer; no test text holds it.</summary>
    private const string Sentinel = "\u0001";

    private int _writerFormatCalls;
    private int _legacyFormatCalls;

    /// <summary>Calls of <see cref="Format(T, ref RecordWriter)"/> (thread-safe).</summary>
    public int WriterFormatCalls => Volatile.Read(ref _writerFormatCalls);

    /// <summary>Calls of the legacy <see cref="Format(T, IBufferWriter{char})"/>.</summary>
    public int LegacyFormatCalls => Volatile.Read(ref _legacyFormatCalls);

    public ReadOnlySpan<FieldDescriptor> Fields => inner.Fields;

    public RecordOptions Options => inner.Options;

    public T Parse(ReadOnlySpan<char> lines, int lineCount) => inner.Parse(lines, lineCount);

    public T CreateDefaultInstance() => inner.CreateDefaultInstance();

    public void Format(T record, IBufferWriter<char> writer)
    {
        Interlocked.Increment(ref _legacyFormatCalls);
        if (legacyThroughAdapter)
            RecordFormatting.Format(this, record, writer);
        else
            inner.Format(record, writer);
    }

    public void Format(T record, ref RecordWriter writer)
    {
        Interlocked.Increment(ref _writerFormatCalls);
        using var temp = new RentedBufferWriter(256) { NewLine = Sentinel };
        inner.Format(record, temp);

        ReadOnlySpan<char> text = temp.WrittenSpan;
        int lineBreak;
        while ((lineBreak = text.IndexOf(Sentinel, StringComparison.Ordinal)) >= 0)
        {
            writer.Write(text[..lineBreak]);
            writer.WriteNewLine();
            text = text[(lineBreak + Sentinel.Length)..];
        }

        writer.Write(text);
    }
}

/// <summary>
/// A 4.8 or 4.9 descriptor seen by the runtime (no <see cref="IRecordWriterDescriptor{T}"/>): forwards to the inner
/// descriptor, counts the <c>Format</c> calls, and records each writer that is not a <see cref="RentedBufferWriter"/>
/// whose <see cref="RecordWriteBuffer.NewLine"/> is <see cref="ExpectedNewLine"/> (design 5.2, first row).
/// </summary>
internal sealed class LegacyCheckingDescriptor<T>(IRecordDescriptor<T> inner, string expectedNewLine) : IRecordDescriptor<T>
{
    private int _formatCalls;
    private int _wrongWriters;

    public string ExpectedNewLine => expectedNewLine;

    public int FormatCalls => Volatile.Read(ref _formatCalls);

    /// <summary>Calls whose writer was not a <see cref="RentedBufferWriter"/> with <see cref="ExpectedNewLine"/>.</summary>
    public int WrongWriters => Volatile.Read(ref _wrongWriters);

    public ReadOnlySpan<FieldDescriptor> Fields => inner.Fields;

    public RecordOptions Options => inner.Options;

    public T Parse(ReadOnlySpan<char> lines, int lineCount) => inner.Parse(lines, lineCount);

    public T CreateDefaultInstance() => inner.CreateDefaultInstance();

    public void Format(T record, IBufferWriter<char> writer)
    {
        Interlocked.Increment(ref _formatCalls);
        if (writer is not RentedBufferWriter { NewLine: var newLine } || !string.Equals(newLine, expectedNewLine, StringComparison.Ordinal))
            Interlocked.Increment(ref _wrongWriters);
        inner.Format(record, writer);
    }
}
