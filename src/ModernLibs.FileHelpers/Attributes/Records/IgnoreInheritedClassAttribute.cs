namespace ModernLibs.FileHelpers;

/// <summary>
/// When applied to a derived record type, instructs the source generator to exclude
/// fields inherited from base classes. Only fields declared directly on the annotated
/// type are included in the generated descriptor.
/// </summary>
/// <remarks>
/// <para>
/// This is useful when you have a shared base class with common properties but the
/// file format only maps the derived class's fields.
/// </para>
/// <example>
/// <code>
/// public class BaseRecord {
///     public int Id;
///     public string BaseField;
/// }
///
/// [IgnoreInheritedClass]
/// [DelimitedRecord(",")]
/// public class DerivedRecord : BaseRecord {
///     public string DerivedField;  // Only this field is mapped
/// }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class IgnoreInheritedClassAttribute : Attribute
{
}
