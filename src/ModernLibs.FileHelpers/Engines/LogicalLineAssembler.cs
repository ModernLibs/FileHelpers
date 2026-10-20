// MA0048: ILogicalLineRules is the small contract consumed only by LogicalLineAssembler; kept together.
#pragma warning disable MA0048

using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>Per-record-type rules the <see cref="LogicalLineAssembler"/> needs.</summary>
internal interface ILogicalLineRules
{
    /// <summary>Physical lines per logical record (<see cref="FieldInNewLineAttribute"/>).</summary>
    int LinesPerRecord { get; }

    /// <summary>
    /// The line opens a quoted field that does not close on the same line; <paramref name="openField"/> is
    /// then the index of that field.
    /// </summary>
    /// <param name="line">The physical line.</param>
    /// <param name="groupLine">
    /// The line of the record it would be (0 for the first line; <see cref="FieldInNewLineAttribute"/> records
    /// have one line per group of fields).
    /// </param>
    /// <param name="openField">Index of the open field.</param>
    bool OpensMultiline(ReadOnlySpan<char> line, int groupLine, out int openField);

    /// <summary>
    /// A continuation line leaves the open quoted field still open. <paramref name="openField"/> is the index
    /// of the open field on input and of the field the line ends in on output.
    /// </summary>
    bool StaysInsideQuotes(ReadOnlySpan<char> line, ref int openField);

    /// <summary>The line is a comment (never opens a multiline record).</summary>
    bool IsComment(ReadOnlySpan<char> line);

    /// <summary>The line is empty for <see cref="IgnoreEmptyLinesAttribute"/>.</summary>
    bool IsEmptyLine(ReadOnlySpan<char> line);

    /// <summary>
    /// Record-level filters (empty line, comment, <see cref="ConditionalRecordAttribute"/>) applied to the first
    /// line of a record.
    /// </summary>
    bool IsFiltered(ReadOnlySpan<char> firstLine, bool allLinesEmpty);
}

/// <summary>
/// Turns physical lines into logical records: multiline quoted fields first (continuation lines are
/// appended verbatim, joined by their original terminator), then <see cref="FieldInNewLineAttribute"/> groups.
/// Shared by the single-type pipeline and the multi-record / master-detail readers.
/// </summary>
internal sealed class LogicalLineAssembler : IDisposable
{
    private PooledCharBuilder? _multiline;
    private bool _multilineActive;
    private int _multilineStart;
    private int _multilineField;
    private LineEnding _multilinePreviousEnding;

    private PooledCharBuilder? _group;
    private int _groupCount;
    private int _groupStart;
    private int _groupFirstLength;
    private bool _groupAllEmpty;

    private ILogicalLineRules? _rules;

    /// <summary>A logical record is partially assembled; the next line belongs to it.</summary>
    public bool InProgress => _multilineActive || _groupCount > 0;

    /// <summary>Rules of the record being assembled (valid while <see cref="InProgress"/>).</summary>
    public ILogicalLineRules? CurrentRules => _rules;

    /// <summary>First physical line of the completed record.</summary>
    public int StartLine { get; private set; }

    /// <summary>Last physical line of the completed record.</summary>
    public int EndLine { get; private set; }

    /// <summary>Length of the first assembled line within the completed record (for comment/condition filters).</summary>
    public int FirstLineLength { get; private set; }

    /// <summary>Every assembled line of the record is empty.</summary>
    public bool AllLinesEmpty { get; private set; }

    /// <summary>
    /// Adds a physical line. <paramref name="rules"/> applies when no record is in progress; otherwise the rules
    /// of the record in progress are used. Returns <see langword="true"/> when a logical record is complete;
    /// <paramref name="record"/> is then valid until the next call.
    /// </summary>
    public bool Push(ReadOnlySpan<char> line, LineEnding ending, int lineNumber, ILogicalLineRules rules, out ReadOnlySpan<char> record)
    {
        record = default;
        if (!InProgress)
            _rules = rules;
        var active = _rules!;

        if (_multilineActive)
        {
            var buffer = _multiline!;
            buffer.Append(LineSplitter.ToText(_multilinePreviousEnding));
            buffer.Append(line);
            if (active.StaysInsideQuotes(line, ref _multilineField))
            {
                _multilinePreviousEnding = ending;
                return false;
            }

            _multilineActive = false;
            return AddAssembledLine(active, buffer.WrittenSpan, _multilineStart, lineNumber, out record);
        }

        // A comment line never opens a multiline record (its quotes are not data).
        if (active.OpensMultiline(line, _groupCount, out int openField) && !(_groupCount == 0 && active.IsComment(line)))
        {
            _multiline ??= new PooledCharBuilder();
            _multiline.Clear();
            _multiline.Append(line);
            _multilineActive = true;
            _multilineField = openField;
            _multilineStart = lineNumber;
            _multilinePreviousEnding = ending;
            return false;
        }

        return AddAssembledLine(active, line, lineNumber, lineNumber, out record);
    }

    /// <summary>
    /// End of input: describes a pending incomplete record (unclosed quote or incomplete group) and clears it.
    /// </summary>
    /// <returns><see langword="null"/> when nothing is pending; otherwise the error to report.</returns>
    public PendingRecord? TakePending()
    {
        if (_multilineActive)
        {
            _multilineActive = false;
            _groupCount = 0;
            return new PendingRecord(
                _multilineStart,
                _multiline!.ToString(),
                new BadUsageException(UnclosedQuoteMessage(_multilineStart)));
        }

        if (_groupCount > 0)
        {
            int available = _groupCount;
            _groupCount = 0;
            return new PendingRecord(
                _groupStart,
                _group!.ToString(),
                new BadUsageException(
                    $"Expected {_rules!.LinesPerRecord} physical line(s) per logical record; only {available} line(s) remain after line {_groupStart}."));
        }

        return null;
    }

    public void Dispose()
    {
        _multiline?.Dispose();
        _group?.Dispose();
    }

    private bool AddAssembledLine(ILogicalLineRules rules, ReadOnlySpan<char> line, int startLine, int endLine, out ReadOnlySpan<char> record)
    {
        int linesPerRecord = rules.LinesPerRecord;
        if (linesPerRecord <= 1)
        {
            Complete(startLine, endLine, line.Length, rules.IsEmptyLine(line));
            record = line;
            return true;
        }

        record = default;

        // FileHelpers 3.x filters only the first line of a multi-line record: an empty, comment or
        // condition-excluded line there is skipped on its own instead of opening a group (continuation lines
        // are taken verbatim).
        if (_groupCount == 0 && rules.IsFiltered(line, rules.IsEmptyLine(line)))
            return false;

        var group = _group ??= new PooledCharBuilder();
        if (_groupCount == 0)
        {
            group.Clear();
            _groupStart = startLine;
            _groupFirstLength = line.Length;
            _groupAllEmpty = true;
        }
        else
        {
            group.Append('\n');
        }

        group.Append(line);
        if (!rules.IsEmptyLine(line))
            _groupAllEmpty = false;

        if (++_groupCount < linesPerRecord)
            return false;

        _groupCount = 0;
        Complete(_groupStart, endLine, _groupFirstLength, _groupAllEmpty);
        record = group.WrittenSpan;
        return true;
    }

    private void Complete(int startLine, int endLine, int firstLength, bool allEmpty)
    {
        StartLine = startLine;
        EndLine = endLine;
        FirstLineLength = firstLength;
        AllLinesEmpty = allEmpty;
    }

    /// <summary>Message for a quoted field still open at end of input.</summary>
    internal static string UnclosedQuoteMessage(int startLine) =>
        $"Unclosed quoted field starting at line {startLine}. Check that all opening quotes have matching closing quotes (use \"\" for escaped quotes within quoted fields).";

    /// <summary>Incomplete record found at end of input.</summary>
    internal sealed record PendingRecord(int StartLine, string Text, Exception Error);
}
