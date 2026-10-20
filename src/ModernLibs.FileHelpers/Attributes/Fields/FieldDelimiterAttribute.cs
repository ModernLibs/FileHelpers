namespace ModernLibs.FileHelpers;

/// <summary>
/// Sets the delimiter that follows this field, instead of the record-level delimiter.
/// Enables mixed-delimiter records where different fields are followed by different separators.
/// </summary>
/// <remarks>
/// <para>As in FileHelpers 3.x, the delimiter belongs to the field it is applied to and is written and read
/// after that field. For <c>{ A; [FieldDelimiter("|")] B; C }</c> on a <c>[DelimitedRecord(",")]</c> record
/// the layout is <c>a,b|c</c>: A is followed by the record delimiter, B by <c>|</c>.</para>
/// <para>On the last field of the record the delimiter only matters for validation (it must not appear after
/// the last field) and for quoting with <see cref="QuoteMode.OptionalForWrite"/>: a value is quoted when it
/// contains the delimiter of its own field.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldDelimiterAttribute : Attribute
{
    /// <summary>The delimiter written and read after this field (overrides the record-level delimiter).</summary>
    /// <remarks>
    /// Must not be null or empty (diagnostic MLFH012).
    /// Common values: ";" (semicolon), "|" (pipe), "\t" (tab), "~" (tilde).
    /// </remarks>
    public string Delimiter { get; }

    /// <summary>Specifies the delimiter that follows this field.</summary>
    /// <param name="delimiter">The delimiter string (e.g., ";", "|", "\t"). Must not be null or empty.</param>
    public FieldDelimiterAttribute(string delimiter)
    {
        Delimiter = delimiter;
    }
}
