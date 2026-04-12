namespace ModernLibs.FileHelpers;

/// <summary>
/// Describes a single mapped field in a record layout.
/// </summary>
/// <param name="Name">The field or property name as declared in source.</param>
/// <param name="FieldType">The CLR type of the field (resolved via <c>typeof()</c> — AOT-safe).</param>
/// <param name="Order">The zero-based column index in the record layout.</param>
public readonly record struct FieldDescriptor(string Name, Type FieldType, int Order);
