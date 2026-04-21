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

    /// <summary>Internal constructor; engines create <see cref="ErrorInfo"/> instances.</summary>
    internal ErrorInfo(int lineNumber, string recordString, Exception? exceptionInfo)
    {
        LineNumber = lineNumber;
        RecordString = recordString;
        ExceptionInfo = exceptionInfo;
    }
}
