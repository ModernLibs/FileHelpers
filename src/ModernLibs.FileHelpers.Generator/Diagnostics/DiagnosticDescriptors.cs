using Microsoft.CodeAnalysis;

namespace ModernLibs.FileHelpers.Generator.Diagnostics;

/// <summary>
/// Compile-time diagnostic descriptors emitted by the FileHelpers source generator.
/// All diagnostics use category "FileHelpers.Generator" for IDE filtering consistency.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <summary>
    /// Custom tags of the blocking errors (no working descriptor is generated). They
    /// cannot be suppressed with <c>#pragma warning disable</c>, an <c>.editorconfig</c> severity or
    /// <c>NoWarn</c>; when they are hidden anyway (<c>[SuppressMessage]</c>, analyzers turned off) the
    /// registered descriptor throws (see <c>InvalidRecordEmitter</c>).
    /// </summary>
    private static readonly string[] NotConfigurable = [WellKnownDiagnosticTags.NotConfigurable];

    /// <summary>
    /// MLFH001: Type used with FileHelperRegistry or FileHelperEngine lacks [DelimitedRecord]
    /// or [FixedLengthRecord].
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH001MissingRecordAttribute = new(
        id: "MLFH001",
        title: "Missing record attribute",
        messageFormat: "Type '{0}' is used with FileHelperEngine<T> or FileHelperRegistry<T> but lacks [DelimitedRecord] or [FixedLengthRecord]. How to fix: Add [DelimitedRecord(\",\")] or [FixedLengthRecord(FixedMode.ExactLength)] to the type declaration.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Add [DelimitedRecord(\",\")] for delimiter-separated files (CSV, TSV, pipe-delimited) or [FixedLengthRecord] for fixed-width column files. FileHelperEngine<T> and FileHelperRegistry<T> require T to be annotated so the source generator can emit a type-safe descriptor at compile time.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh001");

    /// <summary>
    /// MLFH002: A field in a [FixedLengthRecord] type lacks [FieldFixedLength] or declares
    /// a non-positive width. A fixed-length record with unmeasured / zero-width fields has
    /// an undefined or silently-incorrect layout.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH002MissingFixedLength = new(
        id: "MLFH002",
        title: "Missing or non-positive [FieldFixedLength]",
        messageFormat: "Field '{0}' in fixed-length record '{1}' must be annotated with [FieldFixedLength(N)] where N > 0. How to fix: Add [FieldFixedLength(N)] attribute with the expected character width.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Add [FieldFixedLength(N)] to the field, where N is the number of characters this field occupies in the file. For example, a 10-character name field: [FieldFixedLength(10)]. Every visible field in a [FixedLengthRecord] type must declare a positive width so the generator can compute column offsets. Hidden members ([FieldHidden] / [FieldNotInFile]) are exempt since they do not participate in the file layout; [FieldValueDiscarded] fields are read and discarded, so they still need their width.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh002",
        customTags: NotConfigurable);

    /// <summary>
    /// MLFH003: Two or more fields in a record share the same [FieldOrder] value.
    /// Descriptor emission is suppressed — the type has an ambiguous layout.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH003AmbiguousFieldOrder = new(
        id: "MLFH003",
        title: "Ambiguous field order",
        messageFormat: "Fields '{0}' in type '{1}' share the same [FieldOrder] value {2}. Field order must be unique. How to fix: Assign distinct [FieldOrder(N)] values to each field.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Assign a unique integer to each [FieldOrder] in the record. For example, rename the second [FieldOrder(1)] to [FieldOrder(2)]. Field order values determine parse and format column positions — duplicates create an ambiguous layout with no correct resolution.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh003",
        customTags: NotConfigurable);

    /// <summary>
    /// MLFH004: <c>[FieldConverter(typeof(X))]</c> references a type that is neither a
    /// <c>ConverterBase</c> subclass nor an <c>ISpanConverter&lt;T&gt;</c> implementor for the field type.
    /// The converter cannot be used, so no descriptor is generated (3.x throws a <c>BadUsageException</c>
    /// when the engine is created).
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH004UnrecognisedConverterType = new(
        id: "MLFH004",
        title: "Unrecognised [FieldConverter(typeof(X))] type",
        messageFormat: "Field '{0}' in type '{1}' uses [FieldConverter(typeof({2}))] but '{2}' is neither a ConverterBase subclass nor an ISpanConverter<T> for the field type. How to fix: Use a type that inherits from ConverterBase or implements ISpanConverter<T>, or use a built-in ConverterKind value.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Custom field converters must derive from ModernLibs.FileHelpers.Converters.ConverterBase or implement ModernLibs.FileHelpers.Converters.ISpanConverter<T> for the field type. Any other type cannot be wired up, so no descriptor is generated for the record (FileHelpers 3.x rejects such a converter with a BadUsageException at runtime). Derive your converter from ConverterBase, implement ISpanConverter<T>, or use one of the built-in ConverterKind enum values.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh004",
        customTags: NotConfigurable);

    /// <summary>
    /// MLFH005: <c>[ConditionalRecord(RecordCondition.IncludeIfRegex | ExcludeIfRegex, "pattern")]</c>
    /// uses an invalid regular expression pattern (validated at compile time).
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH005InvalidRegexPattern = new(
        id: "MLFH005",
        title: "Invalid ConditionalRecord regex pattern",
        messageFormat: "ConditionalRecord regex pattern '{0}' is invalid: {1}. How to fix: Provide a valid .NET regular expression pattern.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The regex pattern provided to ConditionalRecordAttribute is not a valid regular expression. Fix the pattern syntax or use a non-regex condition type (IncludeIfBegins, IncludeIfContains, IncludeIfEnds, ExcludeIfBegins, ExcludeIfContains, ExcludeIfEnds).",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh005",
        customTags: NotConfigurable);

    /// <summary>
    /// MLFH006: <c>[FieldConverter(typeof(X))]</c> references a type that IS a recognized
    /// <c>ConverterBase</c> subclass or <c>ISpanConverter&lt;T&gt;</c> implementor but lacks a
    /// public parameterless constructor. The generated code uses <c>new ConverterType()</c>,
    /// which requires a public parameterless constructor.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH006MissingParameterlessCtor = new(
        id: "MLFH006",
        title: "FieldConverter target type lacks parameterless constructor",
        messageFormat: "Field '{0}' in type '{1}' uses [FieldConverter(typeof({2}))] but '{2}' does not have a public parameterless constructor. How to fix: Add a public parameterless constructor to '{2}', or use a built-in ConverterKind value.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Without converter arguments the generated code creates converter instances via `new ConverterType()`, which requires a public parameterless constructor. Both ConverterBase subtypes and ISpanConverter implementors need this. Add a public parameterless constructor to the converter type, pass constructor arguments in the attribute (see MLFH022), or use a built-in ConverterKind enum value instead.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh006",
        customTags: NotConfigurable);

    /// <summary>
    /// MLFH007: <c>[FieldFixedLength(N)]</c> exceeds the configured maximum width threshold.
    /// Default threshold is 1000 characters.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH007ExcessiveFieldWidth = new(
        id: "MLFH007",
        title: "FieldFixedLength exceeds configured maximum width",
        messageFormat: "Field '{0}' in type '{1}' has [FieldFixedLength({2})] which exceeds the configured maximum of {3} characters. How to fix: Reduce the field width, or raise the threshold with the 'FileHelpersMaxFieldWidth' MSBuild property (or 'filehelpers.max_field_width' in a .globalconfig).",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Excessively wide fixed-length fields may indicate a configuration error (e.g., confusing character count with byte count). The default threshold is 1000 characters. Reduce the field width or raise the threshold: set <FileHelpersMaxFieldWidth>N</FileHelpersMaxFieldWidth> in the project file (made visible to the generator by the package's buildTransitive props), or 'filehelpers.max_field_width = N' in a .globalconfig file.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh007");

    /// <summary>
    /// MLFH008: <c>[FieldOrder]</c> values have gaps in the sequence (e.g., 1, 2, 5, 6).
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH008FieldOrderGaps = new(
        id: "MLFH008",
        title: "FieldOrder has gaps in sequence",
        messageFormat: "Type '{0}' has a gap in [FieldOrder] values between {1} and {2}. How to fix: Renumber [FieldOrder] attributes to use consecutive values (e.g., 1, 2, 3, 4) or accept the gap if intentional.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Gaps in FieldOrder values (e.g., 1, 2, 5, 6) may indicate deleted fields or a configuration oversight. This is a warning, not an error — the code will work correctly. Renumber [FieldOrder] attributes to use consecutive values.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh008");

    /// <summary>
    /// MLFH009: Record type has no public fields or properties to map.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH009NoPublicFields = new(
        id: "MLFH009",
        title: "Record type has no public fields or properties",
        messageFormat: "Type '{0}' ({1}) has no public fields or properties to map. How to fix: Add at least one public field or property with the appropriate FileHelpers attributes ([FieldOrder], [FieldFixedLength], etc.), or remove the record attribute if this type is not intended for file parsing.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A record type with no public fields/properties will produce an empty descriptor that cannot parse or format any data. This may indicate a missing attribute or a type that was annotated by mistake. Add at least one public field or property with the appropriate FileHelpers attributes.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh009");

    /// <summary>
    /// MLFH010: <c>[FieldNullValue(x)]</c> uses a constant whose type cannot be converted to the field type.
    /// The value cannot be used, so no descriptor is generated.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH010IncompatibleNullValue = new(
        id: "MLFH010",
        title: "FieldNullValue type is incompatible with the field type",
        messageFormat: "Field '{0}' in type '{1}' has [FieldNullValue] of type '{2}', which cannot be converted to the field type '{3}'. How to fix: Use a constant of the field's type (a double constant is accepted for decimal fields), or remove the attribute.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The value of [FieldNullValue] is assigned to the field when the field is empty. It must be implicitly convertible to the field type (FileHelpers 3.x rejects incompatible null values with a BadUsageException at runtime; this generator reports it at compile time).",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh010",
        customTags: NotConfigurable);

    /// <summary>MLFH011: a type carries both [DelimitedRecord] and [FixedLengthRecord].</summary>
    internal static readonly DiagnosticDescriptor MLFH011ConflictingRecordAttributes = new(
        id: "MLFH011",
        title: "Conflicting record attributes",
        messageFormat: "Type '{0}' has both [DelimitedRecord] and [FixedLengthRecord]. How to fix: Keep only the attribute that matches the file layout.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A record type describes exactly one layout. FileHelpers 3.x rejects a type with both attributes; no descriptor is generated for it.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh011",
        customTags: NotConfigurable);

    /// <summary>MLFH012: an empty record or field delimiter.</summary>
    internal static readonly DiagnosticDescriptor MLFH012EmptyDelimiter = new(
        id: "MLFH012",
        title: "Empty delimiter",
        messageFormat: "{0} in type '{1}' declares an empty delimiter. How to fix: Use a non-empty delimiter such as \",\" or \"|\".",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "[DelimitedRecord] and [FieldDelimiter] need at least one character to split fields. No descriptor is generated for the type.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh012",
        customTags: NotConfigurable);

    /// <summary>MLFH013: a field type has no built-in conversion and no converter.</summary>
    internal static readonly DiagnosticDescriptor MLFH013UnsupportedFieldType = new(
        id: "MLFH013",
        title: "Unsupported field type",
        messageFormat: "Field '{0}' in type '{1}' has type '{2}', which has no built-in conversion. How to fix: Add [FieldConverter(typeof(MyConverter))] with a ConverterBase or ISpanConverter<T> implementation, change the field type, or mark the member [FieldHidden].",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Built-in conversions exist for string, bool, char, integer and floating-point types, decimal, DateTime, DateOnly, TimeOnly, DateTimeOffset, TimeSpan, Guid, enums and their nullable forms. Any other type needs a custom converter (FileHelpers 3.x raises a BadUsageException at runtime; this generator reports it at compile time).",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh013",
        customTags: NotConfigurable);

    /// <summary>MLFH014: the record cannot be constructed by the generated code.</summary>
    internal static readonly DiagnosticDescriptor MLFH014NoConstructionStrategy = new(
        id: "MLFH014",
        title: "Record type cannot be constructed",
        messageFormat: "Type '{0}' cannot be constructed by the generated descriptor: {1}. How to fix: Add an accessible parameterless constructor, or a constructor whose parameters match mapped members by name.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The descriptor creates records with an object initializer (parameterless constructor) or through a public/internal constructor whose parameters all bind to mapped members by name (case-insensitive), to [FieldHidden] members, or have default values. Abstract types cannot be records.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh014",
        customTags: NotConfigurable);

    /// <summary>MLFH015: an annotated member cannot be assigned.</summary>
    internal static readonly DiagnosticDescriptor MLFH015UnassignableMember = new(
        id: "MLFH015",
        title: "Annotated member cannot be assigned",
        messageFormat: "Member '{0}' in type '{1}' has FileHelpers field attributes but cannot be assigned (get-only, inaccessible setter or readonly) and is not bound to a constructor parameter. How to fix: Add an accessible setter, bind it through a constructor parameter with the same name, or mark it [FieldHidden].",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Members that cannot be assigned are skipped by the descriptor. Unannotated members (for example computed properties) are skipped silently; annotated members are reported because the declared layout cannot be honoured.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh015",
        customTags: NotConfigurable);

    /// <summary>MLFH016: generic record types are not supported.</summary>
    internal static readonly DiagnosticDescriptor MLFH016GenericRecord = new(
        id: "MLFH016",
        title: "Generic record types are not supported",
        messageFormat: "Type '{0}' is generic or nested in a generic type; FileHelpers records must be non-generic. How to fix: Use a non-generic record type (wrap or derive from a closed type).",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The source generator emits one descriptor per closed record type at compile time and cannot describe an open generic type.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh016",
        customTags: NotConfigurable);

    /// <summary>MLFH017: record type not accessible to generated code.</summary>
    internal static readonly DiagnosticDescriptor MLFH017InaccessibleRecord = new(
        id: "MLFH017",
        title: "Record type is not accessible",
        messageFormat: "Type '{0}' (or a containing type) is private, protected or file-local, so the generated descriptor cannot reference it. How to fix: Make the type and its containing types public or internal.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Descriptors are generated in separate files of the same assembly; the record type and every containing type must be at least internal.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh017",
        customTags: NotConfigurable);

    /// <summary>MLFH018: a required field follows an optional one.</summary>
    internal static readonly DiagnosticDescriptor MLFH018OptionalFieldOrder = new(
        id: "MLFH018",
        title: "Required field after an optional field",
        messageFormat: "Field '{0}' in type '{1}' must be marked [FieldOptional] because the previous field '{2}' is optional. How to fix: Mark all fields after the first [FieldOptional] field as optional.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only trailing fields can be missing from a record. FileHelpers 3.x rejects this layout with a BadUsageException; this generator reports it at compile time.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh018",
        customTags: NotConfigurable);

    /// <summary>MLFH019: [FieldConverter(ConverterKind.X)] does not match the field type.</summary>
    internal static readonly DiagnosticDescriptor MLFH019ConverterKindMismatch = new(
        id: "MLFH019",
        title: "ConverterKind does not match the field type",
        messageFormat: "Field '{0}' in type '{1}' uses [FieldConverter(ConverterKind.{2})] but its type is '{3}'. How to fix: Use the ConverterKind that matches the field type, or change the field type.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each ConverterKind converts one CLR type (Date and DateMultiFormat: DateTime; Enum: any enum; numeric kinds: their numeric type). FileHelpers 3.x rejects a mismatch at runtime; this generator reports it at compile time.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh019",
        customTags: NotConfigurable);

    /// <summary>MLFH020: unexpected generator failure for one record type (never drops other output).</summary>
    internal static readonly DiagnosticDescriptor MLFH020GeneratorFailure = new(
        id: "MLFH020",
        title: "Descriptor generation failed",
        messageFormat: "The FileHelpers generator could not generate a descriptor for '{0}': {1}. Please report this issue.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An unexpected error occurred while generating the descriptor for one record type. Other record types are not affected.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh020");

    /// <summary>MLFH021: converter arguments that can never work (e.g. DateMultiFormat without formats).</summary>
    internal static readonly DiagnosticDescriptor MLFH021InvalidConverterArguments = new(
        id: "MLFH021",
        title: "Invalid converter arguments",
        messageFormat: "Field '{0}' in type '{1}' uses [FieldConverter(ConverterKind.{2})] with invalid arguments: {3}",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Some converter kinds need arguments (DateMultiFormat needs the date formats to try). FileHelpers 3.x rejects such converters with a BadUsageException at runtime; this generator reports it at compile time.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh021",
        customTags: NotConfigurable);

    /// <summary>MLFH022: [FieldConverter(typeof(X), args...)] where X has no public constructor for the arguments.</summary>
    internal static readonly DiagnosticDescriptor MLFH022ConverterConstructorMismatch = new(
        id: "MLFH022",
        title: "No converter constructor for the converter arguments",
        messageFormat: "Field '{0}' in type '{1}' passes {2} argument(s) to [FieldConverter(typeof({3}), ...)] but '{3}' has no public constructor taking {2} string parameter(s) or a params string[] parameter. How to fix: Add such a constructor, or remove the arguments.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated descriptor creates custom converters with `new X(\"arg1\", ...)`, passing the [FieldConverter] arguments as strings (no reflection, AOT-safe). FileHelpers 3.x also passes them to a matching constructor and fails at run time when there is none.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh022",
        customTags: NotConfigurable);

    /// <summary>
    /// MLFH023: a private or protected member, or a property without an accessible getter, carries
    /// FileHelpers field attributes.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH023InaccessibleMember = new(
        id: "MLFH023",
        title: "Annotated member is not accessible",
        messageFormat: "Member '{0}' in type '{1}' has FileHelpers field attributes but is {2}, so the generated descriptor cannot read or assign it. How to fix: Make the member (and its get accessor) public or internal, or remove its FileHelpers attributes.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The descriptor is generated in another file of the same assembly, so it can access public, internal and protected internal members only, and formats a property through its get accessor. FileHelpers 3.x also maps private fields through reflection; here a private, protected or private protected member, a write-only property and a property with a private or protected getter are not part of the layout. Annotated members are reported because the declared layout cannot be honoured; unannotated ones that 3.x maps are reported as MLFH028 warnings.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh023",
        customTags: NotConfigurable);

    /// <summary>MLFH024: some mapped members have [FieldOrder] and others do not.</summary>
    internal static readonly DiagnosticDescriptor MLFH024MixedFieldOrder = new(
        id: "MLFH024",
        title: "[FieldOrder] must be used on all fields or none",
        messageFormat: "Field '{0}' in type '{1}' must be marked with [FieldOrder] because other fields of the type use it. How to fix: Add [FieldOrder(N)] to every mapped field and property, or remove it from all of them.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When one field of a record has [FieldOrder], every field that is part of the layout must have it, so that the column order is never a mix of explicit numbers and declaration order. FileHelpers 3.x rejects this layout with a BadUsageException at run time; this generator reports it at compile time. [FieldHidden] members are not part of the layout and are exempt.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh024",
        customTags: NotConfigurable);

    /// <summary>MLFH025: [FieldQuoted] on a field of a [FixedLengthRecord].</summary>
    internal static readonly DiagnosticDescriptor MLFH025QuotedFixedLengthField = new(
        id: "MLFH025",
        title: "[FieldQuoted] is only valid in delimited records",
        messageFormat: "Field '{0}' in fixed-length record '{1}' is marked [FieldQuoted], which is only valid in [DelimitedRecord] types. How to fix: Remove [FieldQuoted] (use [FieldTrim] to strip padding), or make the record delimited.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Fixed-length fields are located by position, so quoting has no meaning. FileHelpers 3.x rejects [FieldQuoted] on a fixed-length field with a BadUsageException at run time; this generator reports it at compile time.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh025",
        customTags: NotConfigurable);

    /// <summary>MLFH026: the project's C# language version is older than C# 11.</summary>
    internal static readonly DiagnosticDescriptor MLFH026LanguageVersionTooLow = new(
        id: "MLFH026",
        title: "C# 11 or later is required",
        messageFormat: "Type '{0}' is a FileHelpers record, but the project uses C# {1}; the generated descriptor needs C# 11 or later, so it is not generated. How to fix: Set <LangVersion>11</LangVersion> or later (C# 11 is the default for net7.0 and later target frameworks).",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated descriptors use C# 11 features (file-local types, scoped locals). With an older language version they would not compile, so the generator skips them and this error is reported for every record type instead.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh026",
        customTags: NotConfigurable);

    /// <summary>MLFH027: a ref struct cannot be a record type.</summary>
    internal static readonly DiagnosticDescriptor MLFH027RefStructRecord = new(
        id: "MLFH027",
        title: "ref struct records are not supported",
        messageFormat: "Type '{0}' is a ref struct, which cannot be a FileHelpers record: engines and the descriptor registry use the record type as a generic type argument. How to fix: Declare the record as a class, struct or record type.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "FileHelperEngine<T>, FileHelperRegistry<T> and IRecordDescriptor<T> take the record type as a type argument, which a ref struct cannot be. No descriptor is generated for a ref struct.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh027",
        customTags: NotConfigurable);

    /// <summary>
    /// MLFH028: an unannotated member that FileHelpers 3.x maps (private / protected field, auto-property
    /// backing field) is not part of the layout.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH028UnmappedLegacyMember = new(
        id: "MLFH028",
        title: "Member mapped by FileHelpers 3.x is not part of the layout",
        messageFormat: "Member '{0}' in type '{1}' is {2}, so it is not part of the record layout, but FileHelpers 3.x maps it as a column. How to fix: Make the member public or internal (with an accessible get accessor) to map it, or mark it [FieldHidden] if it is not a column.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "FileHelpers 3.x maps every instance field of a record through reflection, private and protected ones included, and the compiler-generated backing field of every auto-property. The generated descriptor can only read and assign public, internal and protected internal members with an accessible get accessor, so a migrated record would silently lose these columns. Make the member accessible to keep the column, or mark it [FieldHidden] to state that it is not part of the file.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh028");

    /// <summary>MLFH029: a [FieldInNewLine] first field in a record with [IgnoreEmptyLines].</summary>
    internal static readonly DiagnosticDescriptor MLFH029FirstFieldInNewLineIgnoredAsEmpty = new(
        id: "MLFH029",
        title: "[FieldInNewLine] first field with [IgnoreEmptyLines]",
        messageFormat: "Field '{0}' in type '{1}' is the first field and is marked [FieldInNewLine], so every record starts with a blank line, which [IgnoreEmptyLines] skips: the engine cannot read the records it writes. How to fix: Remove [FieldInNewLine] from the first field, or remove [IgnoreEmptyLines].",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A record whose first field is [FieldInNewLine] is written as a blank line followed by the fields, and is read the same way. [IgnoreEmptyLines] removes that blank line before the record is read, so the record is read from the wrong lines. FileHelpers 3.x behaves the same but does not report it.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/diagnostics.md#mlfh029");

    /// <summary>Descriptors reported from <c>DiagnosticInfo</c> models, by id.</summary>
    internal static readonly IReadOnlyDictionary<string, DiagnosticDescriptor> ById = new Dictionary<string, DiagnosticDescriptor>(StringComparer.Ordinal)
    {
        [MLFH008FieldOrderGaps.Id] = MLFH008FieldOrderGaps,
        [MLFH011ConflictingRecordAttributes.Id] = MLFH011ConflictingRecordAttributes,
        [MLFH012EmptyDelimiter.Id] = MLFH012EmptyDelimiter,
        [MLFH013UnsupportedFieldType.Id] = MLFH013UnsupportedFieldType,
        [MLFH014NoConstructionStrategy.Id] = MLFH014NoConstructionStrategy,
        [MLFH015UnassignableMember.Id] = MLFH015UnassignableMember,
        [MLFH016GenericRecord.Id] = MLFH016GenericRecord,
        [MLFH017InaccessibleRecord.Id] = MLFH017InaccessibleRecord,
        [MLFH018OptionalFieldOrder.Id] = MLFH018OptionalFieldOrder,
        [MLFH019ConverterKindMismatch.Id] = MLFH019ConverterKindMismatch,
        [MLFH021InvalidConverterArguments.Id] = MLFH021InvalidConverterArguments,
        [MLFH022ConverterConstructorMismatch.Id] = MLFH022ConverterConstructorMismatch,
        [MLFH023InaccessibleMember.Id] = MLFH023InaccessibleMember,
        [MLFH024MixedFieldOrder.Id] = MLFH024MixedFieldOrder,
        [MLFH025QuotedFixedLengthField.Id] = MLFH025QuotedFixedLengthField,
        [MLFH027RefStructRecord.Id] = MLFH027RefStructRecord,
        [MLFH028UnmappedLegacyMember.Id] = MLFH028UnmappedLegacyMember,
    };
}
