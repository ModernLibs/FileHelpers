// MA0004: ConfigureAwait — library code; callers control context. Suppressed per project pattern.
#pragma warning disable MA0004

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

    /// <summary>Reads mixed records from a stream asynchronously.</summary>
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
        await foreach (var record in ReadFromStreamAsync(stream, cancellationToken))
            yield return record;
    }

    private async IAsyncEnumerable<object> ReadFromStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ErrorManager.ClearErrors();

        await using var lineReader = new PipelineLineReader(stream, Encoding);

        var lineBuffer = new StringBuilder();
        int lineNumber = 0;
        bool isBuffering = false;
        int bufferStartLine = 0;

        await foreach (var line in lineReader.ReadLinesAsync(cancellationToken))
        {
            lineNumber++;

            if (line.Length == 0)
            {
                if (isBuffering)
                    lineBuffer.Append(line.Span);
                continue;
            }

            if (!isBuffering)
            {
                if (DelimitedFieldEnumerator.HasUnclosedQuote(line.Span))
                {
                    lineBuffer.Clear();
                    lineBuffer.Append(line.Span);
                    bufferStartLine = lineNumber;
                    isBuffering = true;
                    continue;
                }

                var result = TryProcessLine(line.Span.ToString(), lineNumber, cancellationToken);
                if (result is not null)
                    yield return result;
            }
            else
            {
                lineBuffer.Append(line.Span);
                var assembled = lineBuffer.ToString();

                if (!DelimitedFieldEnumerator.HasUnclosedQuote(assembled.AsSpan()))
                {
                    isBuffering = false;
                    var result = TryProcessLine(assembled, bufferStartLine, cancellationToken);
                    if (result is not null)
                        yield return result;
                    lineBuffer.Clear();
                }
            }
        }

        if (isBuffering && lineBuffer.Length > 0)
        {
            var result = TryProcessLine(lineBuffer.ToString(), bufferStartLine, cancellationToken);
            if (result is not null)
                yield return result;
        }
    }

    private object? TryProcessLine(string line, int lineNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IMultiRecordType? recordType = null;
        foreach (var candidate in _recordTypes)
        {
            if (candidate.Matches(line.AsSpan()))
            {
                recordType = candidate;
                break;
            }
        }

        if (recordType is null)
        {
            var selectorException = CreateSelectorMissException(lineNumber, line, _recordTypes);
            switch (ErrorManager.ErrorMode)
            {
                case ErrorMode.ThrowException:
                    throw selectorException;
                case ErrorMode.IgnoreAndContinue:
                    return null;
                case ErrorMode.SaveAndContinue:
                    ErrorManager.AddError(new ErrorInfo(lineNumber, line, selectorException));
                    return null;
            }
        }

        return recordType!.Parse(line.AsSpan());
    }

    private static FormatException CreateSelectorMissException(int lineNumber, string line, IMultiRecordType[] recordTypes)
    {
        var types = string.Join(", ", recordTypes.Select(t => t.GetType().Name));
        return new FormatException($"No multi-record selector matched line {lineNumber}: '{line}'. Available record types: {types}.");
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
