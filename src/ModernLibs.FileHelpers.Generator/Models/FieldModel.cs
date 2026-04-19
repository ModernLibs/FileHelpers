namespace ModernLibs.FileHelpers.Generator.Models;

/// <summary>
/// Equatable pipeline model representing a single mapped field within a record type.
/// Contains ONLY primitives — no ISymbol or SyntaxNode references.
/// </summary>
/// <remarks>
/// <see cref="TrimMode"/> is stored as <see cref="int"/>? matching
/// <c>ModernLibs.FileHelpers.TrimMode</c> enum values: 0=None, 1=Both, 2=Left, 3=Right.
/// Stored as <c>int?</c> rather than the enum itself because the generator project targets
/// netstandard2.0 and does not reference the main library; <c>null</c> means "attribute absent".
/// </remarks>
internal sealed record FieldModel(
    string Name,
    string TypeName,
    string FullTypeName,
    int DeclarationIndex,
    int Order,
    int? TrimMode,
    bool IsOptional,
    bool IsHidden,
    bool IsValueDiscarded,
    int FixedLength = -1,                                             // -1 = not a fixed-length field or attribute missing (sentinel)
    bool IsInitOnly = false,                                          // true for init-only properties (record class / record struct)
    GeneratorConverterKind ConverterKind = GeneratorConverterKind.None, // NEW: explicit [FieldConverter(ConverterKind.X)] annotation
    string? ConverterTypeName = null,                                  // NEW: explicit [FieldConverter(typeof(MyConverter))] annotation
    EquatableArray<string> ConverterArgs = default,                    // NEW: format strings or other converter constructor args
    bool IsQuoted = false,                                             // Phase 7 D-06: from [FieldQuoted]
    char QuoteChar = '"',                                              // Phase 7 D-06: quote character from [FieldQuoted(char)]
    string? UnrecognisedConverterType = null)                          // IN-02: [FieldConverter(typeof(X))] type that isn't a ConverterBase subclass — raises MLFH004
    : IEquatable<FieldModel>;
