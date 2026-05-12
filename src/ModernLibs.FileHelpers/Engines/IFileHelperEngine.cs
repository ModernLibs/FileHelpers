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
    // Properties
    string? HeaderText { get; set; }
    string? FooterText { get; set; }
    Encoding Encoding { get; set; }
    string NewLineForWrite { get; set; }
    ErrorMode ErrorMode { get; set; }
    ErrorManager ErrorManager { get; }
    int LineNumber { get; }
    int TotalRecords { get; }
    bool RecycleRecords { get; set; }

    // Events
    event BeforeReadHandler<T>? BeforeReadRecord;
    event AfterReadHandler<T>? AfterReadRecord;
    event BeforeWriteHandler<T>? BeforeWriteRecord;
    event AfterWriteHandler<T>? AfterWriteRecord;
    event ProgressChangeHandler? Progress;

    // Read methods
    T[] ReadFile(string filePath);
    T[] ReadFile(string filePath, int maxRecords);
    T[] ReadString(string content);
    T[] ReadString(string content, int maxRecords);
    T[] ReadStream(TextReader reader);
    T[] ReadStream(TextReader reader, int maxRecords);

    // Modern streaming
    IAsyncEnumerable<T> ReadAsync(string filePath, CancellationToken cancellationToken = default);
    IEnumerable<T> ReadSync(TextReader reader);
    IEnumerable<T> ReadSync(TextReader reader, int maxRecords);
    IEnumerable<T> ReadSync(string content);
    IEnumerable<T> ReadSync(string content, int maxRecords);

    // Write methods
    void WriteFile(string filePath, IEnumerable<T> records);
    string WriteString(IEnumerable<T> records);
    void WriteStream(TextWriter writer, IEnumerable<T> records);

    // Append methods
    void AppendToFile(string filePath, T record);
    void AppendToFile(string filePath, IEnumerable<T> records);
}
