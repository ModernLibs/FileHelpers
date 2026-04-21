namespace ModernLibs.FileHelpers;

/// <summary>
/// Describes a single mapped field in a record layout.
/// </summary>
/// <param name="Name">The field or property name as declared in source.</param>
/// <param name="FieldType">The CLR type of the field (resolved via <c>typeof()</c> — AOT-safe).</param>
/// <param name="Order">The zero-based column index in the record layout.</param>
/// <param name="Offset">
/// For fixed-length records: the zero-based character offset of this field within the line.
/// <c>-1</c> for delimited records (no fixed position). Default: <c>-1</c>.
/// </param>
/// <param name="Width">
/// For fixed-length records: the character width of this field.
/// <c>-1</c> for delimited records (no fixed width). Default: <c>-1</c>.
/// </param>
/// <param name="Delimiter">
/// For delimited records: the per-field delimiter override from [FieldDelimiter].
/// <c>null</c> means "use record-level delimiter". Default: <c>null</c>.
/// </param>
/// <param name="IsInNewLine">
/// When <see langword="true"/>, this field begins on a new input line (from [FieldInNewLine]).
/// Default: <see langword="false"/>. Phase 14 MLIN-01.
/// </param>
public readonly record struct FieldDescriptor(
    string Name,
    Type FieldType,
    int Order,
    int Offset = -1,
    int Width = -1,
    string? Delimiter = null,
    bool IsInNewLine = false);
