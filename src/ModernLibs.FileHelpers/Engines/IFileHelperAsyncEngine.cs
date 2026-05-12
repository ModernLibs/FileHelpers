// CA1710/MA0046: Delegate names and signatures intentionally match FileHelpers compatibility surface.
#pragma warning disable CA1710, MA0046

using System.Collections;
using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>
/// FileHelpers 3.x-compatible interface for async streaming record processing.
/// Implements the BeginRead*/ReadNext()/Close() pull model and BeginWrite*/WriteNext()/Close() push model.
/// Enables dependency injection and mocking scenarios.
/// </summary>
/// <typeparam name="T">The record type.</typeparam>
public interface IFileHelperAsyncEngine<T> : IEnumerable<T>, IDisposable
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

    /// <summary>Gets the last record read by ReadNext(), or null if no record has been read.</summary>
    T? LastRecord { get; }

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

    /// <summary>Begins reading records from a file.</summary>
    /// <param name="filePath">The path to the input file.</param>
    void BeginReadFile(string filePath);

    /// <summary>Begins reading records from a TextReader.</summary>
    /// <param name="reader">The TextReader to read from.</param>
    void BeginReadStream(TextReader reader);

    /// <summary>Begins reading records from a string.</summary>
    /// <param name="content">The input string containing delimited or fixed-length data.</param>
    void BeginReadString(string content);

    /// <summary>Reads the next record from the input stream.</summary>
    /// <returns>The next record, or null if end of input is reached.</returns>
    T? ReadNext();

    /// <summary>Reads the next <paramref name="count"/> records from the input stream.</summary>
    /// <param name="count">The number of records to read.</param>
    /// <returns>An array of records, or null if end of input is reached.</returns>
    T[] ReadNext(int count);

    /// <summary>Reads all remaining records from the input stream at once.</summary>
    /// <returns>An array of all remaining records.</returns>
    T[] ReadRemaining();

    /// <summary>Closes the current read or write operation and releases resources.</summary>
    void Close();

    /// <summary>Begins writing records to a file (overwrites if exists).</summary>
    /// <param name="filePath">The path to the output file.</param>
    void BeginWriteFile(string filePath);

    /// <summary>Begins writing records to a TextWriter.</summary>
    /// <param name="writer">The TextWriter to write to.</param>
    void BeginWriteStream(TextWriter writer);

    /// <summary>Begins writing records to an internal string buffer.</summary>
    void BeginWriteString();

    /// <summary>Ends the string write operation and returns the formatted output.</summary>
    /// <returns>The formatted string containing all written records.</returns>
    string EndWriteString();

    /// <summary>Begins appending records to an existing file.</summary>
    /// <param name="filePath">The path to the output file.</param>
    void BeginAppendFile(string filePath);

    /// <summary>Writes a single record to the output stream.</summary>
    /// <param name="record">The record to write.</param>
    void WriteNext(T record);

    /// <summary>Writes multiple records to the output stream.</summary>
    /// <param name="records">The records to write.</param>
    void WriteNext(IEnumerable<T> records);

    /// <summary>Reads all records from a file as an async enumerable with backpressure.</summary>
    /// <param name="filePath">The path to the input file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An async enumerable of records.</returns>
    IAsyncEnumerable<T> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
