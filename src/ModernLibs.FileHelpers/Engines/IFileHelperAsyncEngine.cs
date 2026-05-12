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
    // Properties
    string? HeaderText { get; set; }
    string? FooterText { get; set; }
    Encoding Encoding { get; set; }
    string NewLineForWrite { get; set; }
    ErrorMode ErrorMode { get; set; }
    ErrorManager ErrorManager { get; }
    int LineNumber { get; }
    int TotalRecords { get; }
    T? LastRecord { get; }
    bool RecycleRecords { get; set; }

    // Events
    event BeforeReadHandler<T>? BeforeReadRecord;
    event AfterReadHandler<T>? AfterReadRecord;
    event BeforeWriteHandler<T>? BeforeWriteRecord;
    event AfterWriteHandler<T>? AfterWriteRecord;
    event ProgressChangeHandler? Progress;

    // BeginRead methods
    void BeginReadFile(string filePath);
    void BeginReadStream(TextReader reader);
    void BeginReadString(string content);

    // ReadNext methods
    T? ReadNext();
    T[] ReadNext(int count);
    T[] ReadRemaining();

    // Lifecycle
    void Close();

    // BeginWrite methods
    void BeginWriteFile(string filePath);
    void BeginWriteStream(TextWriter writer);
    void BeginWriteString();
    string EndWriteString();
    void BeginAppendFile(string filePath);

    // WriteNext methods
    void WriteNext(T record);
    void WriteNext(IEnumerable<T> records);

    // Modern async streaming
    IAsyncEnumerable<T> ReadAsync(string filePath, CancellationToken cancellationToken = default);
}
