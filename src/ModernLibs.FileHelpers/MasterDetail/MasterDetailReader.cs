using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Shared read pipeline of <see cref="MasterDetailEngine{TMaster, TDetail}"/> and
/// <see cref="MasterDetailAsyncEngine{TMaster, TDetail}"/>. The selector sees the first physical line of each
/// record; the master or detail record type then assembles its multiline record (its own
/// <see cref="MultilineMode"/> and quote char), applies its filters, validates and parses. Failures go through
/// <see cref="ErrorMode"/> with the line number of the record, or for <see cref="FieldInNewLineAttribute"/>
/// records of the failing physical line. As in <see cref="FileHelperEngine{T}"/>, after a layout error in a line
/// before the last of such a record the following lines are read again as the start of the next record (they
/// go through the selector again).
/// </summary>
internal sealed class MasterDetailReader<TMaster, TDetail> : IDisposable
{
    private readonly MasterDetailSelector _selector;
    private readonly RecordReadCore<TMaster> _master;
    private readonly RecordReadCore<TDetail> _detail;
    private readonly ErrorManager _errors;
    private readonly LogicalLineAssembler _assembler = new();
    private readonly PendingLines _pending = new();
    private readonly Queue<MasterDetails<TMaster, TDetail>> _completed = new();
    private RecordAction _pendingAction;
    private int _lineNumber;

    private TMaster? _currentMaster;
    private List<TDetail>? _currentDetails;
    private bool _discardDetails;

    public MasterDetailReader(MasterDetailSelector selector, RecordReadCore<TMaster> master, RecordReadCore<TDetail> detail, ErrorManager errors)
    {
        _selector = selector;
        _master = master;
        _detail = detail;
        _errors = errors;
    }

    /// <summary>
    /// Pushes one physical line; returns <see langword="true"/> when completed master groups are ready
    /// (<see cref="TryTake"/>).
    /// </summary>
    public bool Push(ReadOnlySpan<char> line, LineEnding ending)
    {
        PushLine(line, ending, ++_lineNumber);
        return _completed.Count > 0;
    }

    /// <summary>Takes the next completed master group.</summary>
    public bool TryTake([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MasterDetails<TMaster, TDetail>? group) =>
        _completed.TryDequeue(out group);

    /// <summary>End of input: reports an incomplete record and closes the last group (see <see cref="TryTake"/>).</summary>
    public void Complete()
    {
        ResumePending();
        var pending = _assembler.TakePending();
        if (pending is not null)
            ReportError(pending.StartLine, pending.Text, pending.Error);
        CloseGroup();
    }

    public void Dispose() => _assembler.Dispose();

    private void PushLine(ReadOnlySpan<char> line, LineEnding ending, int lineNumber)
    {
        if (!_assembler.InProgress)
        {
            if (line.IsEmpty)
                return;

            var action = Select(line, lineNumber);
            if (action is not (RecordAction.Master or RecordAction.Detail))
                return;
            _pendingAction = action;
        }

        ILogicalLineRules rules = _pendingAction == RecordAction.Master ? _master : _detail;
        if (!_assembler.Push(line, ending, lineNumber, rules, out var record))
            return;

        try
        {
            if (_pendingAction == RecordAction.Master)
                ReadMaster(record);
            else
                ReadDetail(record);
        }
        catch (Exception ex)
        {
            if (_pendingAction == RecordAction.Master)
            {
                // The failed master's details are dropped with it; the previous group is complete.
                CloseGroup();
                _discardDetails = true;
            }

            int startLine = _assembler.StartLine;
            int errorLine = startLine;
            if (ex is FileHelpersException located)
            {
                errorLine += located.RecordLineOffset;
                if (located.ResumeOffset >= 0 && located.ResumeOffset <= record.Length)
                    _pending.Hold(record[located.ResumeOffset..], startLine + LineSplitter.CountLineBreaks(record[..located.ResumeOffset], out _));
            }

            // The error first, then the lines after the failing one (3.x).
            ReportError(errorLine, record.ToString(), ex);
            ResumePending();
        }
    }

    /// <summary>Reads the lines after a failing line of a [FieldInNewLine] record again, through the selector.</summary>
    private void ResumePending()
    {
        while (_pending.TryRead(out var line, out int lineNumber, out var ending))
            PushLine(line, ending, lineNumber);
    }

    private void ReadMaster(ReadOnlySpan<char> record)
    {
        if (_master.IsFiltered(record[.._assembler.FirstLineLength], _assembler.AllLinesEmpty))
            return;

        var master = _master.Parse(record, recycle: false);
        CloseGroup();
        _currentMaster = master;
        _currentDetails = [];
        _discardDetails = false;
    }

    private void ReadDetail(ReadOnlySpan<char> record)
    {
        if (_detail.IsFiltered(record[.._assembler.FirstLineLength], _assembler.AllLinesEmpty))
            return;

        var detail = _detail.Parse(record, recycle: false);
        if (_currentDetails is null)
        {
            if (_discardDetails)
                return;
            throw new BadUsageException("Detail record appeared before a master record.");
        }

        _currentDetails.Add(detail);
    }

    private void CloseGroup()
    {
        if (_currentDetails is null)
            return;

        _completed.Enqueue(new MasterDetails<TMaster, TDetail>(_currentMaster!, _currentDetails));
        _currentMaster = default;
        _currentDetails = null;
    }

    private RecordAction Select(ReadOnlySpan<char> line, int lineNumber)
    {
        try
        {
            return _selector(line);
        }
        catch (Exception ex)
        {
            // 3.x: "Supplied Record selector failed to process record", routed through ErrorMode.
            ReportError(lineNumber, line.ToString(), new FileHelpersException("Supplied Record selector failed to process record", ex));
            return RecordAction.Skip;
        }
    }

    private void ReportError(int lineNumber, string recordText, Exception ex)
    {
        switch (_errors.ErrorMode)
        {
            case ErrorMode.ThrowException:
                throw RecordErrors.ForThrow(lineNumber, recordText, ex);
            case ErrorMode.SaveAndContinue:
                _errors.AddError(RecordErrors.ToErrorInfo(lineNumber, recordText, ex));
                break;
        }
    }
}
