using System.Text;
using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Factory for AOT-safe multi-record descriptor entries.
/// </summary>
public static class MultiRecordType
{
    /// <summary>Creates a descriptor entry for <typeparamref name="T"/>.</summary>
    public static IMultiRecordType For<T>(MultiRecordSelector selector) =>
        new MultiRecordType<T>(selector, EnginePaths.FromSwitches);

    /// <summary>Creates a descriptor entry for <typeparamref name="T"/> with explicit read paths (tests, fuzzing, benchmarks).</summary>
    internal static IMultiRecordType For<T>(MultiRecordSelector selector, EnginePaths paths) =>
        new MultiRecordType<T>(selector, paths);
}

internal sealed class MultiRecordType<T> : IMultiRecordType, IMultiRecordTypeReader, IMultiRecordTypeWriter
{
    private readonly MultiRecordSelector _selector;
    private readonly FileHelperEngine<T> _engine;

    // The field ends of the record being parsed, when the record type is tokenized (one buffer per entry).
    private readonly TokenBuffer? _tokens;

    public MultiRecordType(MultiRecordSelector selector, EnginePaths paths)
    {
        _selector = selector;
        _engine = new FileHelperEngine<T>(FileHelperRegistry<T>.Descriptor, Encoding.UTF8, paths);
        _tokens = _engine.Core.CreateTokenBuffer();
    }

    public ILogicalLineRules Rules => _engine.Core;

    /// <summary>The record-type configuration of this entry (tests: which read path it takes).</summary>
    internal RecordReadCore<T> Core => _engine.Core;

    public bool Matches(ReadOnlySpan<char> line) => _selector(line);

    /// <summary>Validates and parses one logical record of <typeparamref name="T"/> (no header/footer or filters).</summary>
    public object Parse(ReadOnlySpan<char> line) => _engine.Core.Parse(line, recycle: false, tokens: _tokens)!;

    public bool TryRead(ReadOnlySpan<char> record, int firstLineLength, bool allLinesEmpty, out object? result)
    {
        var core = _engine.Core;
        if (core.IsFiltered(record[..firstLineLength], allLinesEmpty))
        {
            result = null;
            return false;
        }

        result = core.Parse(record, recycle: false, tokens: _tokens);
        return true;
    }

    public bool CanFormat(object record) => record is T;

    public string Format(object record)
    {
        if (record is not T typed)
            throw new ArgumentException($"Record is not a {typeof(T).Name}.", nameof(record));

        _engine.NewLineForWrite = "\r\n";
        string formatted = _engine.WriteString([typed]);
        return formatted.EndsWith("\r\n", StringComparison.Ordinal) ? formatted[..^2] : formatted;
    }

    public void Write(object record, TextWriter writer, string newLine, RentedBufferWriter buffer)
    {
        if (record is not T typed)
            throw new ArgumentException($"Record is not a {typeof(T).Name}.", nameof(record));

        _engine.NewLineForWrite = newLine;
        _engine.WriteMultiRecordText(writer, typed, buffer);
    }
}
