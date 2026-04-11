namespace ModernLibs.FileHelpers;

/// <summary>
/// Excludes a field or property from file mapping.
/// The field is ignored during both read and write operations.
/// Use instead of <see cref="FieldNotInFileAttribute"/> (the obsolete alias).
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldHiddenAttribute : Attribute { }
