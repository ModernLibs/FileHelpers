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

    /// <summary>Error manager for read operations; cleared at the start of every read.</summary>
    public ErrorManager ErrorManager { get; } = new();

    /// <summary>Shortcut to <see cref="ErrorManager.ErrorMode"/>.</summary>
    public ErrorMode ErrorMode
    {
        get => ErrorManager.ErrorMode;
        set => ErrorManager.ErrorMode = value;
    }

    /// <summary>
    /// Line terminator written after each master and detail record (and inside records with
    /// <see cref="FieldInNewLineAttribute"/> fields). Defaults to <c>"\r\n"</c> on every platform, like
    /// <see cref="FileHelperEngine{T}.NewLineForWrite"/>.
    /// </summary>
    public string NewLineForWrite { get; set; } = "\r\n";

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

    /// <summary>
    /// Reads grouped master/detail records from a text reader. Master and detail records keep their own rules
    /// (multiline quoted fields, filters, field-count validation); failures go through <see cref="ErrorMode"/>.
    /// A detail record before the first master is a <see cref="BadUsageException"/>.
    /// </summary>
    public MasterDetails<TMaster, TDetail>[] ReadStream(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ErrorManager.ClearErrors();

        var groups = new List<MasterDetails<TMaster, TDetail>>();
        using var source = new Parsing.TextReaderLineSource(reader);
        using var groupReader = new MasterDetailReader<TMaster, TDetail>(_selector, _masterEngine.Core, _detailEngine.Core, ErrorManager);
        while (source.TryReadLine(out var line, out var ending))
        {
            if (groupReader.Push(line, ending))
            {
                while (groupReader.TryTake(out var group))
                    groups.Add(group);
            }
        }

        groupReader.Complete();
        while (groupReader.TryTake(out var group))
            groups.Add(group);
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

        // The record engines write each record followed by NewLineForWrite.
        _masterEngine.NewLineForWrite = NewLineForWrite;
        _detailEngine.NewLineForWrite = NewLineForWrite;
        foreach (var group in groups)
        {
            _masterEngine.WriteStream(writer, [group.Master]);
            _detailEngine.WriteStream(writer, group.Details);
        }
    }
}
