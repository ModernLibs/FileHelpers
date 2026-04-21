namespace ModernLibs.FileHelpers;

/// <summary>
/// Overrides the record-level delimiter for a specific field.
/// Enables mixed-delimiter records where different fields use different separators.
/// </summary>
/// <remarks>
/// When applied to a field or property, the specified delimiter is used instead of
/// the record-level delimiter from [DelimitedRecord]. This enables parsing formats
/// like "value1;value2,value3" where Field1 uses comma, Field2 uses semicolon.
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldDelimiterAttribute : Attribute
{
    /// <summary>The delimiter string for this field (overrides record-level delimiter).</summary>
    /// <remarks>
    /// Null or empty string falls back to the record-level delimiter.
    /// Common values: ";" (semicolon), "|" (pipe), "\t" (tab), "~" (tilde).
    /// </remarks>
    public string Delimiter { get; }

    /// <summary>Specifies a custom delimiter for this field.</summary>
    /// <param name="delimiter">The delimiter string (e.g., ";", "|", "\t"). Null or empty falls back to record-level.</param>
    public FieldDelimiterAttribute(string delimiter)
    {
        Delimiter = delimiter;
    }
}
