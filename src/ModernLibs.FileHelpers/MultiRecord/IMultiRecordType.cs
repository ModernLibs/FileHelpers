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
    /// <remarks>
    /// For a type made by <see cref="MultiRecordType.For{T}"/>, the line break before a
    /// <see cref="FieldInNewLineAttribute"/> field is <c>"\r\n"</c> on every platform here. When
    /// <see cref="MultiRecordEngine"/> writes the record it uses its <see cref="MultiRecordEngine.NewLineForWrite"/>
    /// instead.
    /// </remarks>
    string Format(object record);
}
