namespace ModernLibs.FileHelpers.Generator.Models;

/// <summary>
/// Classification of a field's (underlying, non-nullable) type, computed from the symbol in
/// the extractor so the emitter never has to guess from type-name strings.
/// </summary>
internal enum FieldValueKind
{
    /// <summary>No built-in conversion exists (MLFH013 unless a custom converter is declared).</summary>
    Unsupported = 0,
    String,
    Boolean,
    Char,
    Byte,
    SByte,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Decimal,
    Double,
    Single,
    DateTime,
    DateOnly,
    TimeOnly,
    DateTimeOffset,
    TimeSpan,
    Guid,
    Enum,

}
