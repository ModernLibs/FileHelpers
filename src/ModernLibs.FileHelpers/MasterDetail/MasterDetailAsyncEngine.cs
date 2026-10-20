using System.Runtime.CompilerServices;
using System.Text;
using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Async streaming engine for grouped master/detail files.
/// Yields completed groups via <c>IAsyncEnumerable&lt;MasterDetails&lt;TMaster, TDetail&gt;&gt;</c>
/// over <see cref="PipelineLineReader"/>.
/// </summary>
public sealed class MasterDetailAsyncEngine<TMaster, TDetail> : IAsyncDisposable
{
    private readonly MasterDetailSelector _selector;
    private readonly FileHelperEngine<TMaster> _masterEngine = new();
    private readonly FileHelperEngine<TDetail> _detailEngine = new();

    /// <summary>Error manager for read operations; cleared at the start of every read.</summary>
    public ErrorManager ErrorManager { get; } = new();

    /// <summary>Shortcut to <see cref="ErrorManager.ErrorMode"/>.</summary>
    public ErrorMode ErrorMode
    {
        get => ErrorManager.ErrorMode;
        set => ErrorManager.ErrorMode = value;
    }

    /// <summary>Encoding used for file/stream reads. Defaults to UTF-8.</summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>Creates an async master/detail engine with an explicit line selector.</summary>
    public MasterDetailAsyncEngine(MasterDetailSelector selector)
    {
        _selector = selector;
    }

    /// <summary>
    /// Creates an async master/detail engine with a common selector mode and selector string.
    /// Matches FileHelpers 3.x API for ergonomic master/detail classification.
    /// </summary>
    public MasterDetailAsyncEngine(CommonSelectorMode mode, string selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        _selector = CreateFromCommonSelector(mode, selector);
    }

    private static MasterDetailSelector CreateFromCommonSelector(CommonSelectorMode mode, string selector)
    {
        return mode switch
        {
            CommonSelectorMode.MasterIfContains => line => line.IndexOf(selector.AsSpan(), StringComparison.Ordinal) >= 0 ? RecordAction.Master : RecordAction.Detail,
            CommonSelectorMode.MasterIfBegins => line => line.StartsWith(selector.AsSpan()) ? RecordAction.Master : RecordAction.Detail,
            CommonSelectorMode.MasterIfEnds => line => line.EndsWith(selector.AsSpan()) ? RecordAction.Master : RecordAction.Detail,
            CommonSelectorMode.MasterIfEnclosed => line => line.StartsWith(selector.AsSpan()) && line.EndsWith(selector.AsSpan()) ? RecordAction.Master : RecordAction.Detail,
            CommonSelectorMode.DetailIfContains => line => line.IndexOf(selector.AsSpan(), StringComparison.Ordinal) >= 0 ? RecordAction.Detail : RecordAction.Master,
            CommonSelectorMode.DetailIfBegins => line => line.StartsWith(selector.AsSpan()) ? RecordAction.Detail : RecordAction.Master,
            CommonSelectorMode.DetailIfEnds => line => line.EndsWith(selector.AsSpan()) ? RecordAction.Detail : RecordAction.Master,
            CommonSelectorMode.DetailIfEnclosed => line => line.StartsWith(selector.AsSpan()) && line.EndsWith(selector.AsSpan()) ? RecordAction.Detail : RecordAction.Master,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }

    /// <summary>Reads grouped master/detail records from a file asynchronously.</summary>
    public IAsyncEnumerable<MasterDetails<TMaster, TDetail>> ReadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return ReadFromFileAsync(filePath, cancellationToken);
    }

    /// <summary>Reads grouped master/detail records from a stream asynchronously. The caller owns <paramref name="stream"/>; it is left open.</summary>
    public IAsyncEnumerable<MasterDetails<TMaster, TDetail>> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return ReadFromStreamAsync(stream, cancellationToken);
    }

    private async IAsyncEnumerable<MasterDetails<TMaster, TDetail>> ReadFromFileAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        await foreach (var group in ReadFromStreamAsync(stream, cancellationToken).ConfigureAwait(false))
            yield return group;
    }

    private async IAsyncEnumerable<MasterDetails<TMaster, TDetail>> ReadFromStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ErrorManager.ClearErrors();

        var lineReader = new PipelineLineReader(stream, Encoding);
        await using var lineReaderScope = lineReader.ConfigureAwait(false);
        using var groupReader = new MasterDetailReader<TMaster, TDetail>(_selector, _masterEngine.Core, _detailEngine.Core, ErrorManager);

        while (await lineReader.ReadLineAsync(cancellationToken).ConfigureAwait(false))
        {
            if (groupReader.Push(lineReader.CurrentLine.Span, lineReader.CurrentEnding))
            {
                while (groupReader.TryTake(out var group))
                    yield return group;
            }
        }

        groupReader.Complete();
        while (groupReader.TryTake(out var group))
            yield return group;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
