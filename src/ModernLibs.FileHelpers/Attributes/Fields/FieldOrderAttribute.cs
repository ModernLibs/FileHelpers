namespace ModernLibs.FileHelpers;

/// <summary>
/// Specifies the explicit column position of a field or property within a delimited or fixed-length record.
/// Use when field declaration order differs from the desired read/write order.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldOrderAttribute : Attribute
{
    /// <summary>The 1-based column index of this field in the record layout.</summary>
    public int Order { get; }

    /// <summary>Specifies the column position of this field.</summary>
    /// <param name="order">The 1-based column index.</param>
    public FieldOrderAttribute(int order)
    {
        Order = order;
    }
}
