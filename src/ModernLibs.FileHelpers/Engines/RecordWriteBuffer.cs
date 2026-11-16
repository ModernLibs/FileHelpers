namespace ModernLibs.FileHelpers;

/// <summary>
/// The storage a <see cref="RecordWriter"/> formats records into: an array, the length of the text committed so far,
/// and the newline of the write session. A record starts at the committed end; the writer holds the span and the
/// position, and the record is committed only when <see cref="Format{T}"/> returns, so a record whose formatting
/// throws leaves nothing. Only growth is virtual.
/// </summary>
/// <remarks>
/// Internal and not frozen (T22 freeze, section 3): a later task may add implementations, for example over the
/// chunks of <see cref="PooledStringWriter"/> (T24).
/// </remarks>
internal abstract class RecordWriteBuffer
{
    /// <summary>The storage; the committed text is <c>[0, Committed)</c>, the record being formatted follows it.</summary>
    private protected char[] Storage;

    /// <summary>The length of the committed text: where the next record starts.</summary>
    private protected int Committed;

    private protected RecordWriteBuffer(char[] storage) => Storage = storage;

    /// <summary>
    /// The engine's <c>NewLineForWrite</c>: written by <see cref="RecordWriter.WriteNewLine"/> and, for 4.8 and 4.9
    /// descriptors, by <see cref="RecordFormatting.WriteFieldNewLine"/> before each <see cref="FieldInNewLineAttribute"/>
    /// field. <c>"\r\n"</c> until an engine sets it.
    /// </summary>
    public string NewLine { get; set; } = "\r\n";

    /// <summary>
    /// Formats <paramref name="record"/> after the committed text and commits it when
    /// <see cref="IRecordWriterDescriptor{T}.Format(T, ref RecordWriter)"/> returns. When it throws, the committed
    /// text is unchanged.
    /// </summary>
    public void Format<T>(IRecordWriterDescriptor<T> descriptor, T record)
    {
        var writer = new RecordWriter(this, Storage.AsSpan(Committed));
        descriptor.Format(record, ref writer);

        // Position throws InvalidOperationException when the descriptor replaced the writer with default.
        int length = writer.Position;
        if ((uint)length > (uint)(Storage.Length - Committed))
            throw new InvalidOperationException("The record writer does not belong to this buffer.");
        Committed += length;
    }

    /// <summary>
    /// Moves the record being formatted (its first <paramref name="used"/> chars, which start at the committed end) to
    /// storage with at least <paramref name="minFree"/> chars free after it, and returns the span from the record start
    /// to the end of the storage. The committed text is kept.
    /// </summary>
    internal abstract Span<char> GrowRecord(int used, int minFree);
}
