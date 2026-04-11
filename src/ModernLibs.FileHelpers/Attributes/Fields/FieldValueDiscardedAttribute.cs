namespace ModernLibs.FileHelpers;

/// <summary>
/// Marks a field as a placeholder that is parsed from input but whose value is discarded.
/// The field occupies space in the record layout (contributing to field count or fixed-length offset)
/// but is never assigned to any record member.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldValueDiscardedAttribute : Attribute { }
