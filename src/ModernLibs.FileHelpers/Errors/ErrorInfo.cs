using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Captures details about a single record that failed to parse.
/// </summary>
public sealed class ErrorInfo
{
    /// <summary>1-based line number in the original file where the error occurred.</summary>
    public int LineNumber { get; }

    /// <summary>Raw line string that caused the parse failure.</summary>
    public string RecordString { get; }

    /// <summary>
    /// The exception that caused the parse failure.
    /// Named <c>ExceptionInfo</c> to match the FileHelpers API shape.
    /// </summary>
    public Exception? ExceptionInfo { get; }

    /// <summary>Name of the field that caused the error, or null if not field-specific.</summary>
    public string? FieldName { get; }

    /// <summary>Zero-based column position within the line, or -1 if unknown.</summary>
    public int ColumnIndex { get; }

    /// <summary>Raw input text that failed to parse, or null if not available.</summary>
    public string? RawFieldValue { get; }

    /// <summary>Internal constructor; engines create <see cref="ErrorInfo"/> instances.</summary>
    internal ErrorInfo(int lineNumber, string recordString, Exception? exceptionInfo, string? fieldName = null, int columnIndex = -1, string? rawFieldValue = null)
    {
        LineNumber = lineNumber;
        RecordString = recordString;
        ExceptionInfo = exceptionInfo;
        FieldName = fieldName;
        ColumnIndex = columnIndex;
        RawFieldValue = rawFieldValue;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("Line ");
        sb.Append(LineNumber);
        if (ColumnIndex >= 0)
        {
            sb.Append(", Column ");
            sb.Append(ColumnIndex + 1);
        }
        if (FieldName != null)
        {
            sb.Append(", Field '");
            sb.Append(FieldName);
            sb.Append('\'');
        }
        sb.Append(": '");
        sb.Append(RecordString);
        sb.Append('\'');
        if (RawFieldValue != null)
        {
            sb.Append(" [raw: '");
            sb.Append(RawFieldValue);
            sb.Append("']");
        }
        if (ExceptionInfo != null)
        {
            sb.Append(" — ");
            sb.Append(ExceptionInfo.Message);
        }
        return sb.ToString();
    }
}
