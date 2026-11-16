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

    public MultiRecordType(MultiRecordSelector selector, EnginePaths paths)
        : this(selector, FileHelperRegistry<T>.Descriptor, paths)
    {
    }

    /// <summary>An entry over <paramref name="descriptor"/> (tests: a checking descriptor).</summary>
    internal MultiRecordType(MultiRecordSelector selector, IRecordDescriptor<T> descriptor, EnginePaths paths)
    {
        _selector = selector;
        _engine = new FileHelperEngine<T>(descriptor, Encoding.UTF8, paths);
    }

    public ILogicalLineRules Rules => _engine.Core;

    /// <summary>The record-type configuration of this entry (tests: which read path it takes).</summary>
    internal RecordReadCore<T> Core => _engine.Core;

    public bool Matches(ReadOnlySpan<char> line) => _selector(line);

    /// <summary>
    /// Validates and parses one logical record of <typeparamref name="T"/> (no header/footer or filters). The legacy
    /// path: this public member holds no buffer, so it stays safe to call from several threads, as in 4.9.
    /// </summary>
    public object Parse(ReadOnlySpan<char> line) => _engine.Core.Parse(line, recycle: false)!;

    public TokenBuffer? CreateTokenBuffer() => _engine.Core.CreateTokenBuffer();

    public bool TryRead(ReadOnlySpan<char> record, int firstLineLength, bool allLinesEmpty, TokenBuffer? tokens, out object? result)
    {
        var core = _engine.Core;
        if (core.IsFiltered(record[..firstLineLength], allLinesEmpty))
        {
            result = null;
            return false;
        }

        result = core.Parse(record, recycle: false, tokens: tokens);
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
