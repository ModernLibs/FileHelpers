namespace ModernLibs.FileHelpers;

/// <summary>
/// Specifies the column name in the header row that maps to this field.
/// Enables header-based column mapping for CSV-first workflows.
/// Phase 38 HDR-03: Sep-inspired header row support.
/// </summary>
/// <remarks>
/// When <see cref="DelimitedRecordAttribute.HasHeader"/> is true, the engine parses
/// the first row as a header and maps columns to fields by name. This attribute
/// allows explicit mapping when the field name differs from the column name.
///
/// Example:
/// <code>
/// [DelimitedRecord(",", HasHeader = true)]
/// record Product
/// {
///     [FieldName("ProductID")]
///     public int Id;
///
///     [FieldName("ProductName")]
///     public string Name;
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldNameAttribute : Attribute
{
    /// <summary>The column name in the header row.</summary>
    public string Name { get; }

    /// <summary>Specifies the column name in the header row that maps to this field.</summary>
    /// <param name="name">The column name as it appears in the header row.</param>
    public FieldNameAttribute(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0)
            throw new ArgumentException("Column name cannot be empty.", nameof(name));
        Name = name;
    }
}
