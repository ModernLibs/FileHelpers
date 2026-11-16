using ModernLibs.FileHelpers.Parsing;

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
    /// A token buffer for <see cref="TryRead"/>, owned by one read (<see cref="MultiRecordReader"/>); the entry itself
    /// holds no read state, so entries can be shared by engines that read at the same time.
    /// <see langword="null"/> when the record type is not tokenized.
    /// </summary>
    TokenBuffer? CreateTokenBuffer();

    /// <summary>
    /// Applies the type's record filters, validates and parses. Returns <see langword="false"/> when the record is
    /// filtered out; throws on layout or conversion errors.
    /// </summary>
    /// <param name="record">The logical record.</param>
    /// <param name="firstLineLength">The length of its first physical line.</param>
    /// <param name="allLinesEmpty">Every physical line of the record is empty.</param>
    /// <param name="tokens">The read's buffer from <see cref="CreateTokenBuffer"/> for this type.</param>
    /// <param name="result">The record, when the method returns <see langword="true"/>.</param>
    bool TryRead(ReadOnlySpan<char> record, int firstLineLength, bool allLinesEmpty, TokenBuffer? tokens, out object? result);
}
