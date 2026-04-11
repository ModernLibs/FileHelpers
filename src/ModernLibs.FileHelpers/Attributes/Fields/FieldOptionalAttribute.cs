namespace ModernLibs.FileHelpers;

/// <summary>
/// Marks a field as optional — it may be absent from the input record.
/// When the input line has fewer fields than the record type declares, optional fields at the end
/// receive their default value rather than causing a parse error.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldOptionalAttribute : Attribute { }
