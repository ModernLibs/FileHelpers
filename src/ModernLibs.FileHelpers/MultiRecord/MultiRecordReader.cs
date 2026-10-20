using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Shared read pipeline of <see cref="MultiRecordEngine"/> and <see cref="MultiRecordAsyncEngine"/>.
/// FileHelpers 3.x semantics: the selector sees the first physical line of each record; a line no selector
/// matches is skipped; the selected type then assembles its multiline record and parses it; failures go through
/// <see cref="ErrorMode"/> with the line number of the record, or for <see cref="FieldInNewLineAttribute"/>
/// records of the failing physical line. As in <see cref="FileHelperEngine{T}"/>, after a layout error in a line
/// before the last of such a record the following lines are read again as the start of the next record (they
/// go through the selector again).
/// </summary>
internal sealed class MultiRecordReader : IDisposable
{
    private readonly IMultiRecordType[] _types;
    private readonly ErrorManager _errors;
    private readonly LogicalLineAssembler _assembler = new();
    private readonly PendingLines _pending = new();
    private readonly Queue<object> _ready = new();
    private IMultiRecordType? _pendingType;
    private int _lineNumber;

    public MultiRecordReader(IMultiRecordType[] types, ErrorManager errors)
    {
        _types = types;
        _errors = errors;
    }

    /// <summary>
    /// Pushes one physical line; returns <see langword="true"/> when records are ready (<see cref="TryTake"/>).
    /// Lines read again after a failing line can make more than one record ready.
    /// </summary>
    public bool Push(ReadOnlySpan<char> line, LineEnding ending)
    {
        PushLine(line, ending, ++_lineNumber);
        return _ready.Count > 0;
    }

    /// <summary>Takes the next ready record.</summary>
    public bool TryTake([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out object? record) => _ready.TryDequeue(out record);

    /// <summary>End of input: reports a record left incomplete (unclosed quote, incomplete group).</summary>
    public void Complete()
    {
        ResumePending();
        var pending = _assembler.TakePending();
        if (pending is not null)
            ReportError(pending.StartLine, pending.Text, pending.Error);
    }

    private void PushLine(ReadOnlySpan<char> line, LineEnding ending, int lineNumber)
    {
        IMultiRecordType type;
        if (_assembler.InProgress)
        {
            type = _pendingType!;
        }
        else
        {
            if (line.IsEmpty)
                return;

            var selected = Select(line, lineNumber);
            if (selected is null)
                return;
            type = selected;
            _pendingType = selected;
        }

        var reader = type as IMultiRecordTypeReader;
        if (!_assembler.Push(line, ending, lineNumber, reader?.Rules ?? SingleLineRules.Instance, out var record))
            return;

        try
        {
            if (reader is null)
                _ready.Enqueue(type.Parse(record));
            else if (reader.TryRead(record, _assembler.FirstLineLength, _assembler.AllLinesEmpty, out var result))
                _ready.Enqueue(result!);
        }
        catch (Exception ex)
        {
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

    public void Dispose() => _assembler.Dispose();

    private IMultiRecordType? Select(ReadOnlySpan<char> line, int lineNumber)
    {
        try
        {
            foreach (var candidate in _types)
            {
                if (candidate.Matches(line))
                    return candidate;
            }
        }
        catch (Exception ex)
        {
            // 3.x: "Selector failed to process correctly", routed through ErrorMode.
            ReportError(lineNumber, line.ToString(), new FileHelpersException("Selector failed to process correctly", ex));
        }

        return null;
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

    /// <summary>Rules for custom <see cref="IMultiRecordType"/> implementations: one physical line per record.</summary>
    private sealed class SingleLineRules : ILogicalLineRules
    {
        public static readonly SingleLineRules Instance = new();

        public int LinesPerRecord => 1;

        public bool OpensMultiline(ReadOnlySpan<char> line, int groupLine, out int openField)
        {
            openField = 0;
            return false;
        }

        public bool StaysInsideQuotes(ReadOnlySpan<char> line, ref int openField) => false;

        public bool IsComment(ReadOnlySpan<char> line) => false;

        public bool IsEmptyLine(ReadOnlySpan<char> line) => line.IsEmpty;

        public bool IsFiltered(ReadOnlySpan<char> firstLine, bool allLinesEmpty) => false;
    }
}
