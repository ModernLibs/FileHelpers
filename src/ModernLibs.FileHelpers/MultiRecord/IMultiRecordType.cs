namespace ModernLibs.FileHelpers;

/// <summary>
/// Describes one record type handled by <see cref="MultiRecordEngine"/>.
/// </summary>
public interface IMultiRecordType
{
    /// <summary>Returns true when this record type should parse the supplied line.</summary>
    bool Matches(ReadOnlySpan<char> line);

    /// <summary>Parses one physical line into a record instance.</summary>
    object Parse(ReadOnlySpan<char> line);

    /// <summary>Returns true when <paramref name="record"/> is an instance of this record type.</summary>
    bool CanFormat(object record);

    /// <summary>Formats one record instance without a trailing line terminator.</summary>
    string Format(object record);
}
