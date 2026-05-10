// MA0004: ConfigureAwait — library code; callers control context. Suppressed per project pattern.
#pragma warning disable MA0004
// MA0051: ReadFromStreamAsync is intentionally cohesive — splitting would obscure the async state machine.
#pragma warning disable MA0051

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

    /// <summary>Encoding used for file/stream reads. Defaults to UTF-8.</summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>Creates an async master/detail engine with an explicit line selector.</summary>
    public MasterDetailAsyncEngine(MasterDetailSelector selector)
    {
        _selector = selector;
    }

    /// <summary>Reads grouped master/detail records from a file asynchronously.</summary>
    public IAsyncEnumerable<MasterDetails<TMaster, TDetail>> ReadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return ReadFromFileAsync(filePath, cancellationToken);
    }

    /// <summary>Reads grouped master/detail records from a stream asynchronously.</summary>
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
        await foreach (var group in ReadFromStreamAsync(stream, cancellationToken))
            yield return group;
    }

    private async IAsyncEnumerable<MasterDetails<TMaster, TDetail>> ReadFromStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var lineReader = new PipelineLineReader(stream, Encoding);

        var groups = new List<MasterDetails<TMaster, TDetail>>();
        TMaster? currentMaster = default;
        List<TDetail>? currentDetails = null;
        var logicalLineBuffer = new StringBuilder();
        var hasPendingLogicalLine = false;
        RecordAction pendingAction = RecordAction.Skip;
        string newline = "\r\n";

        await foreach (var line in lineReader.ReadLinesAsync(cancellationToken))
        {
            if (line.Length == 0)
                continue;

            if (!hasPendingLogicalLine)
            {
                var action = _selector(line.Span);
                if (action is RecordAction.Skip)
                    continue;

                newline = Parsing.MultilineLineJoin.Detect(line.Span);
                pendingAction = action;
                hasPendingLogicalLine = true;
            }

            // Always append — first line of logical record must be buffered too
            AppendLogicalLine(logicalLineBuffer, line.Span.ToString(), newline);

            if (!TryFlushPendingLogicalLine(
                    isEndOfInput: false,
                    groups,
                    ref currentMaster,
                    ref currentDetails,
                    ref hasPendingLogicalLine,
                    ref pendingAction,
                    logicalLineBuffer))
                continue;

            while (groups.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return groups[0];
                groups.RemoveAt(0);
            }
        }

        if (hasPendingLogicalLine)
        {
            if (!TryFlushPendingLogicalLine(
                    isEndOfInput: true,
                    groups,
                    ref currentMaster,
                    ref currentDetails,
                    ref hasPendingLogicalLine,
                    ref pendingAction,
                    logicalLineBuffer))
                throw new FormatException("Incomplete multiline record at end of input.");
        }

        FlushCurrentGroup(groups, currentMaster, currentDetails);

        while (groups.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return groups[0];
            groups.RemoveAt(0);
        }
    }

    private static void AppendLogicalLine(StringBuilder buffer, string line, string newline)
    {
        if (buffer.Length > 0)
            buffer.Append(newline);
        buffer.Append(line);
    }

    private bool TryFlushPendingLogicalLine(
        bool isEndOfInput,
        List<MasterDetails<TMaster, TDetail>> groups,
        ref TMaster? currentMaster,
        ref List<TDetail>? currentDetails,
        ref bool hasPendingLogicalLine,
        ref RecordAction pendingAction,
        StringBuilder logicalLineBuffer)
    {
        if (!hasPendingLogicalLine)
            return true;

        var logicalLine = logicalLineBuffer.ToString();
        if (!isEndOfInput && DelimitedFieldEnumerator.HasUnclosedQuote(logicalLine.AsSpan()))
            return false;

        try
        {
            if (pendingAction is RecordAction.Master)
                FlushCurrentGroup(groups, currentMaster, currentDetails);
            ApplyLogicalLine(pendingAction, logicalLine, ref currentMaster, ref currentDetails);
        }
        catch (FormatException) when (!isEndOfInput)
        {
            return false;
        }

        logicalLineBuffer.Clear();
        hasPendingLogicalLine = false;
        pendingAction = RecordAction.Skip;
        return true;
    }

    private static void FlushCurrentGroup(
        List<MasterDetails<TMaster, TDetail>> groups,
        TMaster? currentMaster,
        List<TDetail>? currentDetails)
    {
        if (currentDetails is not null && currentMaster is not null)
            groups.Add(new MasterDetails<TMaster, TDetail>(currentMaster, currentDetails));
    }

    private void ApplyLogicalLine(
        RecordAction action,
        string logicalLine,
        ref TMaster? currentMaster,
        ref List<TDetail>? currentDetails)
    {
        switch (action)
        {
            case RecordAction.Master:
                currentMaster = ParseSingle(_masterEngine, logicalLine);
                currentDetails = [];
                break;
            case RecordAction.Detail:
                if (currentDetails is null)
                    throw new FormatException("Detail record appeared before a master record.");
                currentDetails.Add(ParseSingle(_detailEngine, logicalLine));
                break;
        }
    }

    private static T ParseSingle<T>(FileHelperEngine<T> engine, string line)
    {
        var records = engine.ReadString(line);
        return records.Length == 1
            ? records[0]
            : throw new FormatException($"Expected one {typeof(T).Name} record but parsed {records.Length}.");
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
