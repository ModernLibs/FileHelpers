// CA1710/MA0046: Delegate names and signatures intentionally match FileHelpers compatibility surface.
#pragma warning disable CA1710, MA0046

using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>
/// FileHelpers 3.x-compatible interface for batch-mode record processing.
/// Enables dependency injection and mocking scenarios.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
public interface IFileHelperEngine<T>
{
    /// <summary>Gets or sets the header text captured from ignored leading lines.</summary>
    string? HeaderText { get; set; }

    /// <summary>Gets or sets the footer text captured from ignored trailing lines.</summary>
    string? FooterText { get; set; }

    /// <summary>Gets or sets the encoding used for file I/O. Defaults to UTF-8.</summary>
    Encoding Encoding { get; set; }

    /// <summary>Gets or sets the newline sequence used when writing. Defaults to \r\n.</summary>
    string NewLineForWrite { get; set; }

    /// <summary>Gets or sets how parse errors are handled. Defaults to ThrowException.</summary>
    ErrorMode ErrorMode { get; set; }

    /// <summary>Gets the error manager for SaveAndContinue error collection.</summary>
    ErrorManager ErrorManager { get; }

    /// <summary>Gets the current line number being processed.</summary>
    int LineNumber { get; }

    /// <summary>Gets the total number of records successfully parsed.</summary>
    int TotalRecords { get; }

    /// <summary>Gets or sets whether to reuse a single record instance across callback invocations.</summary>
    bool RecycleRecords { get; set; }

    /// <summary>Occurs before a record is parsed from a line.</summary>
    event BeforeReadHandler<T>? BeforeReadRecord;

    /// <summary>Occurs after a record is parsed from a line.</summary>
    event AfterReadHandler<T>? AfterReadRecord;

    /// <summary>Occurs before a record is written to a line.</summary>
    event BeforeWriteHandler<T>? BeforeWriteRecord;

    /// <summary>Occurs after a record is written to a line.</summary>
    event AfterWriteHandler<T>? AfterWriteRecord;

    /// <summary>Occurs periodically during batch operations to report progress.</summary>
    event ProgressChangeHandler? Progress;

    /// <summary>Reads all records from a file.</summary>
    /// <param name="filePath">The path to the input file.</param>
    /// <returns>An array of parsed records.</returns>
    T[] ReadFile(string filePath);

    /// <summary>Reads at most <paramref name="maxRecords"/> records from a file.</summary>
    /// <param name="filePath">The path to the input file.</param>
    /// <param name="maxRecords">The maximum number of records to read.</param>
    /// <returns>An array of parsed records.</returns>
    T[] ReadFile(string filePath, int maxRecords);

    /// <summary>Reads all records from a string.</summary>
    /// <param name="content">The input string containing delimited or fixed-length data.</param>
    /// <returns>An array of parsed records.</returns>
    T[] ReadString(string content);

    /// <summary>Reads at most <paramref name="maxRecords"/> records from a string.</summary>
    /// <param name="content">The input string containing delimited or fixed-length data.</param>
    /// <param name="maxRecords">The maximum number of records to read.</param>
    /// <returns>An array of parsed records.</returns>
    T[] ReadString(string content, int maxRecords);

    /// <summary>Reads all records from a TextReader.</summary>
    /// <param name="reader">The TextReader to read from.</param>
    /// <returns>An array of parsed records.</returns>
    T[] ReadStream(TextReader reader);

    /// <summary>Reads at most <paramref name="maxRecords"/> records from a TextReader.</summary>
    /// <param name="reader">The TextReader to read from.</param>
    /// <param name="maxRecords">The maximum number of records to read.</param>
    /// <returns>An array of parsed records.</returns>
    T[] ReadStream(TextReader reader, int maxRecords);

    /// <summary>Reads all records from a file as an async enumerable with backpressure.</summary>
    /// <param name="filePath">The path to the input file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An async enumerable of records.</returns>
    IAsyncEnumerable<T> ReadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>Reads all records from a TextReader as a synchronous enumerable.</summary>
    /// <param name="reader">The TextReader to read from.</param>
    /// <returns>An enumerable of records.</returns>
    IEnumerable<T> ReadSync(TextReader reader);

    /// <summary>Reads at most <paramref name="maxRecords"/> records from a TextReader as a synchronous enumerable.</summary>
    /// <param name="reader">The TextReader to read from.</param>
    /// <param name="maxRecords">The maximum number of records to read.</param>
    /// <returns>An enumerable of records.</returns>
    IEnumerable<T> ReadSync(TextReader reader, int maxRecords);

    /// <summary>Reads all records from a string as a synchronous enumerable.</summary>
    /// <param name="content">The input string containing delimited or fixed-length data.</param>
    /// <returns>An enumerable of records.</returns>
    IEnumerable<T> ReadSync(string content);

    /// <summary>Reads at most <paramref name="maxRecords"/> records from a string as a synchronous enumerable.</summary>
    /// <param name="content">The input string containing delimited or fixed-length data.</param>
    /// <param name="maxRecords">The maximum number of records to read.</param>
    /// <returns>An enumerable of records.</returns>
    IEnumerable<T> ReadSync(string content, int maxRecords);

    /// <summary>Writes all records to a file.</summary>
    /// <param name="filePath">The path to the output file.</param>
    /// <param name="records">The records to write.</param>
    void WriteFile(string filePath, IEnumerable<T> records);

    /// <summary>Writes all records to a string and returns the result.</summary>
    /// <param name="records">The records to write.</param>
    /// <returns>The formatted string output.</returns>
    string WriteString(IEnumerable<T> records);

    /// <summary>Writes all records to a TextWriter.</summary>
    /// <param name="writer">The TextWriter to write to.</param>
    /// <param name="records">The records to write.</param>
    void WriteStream(TextWriter writer, IEnumerable<T> records);

    /// <summary>Appends a single record to a file.</summary>
    /// <param name="filePath">The path to the output file.</param>
    /// <param name="record">The record to append.</param>
    void AppendToFile(string filePath, T record);

    /// <summary>Appends multiple records to a file.</summary>
    /// <param name="filePath">The path to the output file.</param>
    /// <param name="records">The records to append.</param>
    void AppendToFile(string filePath, IEnumerable<T> records);
}
