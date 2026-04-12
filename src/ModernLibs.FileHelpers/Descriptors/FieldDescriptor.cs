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
public readonly record struct FieldDescriptor(
    string Name,
    Type FieldType,
    int Order,
    int Offset = -1,
    int Width = -1);
