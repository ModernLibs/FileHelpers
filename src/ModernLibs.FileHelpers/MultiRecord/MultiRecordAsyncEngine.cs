using System.Runtime.CompilerServices;
using System.Text;
using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Async streaming engine for files containing a closed set of record types.
/// Yields records via <c>IAsyncEnumerable&lt;object&gt;</c> over <see cref="PipelineLineReader"/>.
/// </summary>
public sealed class MultiRecordAsyncEngine : IAsyncDisposable
{
    private readonly IMultiRecordType[] _recordTypes;

    /// <summary>Shared error manager instance for this engine.</summary>
    public ErrorManager ErrorManager { get; } = new();

    /// <summary>Shortcut to <see cref="ErrorManager.ErrorMode"/>.</summary>
    public ErrorMode ErrorMode
    {
        get => ErrorManager.ErrorMode;
        set => ErrorManager.ErrorMode = value;
    }

    /// <summary>Encoding used for file/stream reads. Defaults to UTF-8.</summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>Creates an async multi-record engine over an AOT-safe closed record set.</summary>
    public MultiRecordAsyncEngine(params IMultiRecordType[] recordTypes)
    {
        if (recordTypes.Length == 0)
            throw new ArgumentException("At least one record type is required.", nameof(recordTypes));

        _recordTypes = recordTypes;
    }

    /// <summary>Reads mixed records from a file asynchronously.</summary>
    public IAsyncEnumerable<object> ReadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return ReadFromFileAsync(filePath, cancellationToken);
    }

    /// <summary>Reads mixed records from a stream asynchronously. The caller owns <paramref name="stream"/>; it is left open.</summary>
    public IAsyncEnumerable<object> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return ReadFromStreamAsync(stream, cancellationToken);
    }

    private async IAsyncEnumerable<object> ReadFromFileAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        await foreach (var record in ReadFromStreamAsync(stream, cancellationToken).ConfigureAwait(false))
            yield return record;
    }

    private async IAsyncEnumerable<object> ReadFromStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ErrorManager.ClearErrors();

        var lineReader = new PipelineLineReader(stream, Encoding);
        await using var lineReaderScope = lineReader.ConfigureAwait(false);
        using var multiRecordReader = new MultiRecordReader(_recordTypes, ErrorManager);

        while (await lineReader.ReadLineAsync(cancellationToken).ConfigureAwait(false))
        {
            if (multiRecordReader.Push(lineReader.CurrentLine.Span, lineReader.CurrentEnding))
            {
                while (multiRecordReader.TryTake(out var record))
                    yield return record;
            }
        }

        multiRecordReader.Complete();
        while (multiRecordReader.TryTake(out var record))
            yield return record;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
