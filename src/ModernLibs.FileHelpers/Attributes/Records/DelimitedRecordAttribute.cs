namespace ModernLibs.FileHelpers;

/// <summary>
/// Marks a class or struct as a delimited record type.
/// The engine uses <see cref="Separator"/> to split each line into fields.
/// Requires at least one field decorated with <see cref="FieldOrderAttribute"/> when field declaration
/// order is ambiguous.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class DelimitedRecordAttribute : Attribute
{
    /// <summary>The string used to separate fields in each record line.</summary>
    public string Separator { get; }

    /// <summary>
    /// Record-wide override of <see cref="FieldQuotedAttribute.QuoteMultiline"/>: when this property is set,
    /// every <see cref="FieldQuotedAttribute"/> field of the record uses this mode. When it is not set, each
    /// quoted field uses its own mode (FileHelpers 3.x default: <see cref="MultilineMode.AllowForBoth"/>).
    /// </summary>
    /// <remarks>
    /// Not part of FileHelpers 3.x. The getter returns <see cref="MultilineMode.NotAllow"/> when the property is
    /// not set; the source generator only applies the value when it is set explicitly.
    /// </remarks>
    public MultilineMode MultilineMode { get; set; } = MultilineMode.NotAllow;

    /// <summary>Marks this type as a delimited record.</summary>
    /// <param name="delimiter">The separator string used to split fields (e.g., <c>","</c>, <c>"\t"</c>, <c>"|"</c>).</param>
    public DelimitedRecordAttribute(string delimiter)
    {
        Separator = delimiter;
    }
}
