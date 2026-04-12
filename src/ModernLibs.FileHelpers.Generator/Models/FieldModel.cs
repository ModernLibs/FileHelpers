namespace ModernLibs.FileHelpers.Generator.Models;

/// <summary>
/// Equatable pipeline model representing a single mapped field within a record type.
/// Contains ONLY primitives — no ISymbol or SyntaxNode references.
/// </summary>
internal sealed record FieldModel(
    string Name,
    string TypeName,
    string FullTypeName,
    int DeclarationIndex,
    int Order,
    string? TrimMode,
    bool IsOptional,
    bool IsHidden,
    bool IsValueDiscarded,
    int FixedLength = -1,                                             // -1 = not a fixed-length field or attribute missing (sentinel)
    bool IsInitOnly = false,                                          // true for init-only properties (record class / record struct)
    GeneratorConverterKind ConverterKind = GeneratorConverterKind.None, // NEW: explicit [FieldConverter(ConverterKind.X)] annotation
    string? ConverterTypeName = null,                                  // NEW: explicit [FieldConverter(typeof(MyConverter))] annotation
    EquatableArray<string> ConverterArgs = default)                    // NEW: format strings or other converter constructor args
    : IEquatable<FieldModel>;
