namespace ModernLibs.FileHelpers.Generator.Models;

/// <summary>
/// Mirror enum for <c>ModernLibs.FileHelpers.ConverterKind</c>.
/// The generator project targets netstandard2.0 and cannot reference the net10.0 library directly.
/// Integer values MUST match ConverterKind exactly — the extractor casts (int)TypedConstant.Value
/// to this enum.
/// </summary>
internal enum GeneratorConverterKind
{
    None = 0,
    Date = 1,
    DateMultiFormat = 2,
    Byte = 3,
    SByte = 4,
    Int16 = 5,
    Int32 = 6,
    Int64 = 7,
    UInt16 = 8,
    UInt32 = 9,
    UInt64 = 10,
    Decimal = 11,
    Double = 12,
    PercentDouble = 13,
    Single = 14,
    Boolean = 15,
    Char = 16,
    Guid = 17,
    Enum = 18,
}
