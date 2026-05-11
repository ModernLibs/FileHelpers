using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Synchronous engine for grouped master/detail files.
/// </summary>
public sealed class MasterDetailEngine<TMaster, TDetail>
{
    private readonly MasterDetailSelector _selector;
    private readonly FileHelperEngine<TMaster> _masterEngine = new();
    private readonly FileHelperEngine<TDetail> _detailEngine = new();

    /// <summary>Creates a master/detail engine with an explicit line selector.</summary>
    public MasterDetailEngine(MasterDetailSelector selector)
    {
        _selector = selector;
    }

    /// <summary>
    /// Creates a master/detail engine with a common selector mode and selector string.
    /// Matches FileHelpers 3.x API for ergonomic master/detail classification.
    /// </summary>
    public MasterDetailEngine(CommonSelectorMode mode, string selector)
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

    /// <summary>Reads grouped master/detail records from a string.</summary>
    public MasterDetails<TMaster, TDetail>[] ReadString(string content)
    {
        using var reader = new StringReader(content);
        return ReadStream(reader);
    }

    /// <summary>Reads grouped master/detail records from a file.</summary>
    public MasterDetails<TMaster, TDetail>[] ReadFile(string filePath)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return ReadStream(reader);
    }

    /// <summary>Reads grouped master/detail records from a text reader.</summary>
    public MasterDetails<TMaster, TDetail>[] ReadStream(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadFromContent(reader.ReadToEnd());
    }

    private MasterDetails<TMaster, TDetail>[] ReadFromContent(string content)
    {
        var groups = new List<MasterDetails<TMaster, TDetail>>();
        TMaster? currentMaster = default;
        List<TDetail>? currentDetails = null;
        using var lineReader = new StringReader(content);
        var newline = Parsing.MultilineLineJoin.Detect(content.AsSpan());
        var logicalLineBuffer = new StringBuilder();
        var hasPendingLogicalLine = false;
        RecordAction pendingAction = RecordAction.Skip;

        string? line;
        while ((line = lineReader.ReadLine()) is not null)
        {
            if (line.Length == 0)
                continue;

            if (!hasPendingLogicalLine)
            {
                var action = _selector(line.AsSpan());
                if (action is RecordAction.Skip)
                    continue;

                pendingAction = action;
                hasPendingLogicalLine = true;
            }

            AppendLogicalLine(logicalLineBuffer, line, newline);
            if (!TryFlushPendingLogicalLine(
                    isEndOfInput: false,
                    groups,
                    ref currentMaster,
                    ref currentDetails,
                    ref hasPendingLogicalLine,
                    ref pendingAction,
                    logicalLineBuffer))
                continue;
        }

        if (!TryFlushPendingLogicalLine(
                isEndOfInput: true,
                groups,
                ref currentMaster,
                ref currentDetails,
                ref hasPendingLogicalLine,
                ref pendingAction,
                logicalLineBuffer))
            throw new FormatException("Incomplete multiline record at end of input.");

        FlushCurrentGroup(groups, currentMaster, currentDetails);
        return groups.ToArray();
    }

    /// <summary>Writes grouped master/detail records to a string.</summary>
    public string WriteString(IEnumerable<MasterDetails<TMaster, TDetail>> groups)
    {
        using var writer = new StringWriter();
        WriteStream(writer, groups);
        return writer.ToString();
    }

    /// <summary>Writes grouped master/detail records to a file.</summary>
    public void WriteFile(string filePath, IEnumerable<MasterDetails<TMaster, TDetail>> groups)
    {
        using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
        WriteStream(writer, groups);
    }

    /// <summary>Writes grouped master/detail records to a text writer.</summary>
    public void WriteStream(TextWriter writer, IEnumerable<MasterDetails<TMaster, TDetail>> groups)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(groups);

        foreach (var group in groups)
        {
            writer.WriteLine(FormatSingle(_masterEngine, group.Master));
            foreach (var detail in group.Details)
                writer.WriteLine(FormatSingle(_detailEngine, detail));
        }
    }

    private static T ParseSingle<T>(FileHelperEngine<T> engine, string line)
    {
        var records = engine.ReadString(line);
        return records.Length == 1
            ? records[0]
            : throw new FormatException($"Expected one {typeof(T).Name} record but parsed {records.Length}.");
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
            // Keep buffering until we have a complete logical line.
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

    private static string FormatSingle<T>(FileHelperEngine<T> engine, T record) =>
        engine.WriteString([record]).TrimEnd('\r', '\n');
}
