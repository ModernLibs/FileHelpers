using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Synchronous engine for files containing a closed set of record types.
/// </summary>
public sealed class MultiRecordEngine
{
    private readonly IMultiRecordType[] _recordTypes;
    /// <summary>Shared error manager instance for this engine.</summary>
    public ErrorManager ErrorManager { get; } = new ErrorManager();

    /// <summary>Shortcut to <see cref="ErrorManager.ErrorMode"/>.</summary>
    public ErrorMode ErrorMode
    {
        get => ErrorManager.ErrorMode;
        set => ErrorManager.ErrorMode = value;
    }

    /// <summary>
    /// Line terminator written after each record (and inside records with <see cref="FieldInNewLineAttribute"/>
    /// fields). Defaults to <see cref="Environment.NewLine"/>, as in FileHelpers 3.x, like
    /// <see cref="FileHelperEngine{T}.NewLineForWrite"/>; set <c>"\r\n"</c> to keep the 4.8 to 4.9.1 output.
    /// </summary>
    public string NewLineForWrite { get; set; } = Environment.NewLine;

    /// <summary>Creates a multi-record engine over an AOT-safe closed record set.</summary>
    public MultiRecordEngine(params IMultiRecordType[] recordTypes)
    {
        if (recordTypes.Length == 0)
            throw new ArgumentException("At least one record type is required.", nameof(recordTypes));

        _recordTypes = recordTypes;
    }

    /// <summary>Reads mixed records from a string.</summary>
    public object[] ReadString(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var reader = new StringReader(content);
        return ReadStream(reader);
    }

    /// <summary>Reads mixed records from a file.</summary>
    public object[] ReadFile(string filePath)
    {
        using var reader = FileBuffers.OpenReader(filePath, Encoding.UTF8);
        return ReadStream(reader);
    }

    /// <summary>
    /// Reads mixed records from a text reader. Each record type keeps its own rules (multiline quoted fields,
    /// filters, field-count validation); a line that no selector matches is skipped (FileHelpers 3.x).
    /// </summary>
    public object[] ReadStream(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ErrorManager.ClearErrors();

        var records = new List<object>();
        using var source = new Parsing.TextReaderLineSource(reader);
        using var multiRecordReader = new MultiRecordReader(_recordTypes, ErrorManager);
        while (source.TryReadLine(out var line, out var ending))
        {
            if (multiRecordReader.Push(line, ending))
            {
                while (multiRecordReader.TryTake(out var record))
                    records.Add(record);
            }
        }

        multiRecordReader.Complete();
        while (multiRecordReader.TryTake(out var record))
            records.Add(record);
        return records.ToArray();
    }

    /// <summary>Writes mixed records to a string.</summary>
    public string WriteString(IEnumerable<object> records)
    {
        using var writer = new PooledStringWriter();
        WriteStream(writer, records);
        return writer.ToString();
    }

    /// <summary>Writes mixed records to a file.</summary>
    public void WriteFile(string filePath, IEnumerable<object> records)
    {
        using var writer = FileBuffers.OpenWriter(filePath, append: false, Encoding.UTF8);
        WriteStream(writer, records);
    }

    /// <summary>Writes mixed records to a text writer.</summary>
    public void WriteStream(TextWriter writer, IEnumerable<object> records)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(records);

        // One formatting buffer for every record (each record type used to write through its own WriteString).
        using var buffer = new RentedBufferWriter(4096);
        foreach (var record in records)
        {
            IMultiRecordType? recordType = null;
            foreach (var candidate in _recordTypes)
            {
                if (candidate.CanFormat(record))
                {
                    recordType = candidate;
                    break;
                }
            }

            if (recordType is null)
            {
                var availableTypes = string.Join(", ", _recordTypes.Select(t => t.GetType().Name));
                throw new ArgumentException($"Record type {record.GetType().Name} is not part of this multi-record engine. Available types: {availableTypes}.", nameof(records));
            }

            if (recordType is IMultiRecordTypeWriter typed)
                typed.Write(record, writer, NewLineForWrite, buffer);
            else
                writer.Write(recordType.Format(record));
            writer.Write(NewLineForWrite);
        }
    }
}
