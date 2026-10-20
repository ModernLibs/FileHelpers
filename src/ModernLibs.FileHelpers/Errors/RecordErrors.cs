using System.Runtime.ExceptionServices;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Shared engine-side handling of an exception raised while reading one record, so every read
/// path applies <see cref="ErrorMode"/> the same way (FileHelpers 3.x semantics).
/// </summary>
internal static class RecordErrors
{
    /// <summary>
    /// <see cref="ErrorMode.ThrowException"/>: library exceptions are rethrown unchanged (stack trace
    /// preserved) after the record's line number is attached; any other exception is wrapped in a
    /// <see cref="FileHelpersException"/> carrying the line number and record text.
    /// </summary>
    /// <returns>The exception to throw. Library exceptions never return (they are rethrown here).</returns>
    internal static Exception ForThrow(int lineNumber, string recordText, Exception ex)
    {
        if (ex is FileHelpersException fhe)
        {
            fhe.AttachLineNumber(lineNumber);
            ExceptionDispatchInfo.Throw(fhe);
        }

        return new FileHelpersException(lineNumber, $"Error parsing record '{recordText}'. {ex.Message}", ex);
    }

    /// <summary>
    /// Field-count mismatch in a delimited record, typed like FileHelpers 3.x: a missing delimiter
    /// ("Delimiter not found after field") is a <see cref="FileHelpersException"/>, an extra delimiter
    /// ("Delimiter found after the last field") is a <see cref="BadUsageException"/>.
    /// </summary>
    internal static FileHelpersException FieldCount(int expected, int found, string message) =>
        found < expected ? new FileHelpersException(message) : new BadUsageException(message);

    /// <summary>
    /// <see cref="ErrorMode.SaveAndContinue"/>: builds the <see cref="ErrorInfo"/> for the failed record,
    /// attaching the line number to library exceptions and copying field context from
    /// <see cref="ConvertException"/>.
    /// </summary>
    internal static ErrorInfo ToErrorInfo(int lineNumber, string recordText, Exception ex)
    {
        if (ex is FileHelpersException fhe)
            fhe.AttachLineNumber(lineNumber);

        if (ex is ConvertException convert)
        {
            int columnIndex = convert.ColumnNumber > 0 ? convert.ColumnNumber - 1 : -1;
            string? fieldName = string.IsNullOrEmpty(convert.FieldName) ? null : convert.FieldName;
            return new ErrorInfo(lineNumber, recordText, ex, fieldName, columnIndex, convert.FieldStringValue);
        }

        return new ErrorInfo(lineNumber, recordText, ex);
    }
}
