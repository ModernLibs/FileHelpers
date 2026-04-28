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
        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return ReadStream(reader);
    }

    /// <summary>Reads mixed records from a text reader.</summary>
    public object[] ReadStream(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ErrorManager.ClearErrors();

        var records = new List<object>();
        string? line;
        int lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (line.Length == 0)
                continue;
            var currentLine = line;

            IMultiRecordType? recordType = null;
            foreach (var candidate in _recordTypes)
            {
                if (candidate.Matches(currentLine.AsSpan()))
                {
                    recordType = candidate;
                    break;
                }
            }

            if (recordType is null)
            {
                var selectorException = CreateSelectorMissException(lineNumber, currentLine);
                switch (ErrorManager.ErrorMode)
                {
                    case ErrorMode.ThrowException:
                        throw selectorException;
                    case ErrorMode.IgnoreAndContinue:
                        continue;
                    case ErrorMode.SaveAndContinue:
                        ErrorManager.AddError(new ErrorInfo(lineNumber, currentLine, selectorException));
                        continue;
                }
            }

            records.Add(recordType!.Parse(currentLine.AsSpan()));
        }

        return records.ToArray();
    }

    /// <summary>Writes mixed records to a string.</summary>
    public string WriteString(IEnumerable<object> records)
    {
        using var writer = new StringWriter();
        WriteStream(writer, records);
        return writer.ToString();
    }

    /// <summary>Writes mixed records to a file.</summary>
    public void WriteFile(string filePath, IEnumerable<object> records)
    {
        using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
        WriteStream(writer, records);
    }

    /// <summary>Writes mixed records to a text writer.</summary>
    public void WriteStream(TextWriter writer, IEnumerable<object> records)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(records);

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
                throw new ArgumentException($"Record type {record.GetType().Name} is not part of this multi-record engine.", nameof(records));

            writer.WriteLine(recordType.Format(record));
        }
    }

    private static FormatException CreateSelectorMissException(int lineNumber, string line)
    {
        return new FormatException($"No multi-record selector matched line {lineNumber}: '{line}'.");
    }
}
