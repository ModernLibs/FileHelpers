using System.Globalization;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Base class for all exceptions raised by the library. Matches the FileHelpers 3.x
/// <c>FileHelpers.FileHelpersException</c> shape so existing <c>catch</c> blocks keep working.
/// </summary>
/// <remarks>
/// <see cref="LineNumber"/> and <see cref="ColumnNumber"/> are <c>-1</c> when unknown. Engines attach the
/// line number of the failing record before the exception reaches the caller or the
/// <see cref="ErrorManager"/>, so <see cref="Message"/> reflects the final position.
/// </remarks>
public class FileHelpersException : Exception
{
    /// <summary>Creates an exception with a default message.</summary>
    public FileHelpersException()
    {
    }

    /// <summary>Creates an exception with the given <paramref name="message"/>.</summary>
    public FileHelpersException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with the given <paramref name="message"/> and inner exception.</summary>
    public FileHelpersException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates an exception located at <paramref name="line"/>/<paramref name="column"/>; the message is
    /// prefixed with <c>Line: N Column: C.</c> like FileHelpers 3.x.
    /// </summary>
    public FileHelpersException(int line, int column, string message)
        : base(message)
    {
        LineNumber = line;
        ColumnNumber = column;
    }

    /// <summary>Internal constructor used by engines to wrap non-library failures with a line number.</summary>
    internal FileHelpersException(int line, string message, Exception? innerException)
        : base(message, innerException)
    {
        LineNumber = line;
    }

    /// <summary>1-based line number where the problem was found, or <c>-1</c> when unknown.</summary>
    public int LineNumber { get; internal set; } = -1;

    /// <summary>1-based (estimated) column where the problem was found, or <c>-1</c> when unknown.</summary>
    public int ColumnNumber { get; internal set; } = -1;

    /// <inheritdoc/>
    public override string Message => FormatPosition(LineNumber, ColumnNumber) + base.Message;

    /// <summary>
    /// Physical lines between the first line of the record and the failing line (records spanning several
    /// lines). The engines add it to the record's first line number.
    /// </summary>
    internal int RecordLineOffset { get; set; }

    /// <summary>
    /// For a record of several lines that failed before its last line: the offset in the record text where the
    /// lines that were not consumed start (they are read again as the start of the next record, like 3.x); -1
    /// when the whole record is consumed.
    /// </summary>
    internal int ResumeOffset { get; set; } = -1;

    /// <summary>Records the 1-based line of the failing record when it is not already known.</summary>
    internal void AttachLineNumber(int lineNumber)
    {
        if (LineNumber < 0)
            LineNumber = lineNumber;
    }

    private static string FormatPosition(int line, int column)
    {
        if (line >= 0 && column >= 0)
            return string.Create(CultureInfo.InvariantCulture, $"Line: {line} Column: {column}. ");
        if (line >= 0)
            return string.Create(CultureInfo.InvariantCulture, $"Line: {line}. ");
        if (column >= 0)
            return string.Create(CultureInfo.InvariantCulture, $"Column: {column}. ");
        return string.Empty;
    }
}
