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
/// <para>
/// <see cref="AlignMode"/> is stored as <see cref="int"/>? matching
/// <c>ModernLibs.FileHelpers.AlignMode</c> enum values: 0=Left, 1=Center, 2=Right.
/// <see cref="AlignChar"/> is stored as <see cref="char"/>? where <c>null</c> means use default space.
/// </para>
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
    GeneratorConverterKind ConverterKind = GeneratorConverterKind.None, // explicit [FieldConverter(ConverterKind.X)] annotation
    string? ConverterTypeName = null,                                  // explicit [FieldConverter(typeof(MyConverter))] annotation
    bool DirectSpanConverter = false,                                  // true when ConverterTypeName is ISpanConverter<TField> (public ctor)
    EquatableArray<string> ConverterArgs = default,                    // format strings or other converter constructor args
    bool IsQuoted = false,                                             // from [FieldQuoted]
    char QuoteChar = '"',                                              // quote character from [FieldQuoted(char)]
    int QuoteMode = 1,                                                 // [FieldQuoted] QuoteMode: 0=AlwaysQuoted, 1=OptionalForRead, 2=OptionalForWrite, 3=OptionalForBoth
    int QuoteMultiline = 0,                                            // [FieldQuoted] MultilineMode (record override applied): 0=AllowForBoth, 1=AllowForRead, 2=AllowForWrite, 3=NotAllow
    string? Delimiter = null,                                           // per-field delimiter override from [FieldDelimiter]
    string? UnrecognisedConverterType = null,                          // [FieldConverter(typeof(X))] type that isn't a ConverterBase subclass — raises MLFH004
    int? AlignMode = null,                                             // [FieldAlign]: 0=Left, 1=Center, 2=Right; null = no alignment
    char? AlignChar = null,                                             // [FieldAlign] pad char; null = default space
    int OverflowMode = 0,                                                // 0=DiscardEnd, 1=Error from [FieldFixedLength]
    bool IsNotEmpty = false,                                            // from [FieldNotEmpty]
    bool IsInNewLine = false,                                           // from [FieldInNewLine]
    string? Caption = null,                                             // from [FieldCaption("Display Name")]
    string? CultureName = null,                                         // from [FieldConverter(CultureName = "fr-FR")]
    string? MissingParameterlessCtorTypeName = null,                     // recognized converter type lacks public parameterless ctor — raises MLFH006
    bool IsValueType = false,                                           // Field type is a value type (including Nullable<T>)
    bool IsNullableValueType = false,                                   // Field type is Nullable<T> — empty input reads as null
    string? NullValueExpression = null,                                 // C# expression for [FieldNullValue], already cast to the field type
    string? IncompatibleNullValueType = null,                           // [FieldNullValue] constant type not convertible to the field — raises MLFH010
    string Identifier = "",                                             // Member name escaped for C# (@class)
    string UnderlyingTypeName = "",                                     // Fully-qualified type without Nullable<T> / nullable annotation
    FieldValueKind ValueKind = FieldValueKind.Unsupported,              // Built-in conversion family of the underlying type
    bool ConverterTargetsUnderlying = false,                            // Direct ISpanConverter<T> used on a T? field
    FieldAssignKind Assign = FieldAssignKind.Setter,                    // How the parsed value reaches the record
    LocationInfo? Location = null,                                      // Member declaration location for diagnostics
    string? TrimChars = null)                                           // [FieldTrim] chars (3.x default whitespace set when none given)
    : IEquatable<FieldModel>;
