using System.Buffers;
using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Per-record-type read configuration and the record-level steps shared by every read path:
/// line filters, multiline quote detection, delimited field-count validation and parsing
/// (with optional instance recycling). Created once per engine; immutable except for the
/// recycled instance, so it is safe to share across parallel chunks when recycling is off.
/// </summary>
internal sealed class RecordReadCore<T> : ILogicalLineRules
{
    private readonly bool _validateFieldCount;

    // Quote rules for multiline assembly of physical lines (uniform for [FieldInNewLine] records without
    // segments, i.e. hand-written descriptors whose line count does not match their fields).
    private readonly char[] _quotes;
    private readonly string?[] _trimStart;
    private readonly bool[] _multilineRead;
    private readonly string?[] _scanDelimiters;

    // Per-field quote rules, the delimiter that follows each field ([FieldDelimiter], 3.x; empty when no field
    // has one), and the physical lines of a delimited [FieldInNewLine] record (null otherwise).
    private readonly char[] _fieldQuotes;
    private readonly string?[] _fieldTrimStart;
    private readonly string?[] _fieldDelimiters;
    private readonly bool[] _fieldMultilineRead;
    private readonly LineSegment[]? _segments;
    private readonly int[]? _fieldSegment;
    private T? _recycled;

    public RecordReadCore(IRecordDescriptor<T> descriptor)
    {
        Descriptor = descriptor;
        Recyclable = descriptor as IRecyclableDescriptor<T>;
        Options = descriptor.Options;

        var opts = Options;
        if (opts.Condition != RecordCondition.None && !string.IsNullOrEmpty(opts.ConditionPattern))
            Matcher = new RecordConditionMatcher(opts.Condition, opts.ConditionPattern);

        Delimiter = opts.Delimiter ?? string.Empty;
        bool delimited = Delimiter.Length > 0;
        Multiline = delimited && opts.MultilineMode is MultilineMode.AllowForRead or MultilineMode.AllowForBoth;
        LinesPerRecord = Math.Max(1, opts.LineCountPerLogicalRecord);

        var fields = descriptor.Fields;
        FieldCount = fields.Length;
        MandatoryFieldCount = fields.Length;
        _fieldDelimiters = BuildFieldDelimiters(fields);

        // [FieldOptional]: a record may end before the trailing optional fields.
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].IsOptional)
            {
                MandatoryFieldCount = i;
                break;
            }
        }

        _validateFieldCount = delimited && FieldCount > 0;

        (_fieldQuotes, _fieldTrimStart, _fieldMultilineRead) = BuildQuoteRules(fields, opts.QuoteChar, Multiline);
        (_quotes, _trimStart, _multilineRead, _scanDelimiters) = (_fieldQuotes, _fieldTrimStart, _fieldMultilineRead, _fieldDelimiters);

        if (LinesPerRecord > 1)
        {
            (_quotes, _trimStart, _multilineRead) = UniformQuoteRules(_fieldQuotes, _fieldTrimStart, _fieldMultilineRead);
            _scanDelimiters = [];

            // [FieldInNewLine]: each line of the record is assembled and validated with the rules of its own
            // fields (3.x reads a record line by line).
            if (_validateFieldCount && BuildSegments(fields, LinesPerRecord) is { } segments)
            {
                _segments = segments;
                _fieldSegment = new int[fields.Length];
                for (int k = 0; k < segments.Length; k++)
                    _fieldSegment.AsSpan(segments[k].Start, segments[k].Count).Fill(k);
                _validateFieldCount = false;
            }
        }
    }

    /// <summary>
    /// The delimiter that follows each field: its [FieldDelimiter] (FileHelpers 3.x), or <see langword="null"/>
    /// for the record delimiter. Empty when no field has one (the record delimiter everywhere).
    /// </summary>
    private static string?[] BuildFieldDelimiters(ReadOnlySpan<FieldDescriptor> fields)
    {
        bool any = false;
        foreach (ref readonly var field in fields)
            any |= field.Delimiter is { Length: > 0 };
        if (!any)
            return [];

        var delimiters = new string?[fields.Length];
        for (int i = 0; i < fields.Length; i++)
            delimiters[i] = fields[i].Delimiter is { Length: > 0 } own ? own : null;
        return delimiters;
    }

    /// <summary>The delimiter that follows field <paramref name="index"/>.</summary>
    private string DelimiterAfter(int index) =>
        (uint)index < (uint)_fieldDelimiters.Length && _fieldDelimiters[index] is { } own ? own : Delimiter;

    private ReadOnlySpan<string?> FieldDelimiters(int start, int count) =>
        _fieldDelimiters.Length == 0 ? default : _fieldDelimiters.AsSpan(start, count);

    /// <summary>The fields of one physical line of a <see cref="FieldInNewLineAttribute"/> record.</summary>
    /// <param name="Start">Index of the first field on the line.</param>
    /// <param name="Count">Number of fields on the line (0 when the record's first field is on a new line).</param>
    /// <param name="Mandatory">Leading fields on the line before the first [FieldOptional] one.</param>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct LineSegment(int Start, int Count, int Mandatory);

    /// <summary>
    /// Splits the fields into physical lines: a new line starts at every [FieldInNewLine] field (also the first
    /// one, matching the generated line count). <see langword="null"/> when that does not match
    /// <paramref name="linesPerRecord"/> (hand-written descriptors).
    /// </summary>
    private static LineSegment[]? BuildSegments(ReadOnlySpan<FieldDescriptor> fields, int linesPerRecord)
    {
        var starts = new List<int> { 0 };
        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].IsInNewLine)
                starts.Add(i);
        }

        if (starts.Count != linesPerRecord)
            return null;

        var segments = new LineSegment[starts.Count];
        for (int k = 0; k < starts.Count; k++)
        {
            int start = starts[k];
            int end = k + 1 < starts.Count ? starts[k + 1] : fields.Length;
            int mandatory = 0;
            while (start + mandatory < end && !fields[start + mandatory].IsOptional)
                mandatory++;
            segments[k] = new LineSegment(start, end - start, mandatory);
        }

        return segments;
    }

    /// <summary>
    /// [FieldInNewLine] records are assembled per physical line without knowing which line of the group it is,
    /// so multiline detection uses one rule for every field: the first quote char, and blanks / multiline
    /// allowed when any field allows them.
    /// </summary>
    private static (char[] Quotes, string?[] TrimStart, bool[] MultilineRead) UniformQuoteRules(char[] quotes, string?[] trimStart, bool[] multilineRead)
    {
        char anyQuote = Array.Find(quotes, q => q != '\0');
        string? anyTrimStart = Array.Find(trimStart, t => t is not null);
        bool anyMultilineRead = Array.IndexOf(multilineRead, true) >= 0;
        var uniformQuotes = new char[quotes.Length];
        var uniformTrimStart = new string?[trimStart.Length];
        var uniformMultilineRead = new bool[multilineRead.Length];
        Array.Fill(uniformQuotes, anyQuote);
        Array.Fill(uniformTrimStart, anyTrimStart);
        Array.Fill(uniformMultilineRead, anyMultilineRead);
        return (uniformQuotes, uniformTrimStart, uniformMultilineRead);
    }

    /// <summary>
    /// 3.x quote rules per field: only [FieldQuoted] fields are quoted (each with its own quote char), blanks
    /// may precede the opening quote only in fields trimmed at the start, and a quoted value may span lines
    /// only when its multiline mode allows reading.
    /// </summary>
    private static (char[] Quotes, string?[] TrimStart, bool[] MultilineRead) BuildQuoteRules(ReadOnlySpan<FieldDescriptor> fields, char recordQuote, bool multiline)
    {
        var quotes = new char[fields.Length];
        var trimStart = new string?[fields.Length];
        var multilineRead = new bool[fields.Length];
        bool anyFieldQuote = false;
        for (int i = 0; i < fields.Length; i++)
            anyFieldQuote |= fields[i].QuoteChar != '\0';

        for (int i = 0; i < fields.Length; i++)
        {
            // Descriptors without per-field quote chars (hand-written ones) quote every field with the
            // record quote char, when they declare one.
            quotes[i] = anyFieldQuote ? fields[i].QuoteChar : recordQuote;
            // Characters skipped before an opening quote (3.x: the [FieldTrim] chars, only when the start is trimmed).
            trimStart[i] = fields[i].TrimMode is TrimMode.Both or TrimMode.Left ? fields[i].TrimChars ?? " \t" : null;
            multilineRead[i] = quotes[i] != '\0'
                && (anyFieldQuote ? fields[i].QuoteMultiline is MultilineMode.AllowForRead or MultilineMode.AllowForBoth : multiline);
        }

        return (quotes, trimStart, multilineRead);
    }

    /// <summary><see langword="true"/> when the record type implements <see cref="INotifyRead{T}"/>.</summary>
    public static bool HasNotifyRead { get; } = typeof(INotifyRead<T>).IsAssignableFrom(typeof(T));

    public IRecordDescriptor<T> Descriptor { get; }

    public IRecyclableDescriptor<T>? Recyclable { get; }

    public RecordOptions Options { get; }

    public RecordConditionMatcher? Matcher { get; }

    public string Delimiter { get; }

    /// <summary>Multiline quoted fields are assembled on read (<c>AllowForRead</c> or <c>AllowForBoth</c>).</summary>
    public bool Multiline { get; }

    /// <summary>Physical lines per logical record (<see cref="FieldInNewLineAttribute"/>).</summary>
    public int LinesPerRecord { get; }

    public int FieldCount { get; }

    /// <summary>Fields before the first <see cref="FieldOptionalAttribute"/> field (minimum delimited field count).</summary>
    public int MandatoryFieldCount { get; }

    /// <summary>Empty per <see cref="IgnoreEmptyLinesAttribute"/> (zero length, or whitespace with IgnoreSpaces).</summary>
    public bool IsEmptyLine(ReadOnlySpan<char> line) =>
        line.IsEmpty || (Options.IgnoreEmptySpaces && line.IsWhiteSpace());

    /// <summary>Starts with the <see cref="IgnoreCommentedLinesAttribute"/> marker.</summary>
    public bool IsComment(ReadOnlySpan<char> line) =>
        Options.CommentMarker != '\0'
        && (Options.CommentAnyPlace ? line.TrimStart() : line) is { IsEmpty: false } rest
        && rest[0] == Options.CommentMarker;

    /// <summary>
    /// Record-level filters applied to a complete logical record (after multiline assembly):
    /// empty lines, comments and <see cref="ConditionalRecordAttribute"/>.
    /// </summary>
    public bool IsFiltered(ReadOnlySpan<char> firstLine, bool allLinesEmpty)
    {
        if (Options.IgnoreEmptyLines && allLinesEmpty)
            return true;
        if (IsComment(firstLine))
            return true;
        return Matcher is not null && !Matcher.ShouldInclude(firstLine);
    }

    /// <summary>The line opens a quoted field that does not close on the same line.</summary>
    /// <remarks>
    /// A field whose multiline mode does not allow reading never opens a multiline record; its unclosed quote
    /// is reported by the generated parser (3.x: "unclosed quoted string").
    /// </remarks>
    public bool OpensMultiline(ReadOnlySpan<char> line, int groupLine, out int openField)
    {
        openField = 0;
        if (_segments is { } segments)
        {
            if (!Multiline)
                return false;
            var segment = segments[Math.Min(groupLine, segments.Length - 1)];
            int local = 0;
            if (segment.Count == 0 || !EndsInsideSegmentQuotes(line, startsInsideQuotes: false, segment, ref local) || local >= segment.Count
                || !_fieldMultilineRead[segment.Start + local])
                return false;
            openField = segment.Start + local;
            return true;
        }

        return Multiline
            && QuoteBalanceScanner.EndsInsideQuotes(line, startsInsideQuotes: false, Delimiter, _quotes, _trimStart, ref openField, _scanDelimiters)
            && (uint)openField < (uint)_multilineRead.Length
            && _multilineRead[openField];
    }

    /// <summary>A continuation line of an open quoted field leaves the field still open.</summary>
    public bool StaysInsideQuotes(ReadOnlySpan<char> line, ref int openField)
    {
        if (_segments is { } segments && (uint)openField < (uint)_fieldSegment!.Length)
        {
            var segment = segments[_fieldSegment[openField]];
            int local = openField - segment.Start;
            bool inside = EndsInsideSegmentQuotes(line, startsInsideQuotes: true, segment, ref local);
            openField = segment.Start + local;
            return inside;
        }

        return QuoteBalanceScanner.EndsInsideQuotes(line, startsInsideQuotes: true, Delimiter, _quotes, _trimStart, ref openField, _scanDelimiters);
    }

    /// <summary>Quote scan of one line of a [FieldInNewLine] record against the fields of that line only.</summary>
    private bool EndsInsideSegmentQuotes(ReadOnlySpan<char> line, bool startsInsideQuotes, LineSegment segment, ref int localField) =>
        QuoteBalanceScanner.EndsInsideQuotes(
            line,
            startsInsideQuotes,
            Delimiter,
            _fieldQuotes.AsSpan(segment.Start, segment.Count),
            _fieldTrimStart.AsSpan(segment.Start, segment.Count),
            ref localField,
            FieldDelimiters(segment.Start, segment.Count));

    /// <summary>
    /// Validates the field count of a delimited record and parses it. Throws on any layout or
    /// conversion error; the caller routes the exception through <see cref="ErrorMode"/>.
    /// </summary>
    public T Parse(ReadOnlySpan<char> logicalRecord, bool recycle)
    {
        if (_segments is not null)
            return ParseGroup(logicalRecord, target: default, hasTarget: false, recycle);

        if (_validateFieldCount)
            ValidateFieldCount(logicalRecord);
        return ParseValidated(logicalRecord, recycle);
    }

    private T ParseValidated(ReadOnlySpan<char> logicalRecord, bool recycle)
    {
        if (recycle && Recyclable is not null)
        {
            if (_recycled is null)
                _recycled = Descriptor.Parse(logicalRecord, LinesPerRecord);
            else
                Recyclable.ParseInto(logicalRecord, LinesPerRecord, _recycled);
            return _recycled;
        }

        return Descriptor.Parse(logicalRecord, LinesPerRecord);
    }

    private T ParseIntoValidated(ReadOnlySpan<char> logicalRecord, T target, bool recycle)
    {
        Recyclable!.ParseInto(logicalRecord, LinesPerRecord, target);
        if (recycle)
            _recycled = target;
        return target;
    }

    /// <summary>Validates and normalizes the physical lines of a delimited [FieldInNewLine] record, then parses it.</summary>
    private T ParseGroup(ReadOnlySpan<char> logicalRecord, T? target, bool hasTarget, bool recycle)
    {
        char[]? rented = null;
        scoped ReadOnlySpan<char> normalized = default;
        bool validated = false;
        try
        {
            normalized = NormalizeGroup(logicalRecord, _segments!, ref rented);
            validated = true;
            return hasTarget ? ParseIntoValidated(normalized, target!, recycle) : ParseValidated(normalized, recycle);
        }
        catch (FileHelpersException ex) when (validated)
        {
            LocateFailingLine(ex, normalized);
            throw;
        }
        finally
        {
            if (rented is not null)
                ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// A field of a [FieldInNewLine] record failed (conversion, empty value): like 3.x, the error is reported on
    /// the physical line of the field, with the column inside that line. The generated parser gives the column
    /// in the whole record; the normalized record keeps every line break of the original.
    /// </summary>
    private static void LocateFailingLine(FileHelpersException ex, ReadOnlySpan<char> parsed)
    {
        int offset = ex.ColumnNumber - 1;
        if (offset < 0 || offset > parsed.Length)
            return;

        ex.RecordLineOffset = LineSplitter.CountLineBreaks(parsed[..offset], out int lineStart);
        ex.ColumnNumber = offset - lineStart + 1;
    }

    /// <summary>
    /// Checks the field count of every physical line of a [FieldInNewLine] record against the fields on that
    /// line. A line before a new-line field ends with the delimiter that follows its last field (optionally
    /// followed by spaces): that is the FileHelpers 3.x layout (<c>107,37052652033,</c> / <c>166.90.252.2,111</c>),
    /// which 3.x writes and requires; the delimiter is removed before parsing. A layout error is reported on its
    /// physical line (see <see cref="AtGroupLine"/>).
    /// </summary>
    /// <returns>The record to parse: <paramref name="record"/>, or a copy in <paramref name="rented"/> without the extra delimiters.</returns>
    private ReadOnlySpan<char> NormalizeGroup(ReadOnlySpan<char> record, LineSegment[] segments, ref char[]? rented)
    {
        int pos = 0;
        int written = 0;
        for (int k = 0; k < segments.Length && pos <= record.Length; k++)
        {
            // Same line split as the generated parser: \r, \n or \r\n outside quoted values.
            var rest = record[pos..];
            var segment = segments[k];
            int nl = RecordParsing.IndexOfLineBreak(rest, Descriptor.Fields.Slice(segment.Start, segment.Count), Delimiter);
            var line = nl < 0 ? rest : rest[..nl];
            int separator = nl < 0 ? 0 : rest[nl] == '\r' && nl + 1 < rest.Length && rest[nl + 1] == '\n' ? 2 : 1;
            int next = k == segments.Length - 1 || nl < 0 ? -1 : pos + line.Length + separator;
            int keep;
            try
            {
                keep = ValidateGroupLine(line, segment, k, lastLine: k == segments.Length - 1);
            }
            catch (FileHelpersException ex)
            {
                throw AtGroupLine(ex, record, pos, next);
            }

            if (keep < line.Length && rented is null)
            {
                rented = ArrayPool<char>.Shared.Rent(record.Length);
                record[..pos].CopyTo(rented);
                written = pos;
            }

            if (rented is not null)
            {
                line[..keep].CopyTo(rented.AsSpan(written));
                written += keep;
                rest.Slice(line.Length, separator).CopyTo(rented.AsSpan(written));
                written += separator;
            }

            pos += line.Length + separator;
            if (nl < 0)
                break;
        }

        if (rented is null)
            return record;

        // Anything after the expected lines is left to the parser's line-count check.
        if (pos < record.Length)
        {
            record[pos..].CopyTo(rented.AsSpan(written));
            written += record.Length - pos;
        }

        return rented.AsSpan(0, written);
    }

    /// <summary>
    /// Validates one physical line of a [FieldInNewLine] record; returns the length to keep (without the
    /// delimiter before the line break).
    /// </summary>
    private int ValidateGroupLine(ReadOnlySpan<char> line, LineSegment segment, int index, bool lastLine)
    {
        var fields = Descriptor.Fields;
        if (segment.Count == 0)
        {
            if (!line.IsWhiteSpace())
                throw new BadUsageException($"Text '{line}' found before the new line of the field: {fields[segment.Start].Name} (this is not allowed when you use [FieldInNewLine]).");
            return line.Length;
        }

        int found = CountGroupLineFields(line, segment, out int lastField, out bool unclosed);
        if (unclosed && found <= segment.Count)
            throw UnclosedQuote(fields[segment.Start + lastField].Name, _fieldQuotes[segment.Start + lastField]);

        int keep = line.Length;
        if (found == segment.Count + 1 && !lastLine && TrailingDelimiterStart(line, DelimiterAfter(segment.Start + segment.Count - 1)) is var cut and >= 0
            && CountGroupLineFields(line[..cut], segment, out _, out _) == segment.Count)
        {
            keep = cut;
            found = segment.Count;
        }

        if (found >= segment.Mandatory && found <= segment.Count)
        {
            // 3.x: the last field of the line is not the last of the record, so its delimiter must follow it
            // unless the next field (the first on the next line) is optional or the line ends with a quoted value.
            int next = segment.Start + segment.Count;
            if (!lastLine && found == segment.Count && keep == line.Length && !fields[next].IsOptional && !EndsWithQuotedValue(line, segment))
                throw MissingLineDelimiter(line, index, next - 1);
            return keep;
        }

        int expected = found < segment.Mandatory ? segment.Mandatory : segment.Count;
        string expectedText = segment.Mandatory == segment.Count ? $"{segment.Count}" : $"{segment.Mandatory} to {segment.Count}";
        throw RecordErrors.FieldCount(
            expected,
            found,
            $"Expected {expectedText} fields but found {found} in physical line {index + 1} of the record: '{line}'.{DelimiterDetail(found, segment.Start, segment.Count)} Check that all delimiters are present and no fields contain unescaped delimiter characters.");
    }

    /// <summary>
    /// A layout error in a line of a [FieldInNewLine] record: reported on that physical line, and, like 3.x,
    /// the lines after it are read again as the start of the next record.
    /// </summary>
    /// <param name="ex">The error.</param>
    /// <param name="record">The record text.</param>
    /// <param name="lineStart">Offset of the failing line in <paramref name="record"/>.</param>
    /// <param name="resumeAt">Offset of the next line, or -1 when the failing line is the last one.</param>
    private static FileHelpersException AtGroupLine(FileHelpersException ex, ReadOnlySpan<char> record, int lineStart, int resumeAt)
    {
        ex.RecordLineOffset = LineSplitter.CountLineBreaks(record[..lineStart], out _);
        ex.ResumeOffset = resumeAt;
        return ex;
    }

    /// <summary>
    /// The last field of line <paramref name="index"/> of a [FieldInNewLine] record, <paramref name="last"/>, is
    /// not followed by its delimiter. 3.x requires it because the field is not the last one of the record
    /// ("Delimiter not found after field"), and then reads the next lines as the start of the next record.
    /// </summary>
    private FileHelpersException MissingLineDelimiter(ReadOnlySpan<char> line, int index, int last)
    {
        var fields = Descriptor.Fields;
        return new FileHelpersException(
            $"Delimiter '{DelimiterAfter(last)}' not found after field '{fields[last].Name}' (the record has less fields, the delimiter is wrong or the next field must be marked as optional). The next field, {fields[last + 1].Name}, is on a new line, so physical line {index + 1} of the record must end with the delimiter: '{line}'.");
    }

    /// <summary>
    /// The line ends with the quoted value of its last field (3.x accepts the end of the line instead of the
    /// delimiter after a closing quote; blanks may follow when the field trims its end).
    /// </summary>
    private bool EndsWithQuotedValue(ReadOnlySpan<char> line, LineSegment segment)
    {
        int end = QuoteBalanceScanner.QuotedValueEnd(
            line,
            Delimiter,
            _fieldQuotes.AsSpan(segment.Start, segment.Count),
            _fieldTrimStart.AsSpan(segment.Start, segment.Count),
            FieldDelimiters(segment.Start, segment.Count));
        if (end < 0)
            return false;

        var rest = line[end..];
        ref readonly var field = ref Descriptor.Fields[segment.Start + segment.Count - 1];
        return rest.IsEmpty
            || (field.TrimMode is TrimMode.Both or TrimMode.Right && rest.Trim(field.TrimChars ?? " \t").IsEmpty);
    }

    private int CountGroupLineFields(ReadOnlySpan<char> line, LineSegment segment, out int lastField, out bool unclosed)
    {
        lastField = 0;
        unclosed = EndsInsideSegmentQuotes(line, startsInsideQuotes: false, segment, ref lastField);
        return lastField + 1;
    }

    /// <summary>Start of a trailing <paramref name="delimiter"/> (optionally followed by spaces), or -1.</summary>
    private static int TrailingDelimiterStart(ReadOnlySpan<char> line, string delimiter)
    {
        if (line.EndsWith(delimiter, StringComparison.Ordinal))
            return line.Length - delimiter.Length;
        var trimmed = line.TrimEnd(' ');
        return trimmed.EndsWith(delimiter, StringComparison.Ordinal) ? trimmed.Length - delimiter.Length : -1;
    }

    /// <summary>
    /// For records with [FieldDelimiter] fields: the FileHelpers 3.x description of the delimiter that is missing
    /// or extra (fields <paramref name="start"/> .. <paramref name="start"/> + <paramref name="count"/> - 1 were
    /// expected, <paramref name="found"/> were found). Empty for records with one delimiter.
    /// </summary>
    private string DelimiterDetail(int found, int start, int count)
    {
        if (_fieldDelimiters.Length == 0)
            return string.Empty;

        var fields = Descriptor.Fields;
        if (found > count)
        {
            int last = start + count - 1;
            return $" Delimiter '{DelimiterAfter(last)}' found after the last field '{fields[last].Name}'.";
        }

        int before = start + found - 1;
        return $" Delimiter '{DelimiterAfter(before)}' not found after field '{fields[before].Name}'.";
    }

    /// <summary>
    /// The instance the next parse fills, for <see cref="INotifyRead{T}.BeforeRead"/>: the recycled instance,
    /// or a new default instance. Only descriptors that can parse into an existing instance have one.
    /// </summary>
    public bool TryGetParseTarget(bool recycle, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T target)
    {
        if (Recyclable is null)
        {
            target = default;
            return false;
        }

        target = recycle && _recycled is not null ? _recycled : Recyclable.CreateDefaultInstance();
        return true;
    }

    /// <summary>
    /// Validates and parses into <paramref name="target"/> (from <see cref="TryGetParseTarget"/>), which then
    /// becomes the recycled instance when <paramref name="recycle"/> is set.
    /// </summary>
    public T ParseInto(ReadOnlySpan<char> logicalRecord, T target, bool recycle)
    {
        if (_segments is not null)
            return ParseGroup(logicalRecord, target, hasTarget: true, recycle);

        if (_validateFieldCount)
            ValidateFieldCount(logicalRecord);
        return ParseIntoValidated(logicalRecord, target, recycle);
    }

    private void ValidateFieldCount(ReadOnlySpan<char> logicalRecord)
    {
        int lastField = 0;
        bool unclosed = QuoteBalanceScanner.EndsInsideQuotes(logicalRecord, startsInsideQuotes: false, Delimiter, _quotes, _trimStart, ref lastField, _fieldDelimiters);
        int found = lastField + 1;
        if (unclosed && found <= FieldCount)
        {
            // The open quote swallowed the rest of the record, so the count is meaningless; report the quote
            // (3.x: BadUsageException "unclosed quoted string", whatever the field position).
            throw UnclosedQuote(Descriptor.Fields[lastField].Name, _quotes[lastField]);
        }

        if (found >= MandatoryFieldCount && found <= FieldCount)
            return;

        int expected = found < MandatoryFieldCount ? MandatoryFieldCount : FieldCount;
        string expectedText = MandatoryFieldCount == FieldCount ? $"{FieldCount}" : $"{MandatoryFieldCount} to {FieldCount}";
        throw RecordErrors.FieldCount(
            expected,
            found,
            $"Expected {expectedText} fields but found {found} in line: '{logicalRecord}'.{DelimiterDetail(found, 0, FieldCount)} Check that all delimiters are present and no fields contain unescaped delimiter characters.");
    }

    /// <summary>Same text as the generated parser reports for an unclosed quote in the last field.</summary>
    private static BadUsageException UnclosedQuote(string field, char quote) =>
        new($"The field '{field}' has an unclosed quoted string. Check that every opening {quote} has a closing {quote} (use {quote}{quote} for a quote inside the value) or allow multiline values in [FieldQuoted].");
}
