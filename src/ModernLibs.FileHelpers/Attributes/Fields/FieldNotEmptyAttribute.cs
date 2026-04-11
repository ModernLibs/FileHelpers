namespace ModernLibs.FileHelpers;

/// <summary>
/// Validates that the parsed field value is not empty or whitespace.
/// The source generator emits a validation check; an empty value causes a parse exception.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldNotEmptyAttribute : Attribute { }
