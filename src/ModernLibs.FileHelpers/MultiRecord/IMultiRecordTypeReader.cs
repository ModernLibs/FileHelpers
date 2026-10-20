namespace ModernLibs.FileHelpers;

/// <summary>
/// Implemented by the built-in <see cref="MultiRecordType"/> entries so the multi-record engines can apply the
/// record type's own pipeline rules (multiline quoted fields, <see cref="FieldInNewLineAttribute"/>, empty /
/// comment / condition filters, field-count validation) instead of a bare <see cref="IMultiRecordType.Parse"/>.
/// </summary>
internal interface IMultiRecordTypeReader
{
    /// <summary>Line assembly rules of the record type.</summary>
    ILogicalLineRules Rules { get; }

    /// <summary>
    /// Applies the type's record filters, validates and parses. Returns <see langword="false"/> when the record is
    /// filtered out; throws on layout or conversion errors.
    /// </summary>
    bool TryRead(ReadOnlySpan<char> record, int firstLineLength, bool allLinesEmpty, out object? result);
}
