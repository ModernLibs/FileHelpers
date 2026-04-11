namespace ModernLibs.FileHelpers;

/// <summary>
/// Provides a default value that is used when the parsed field is empty or whitespace-only.
/// The <paramref name="nullValue"/> must be a compile-time constant compatible with the field's type
/// (the source generator performs the type coercion).
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldNullValueAttribute : Attribute
{
    /// <summary>The value substituted when the field is empty. May be <see langword="null"/>.</summary>
    public object? NullValue { get; }

    /// <summary>Specifies the default value for an empty field.</summary>
    /// <param name="nullValue">The value to use when the field is empty (e.g., <c>0</c>, <c>""</c>, <c>false</c>).</param>
    public FieldNullValueAttribute(object? nullValue)
    {
        NullValue = nullValue;
    }
}
