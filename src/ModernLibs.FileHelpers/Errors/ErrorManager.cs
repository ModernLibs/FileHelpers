using System.Globalization;
using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>Manages parse errors for engine read operations.</summary>
public sealed class ErrorManager
{
    private readonly List<ErrorInfo> _errors = new();

    /// <summary>Controls how parse failures are handled.</summary>
    public ErrorMode ErrorMode { get; set; } = ErrorMode.ThrowException;

    /// <summary>All errors captured during the most recent engine operation.</summary>
    public IReadOnlyList<ErrorInfo> Errors => _errors;

    /// <summary>Number of captured errors.</summary>
    public int ErrorCount => _errors.Count;

    /// <summary><see langword="true"/> when at least one error has been captured.</summary>
    public bool HasErrors => _errors.Count > 0;

    /// <summary>Removes all captured errors.</summary>
    public void ClearErrors() => _errors.Clear();

    /// <summary>Saves captured errors using UTF-8 encoding.</summary>
    public void SaveErrors(string filePath) => SaveErrors(filePath, Encoding.UTF8);

    /// <summary>
    /// Saves captured errors using the provided <paramref name="encoding"/>: a tab-separated report with the
    /// line, the 1-based column (as in the exception message; empty when unknown), field, record, raw value and
    /// exception message.
    /// </summary>
    public void SaveErrors(string filePath, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(encoding);

        using var writer = new StreamWriter(filePath, append: false, encoding);
        writer.WriteLine("Line\t|\tColumn\t|\tField\t|\tRecord\t|\tRawValue\t|\tException");
        writer.WriteLine(new string('-', 80));
        foreach (var err in _errors)
        {
            string column = err.ColumnIndex >= 0 ? (err.ColumnIndex + 1).ToString(CultureInfo.InvariantCulture) : string.Empty;
            writer.WriteLine($"{err.LineNumber}\t|\t{column}\t|\t{err.FieldName ?? ""}\t|\t{err.RecordString}\t|\t{err.RawFieldValue ?? ""}\t|\t{err.ExceptionInfo?.Message}");
        }
    }

    /// <summary>Internal write path for engines running in SaveAndContinue mode.</summary>
    internal void AddError(ErrorInfo error) => _errors.Add(error);
}
