// MA0048: the record types of the tokenized-read tests are grouped in one file.
#pragma warning disable MA0048

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace ModernLibs.FileHelpers.Tests.Engines.Fixtures;

/// <summary>Five plain fields, the last two <c>[FieldOptional]</c>: a record the runtime tokenizes.</summary>
[DelimitedRecord(",")]
public sealed class TokPlain
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3)] public decimal Amount { get; set; }
    [FieldOrder(4), FieldOptional] public string? Note { get; set; }
    [FieldOrder(5), FieldOptional] public int? Rank { get; set; }
}

/// <summary>
/// A hand-written tokenized descriptor for <see cref="TokPlain"/>: <see cref="TryParse"/> reads the values from
/// <see cref="RecordTokens.GetValue"/> and <see cref="RecordTokens.GetString"/> only. The legacy members forward to
/// the generated descriptor. Counts the calls of both parse methods.
/// </summary>
internal sealed class TokPlainDescriptor : ITokenizedDescriptor<TokPlain>
{
    private readonly IRecordDescriptor<TokPlain> _generated = FileHelperRegistry<TokPlain>.Descriptor;
    private int _tryParseCalls;
    private int _parseCalls;

    public int TryParseCalls => Volatile.Read(ref _tryParseCalls);

    public int ParseCalls => Volatile.Read(ref _parseCalls);

    public ReadOnlySpan<FieldDescriptor> Fields => _generated.Fields;

    public RecordOptions Options => _generated.Options;

    public TokPlain Parse(ReadOnlySpan<char> lines, int lineCount)
    {
        Interlocked.Increment(ref _parseCalls);
        return _generated.Parse(lines, lineCount);
    }

    public void Format(TokPlain record, IBufferWriter<char> writer) => _generated.Format(record, writer);

    public TokPlain CreateDefaultInstance() => _generated.CreateDefaultInstance();

    public bool TryParse(ReadOnlySpan<char> record, in RecordTokens tokens, [MaybeNullWhen(false)] out TokPlain result)
    {
        Interlocked.Increment(ref _tryParseCalls);
        result = new TokPlain
        {
            Id = int.Parse(tokens.GetValue(0), NumberStyles.Integer, CultureInfo.InvariantCulture),
            Name = tokens.GetString(1),
            Amount = decimal.Parse(tokens.GetValue(2), NumberStyles.Number, CultureInfo.InvariantCulture),
            Note = tokens.GetString(3), // the legacy code reads a missing optional string as ""
            Rank = tokens.GetValue(4).IsEmpty ? null : int.Parse(tokens.GetValue(4), NumberStyles.Integer, CultureInfo.InvariantCulture),
        };
        return true;
    }
}

/// <summary>Only plain fields: <c>,,,,</c> is a valid record of five empty strings.</summary>
[DelimitedRecord(",")]
public sealed class TokStrings
{
    [FieldOrder(1)] public string A { get; set; } = string.Empty;
    [FieldOrder(2)] public string B { get; set; } = string.Empty;
    [FieldOrder(3), FieldOptional] public string? C { get; set; }
    [FieldOrder(4), FieldOptional] public string? D { get; set; }
    [FieldOrder(5), FieldOptional] public string? E { get; set; }
}

/// <summary>Not tokenized in T15: a <c>[FieldQuoted]</c> field.</summary>
[DelimitedRecord(",")]
public sealed class TokQuoted
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldQuoted] public string Name { get; set; } = string.Empty;
}

/// <summary>Not tokenized: a two-char delimiter.</summary>
[DelimitedRecord("::")]
public sealed class TokTwoCharDelimiter
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
}

/// <summary>Not tokenized: a <c>[FieldDelimiter]</c> field.</summary>
[DelimitedRecord(",")]
public sealed class TokFieldDelimiter
{
    [FieldOrder(1), FieldDelimiter("|")] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
}

/// <summary>Not tokenized: a <c>[FieldInNewLine]</c> field.</summary>
[DelimitedRecord(",")]
public sealed class TokNewLine
{
    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2), FieldInNewLine] public string Name { get; set; } = string.Empty;
}

/// <summary>Not tokenized: a fixed-length record.</summary>
[FixedLengthRecord]
public sealed class TokFixed
{
    [FieldOrder(1), FieldFixedLength(3)] public int Id { get; set; }
    [FieldOrder(2), FieldFixedLength(5)] public string Name { get; set; } = string.Empty;
}

/// <summary>Master record of the multi-record and master-detail tests: lines that start with <c>M</c>.</summary>
[DelimitedRecord(",")]
public sealed class TokMaster
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2)] public int Id { get; set; }
    [FieldOrder(3)] public string Name { get; set; } = string.Empty;
}

/// <summary>Detail record of the multi-record and master-detail tests: lines that start with <c>D</c>.</summary>
[DelimitedRecord(",")]
public sealed class TokDetail
{
    [FieldOrder(1)] public string Kind { get; set; } = string.Empty;
    [FieldOrder(2)] public int Qty { get; set; }
    [FieldOrder(3), FieldOptional] public string? Text { get; set; }
}

/// <summary>
/// An <see cref="INotifyRead{T}"/> record: the engine parses it through <c>ParseInto</c>, so the tokenized path calls
/// <c>TryParseInto</c> (freeze F-12). The events of the last record are kept for <see cref="Show"/>.
/// </summary>
[DelimitedRecord(",")]
public sealed class TokNotify : INotifyRead<TokNotify>
{
    [FieldHidden] private string _events = string.Empty;

    [FieldOrder(1)] public int Id { get; set; }
    [FieldOrder(2)] public string Name { get; set; } = string.Empty;
    [FieldOrder(3), FieldOptional] public string? Note { get; set; }

    public string Show() => string.Create(CultureInfo.InvariantCulture, $"{Id}|{Name}|{Note ?? "<null>"}|{_events}");

    public void BeforeRead(BeforeReadEventArgs<TokNotify> e) => _events = "B:" + e.RecordLine[0];

    public void AfterRead(AfterReadEventArgs<TokNotify> e) => _events += string.Create(CultureInfo.InvariantCulture, $",A:{Id}");
}

/// <summary>
/// A hand-written descriptor with <c>fieldCount</c> string fields (the field cap test): <c>Parse</c> splits the
/// record; it cannot format.
/// </summary>
internal sealed class WideDescriptor(int fieldCount) : IRecordDescriptor<string[]>
{
    private readonly FieldDescriptor[] _fields =
        Enumerable.Range(0, fieldCount).Select(i => new FieldDescriptor("F" + i.ToString(CultureInfo.InvariantCulture), typeof(string), i)).ToArray();

    public ReadOnlySpan<FieldDescriptor> Fields => _fields;

    public RecordOptions Options => new() { Delimiter = "," };

    public string[] Parse(ReadOnlySpan<char> lines, int lineCount) => lines.ToString().Split(',');

    public void Format(string[] record, IBufferWriter<char> writer) => throw new NotSupportedException();

    public string[] CreateDefaultInstance() => [];
}
