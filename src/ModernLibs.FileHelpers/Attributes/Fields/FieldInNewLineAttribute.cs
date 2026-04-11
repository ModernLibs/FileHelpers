namespace ModernLibs.FileHelpers;

/// <summary>
/// Indicates that this field begins on a new input line.
/// Used for record types that span multiple physical lines.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldInNewLineAttribute : Attribute { }
