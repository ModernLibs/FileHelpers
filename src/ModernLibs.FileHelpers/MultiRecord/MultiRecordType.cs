namespace ModernLibs.FileHelpers;

/// <summary>
/// Factory for AOT-safe multi-record descriptor entries.
/// </summary>
public static class MultiRecordType
{
    /// <summary>Creates a descriptor entry for <typeparamref name="T"/>.</summary>
    public static IMultiRecordType For<T>(MultiRecordSelector selector) =>
        new MultiRecordType<T>(selector);
}

internal sealed class MultiRecordType<T>(MultiRecordSelector selector) : IMultiRecordType, IMultiRecordTypeReader, IMultiRecordTypeWriter
{
    private readonly FileHelperEngine<T> _engine = new();

    public ILogicalLineRules Rules => _engine.Core;

    public bool Matches(ReadOnlySpan<char> line) => selector(line);

    /// <summary>Validates and parses one logical record of <typeparamref name="T"/> (no header/footer or filters).</summary>
    public object Parse(ReadOnlySpan<char> line) => _engine.Core.Parse(line, recycle: false)!;

    public bool TryRead(ReadOnlySpan<char> record, int firstLineLength, bool allLinesEmpty, out object? result)
    {
        var core = _engine.Core;
        if (core.IsFiltered(record[..firstLineLength], allLinesEmpty))
        {
            result = null;
            return false;
        }

        result = core.Parse(record, recycle: false);
        return true;
    }

    public bool CanFormat(object record) => record is T;

    public string Format(object record) => Format(record, "\r\n");

    public string Format(object record, string newLine)
    {
        if (record is not T typed)
            throw new ArgumentException($"Record is not a {typeof(T).Name}.", nameof(record));

        _engine.NewLineForWrite = newLine;
        string formatted = _engine.WriteString([typed]);
        return formatted.EndsWith(newLine, StringComparison.Ordinal) ? formatted[..^newLine.Length] : formatted;
    }
}
