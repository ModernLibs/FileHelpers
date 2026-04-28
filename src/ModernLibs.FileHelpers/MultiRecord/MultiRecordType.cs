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

internal sealed class MultiRecordType<T>(MultiRecordSelector selector) : IMultiRecordType
{
    private readonly FileHelperEngine<T> _engine = new();

    public bool Matches(ReadOnlySpan<char> line) => selector(line);

    public object Parse(ReadOnlySpan<char> line)
    {
        var records = _engine.ReadString(new string(line));
        return records.Length == 1
            ? records[0]!
            : throw new FormatException($"Expected one {typeof(T).Name} record but parsed {records.Length}.");
    }

    public bool CanFormat(object record) => record is T;

    public string Format(object record)
    {
        if (record is not T typed)
            throw new ArgumentException($"Record is not a {typeof(T).Name}.", nameof(record));

        string formatted = _engine.WriteString([typed]);
        return formatted.TrimEnd('\r', '\n');
    }
}
