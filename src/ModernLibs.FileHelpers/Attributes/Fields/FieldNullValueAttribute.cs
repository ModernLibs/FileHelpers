namespace ModernLibs.FileHelpers;

/// <summary>
/// Provides a default value that is used when the parsed field is empty or whitespace-only.
/// The <see cref="NullValue"/> must be a compile-time constant implicitly convertible to the field's
/// type (a numeric constant is also accepted for <see cref="decimal"/> fields); the source generator
/// performs the coercion and reports MLFH010 otherwise.
/// </summary>
/// <remarks>
/// Without this attribute an empty non-nullable value-type field raises
/// <see cref="NullValueNotFoundException"/> and an empty <c>T?</c> field reads as <see langword="null"/>.
/// As in FileHelpers 3.x, string fields keep their (empty) text on read.
/// </remarks>
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
