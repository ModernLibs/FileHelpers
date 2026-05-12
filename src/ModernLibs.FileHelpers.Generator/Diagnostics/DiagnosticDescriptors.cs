using Microsoft.CodeAnalysis;

namespace ModernLibs.FileHelpers.Generator.Diagnostics;

/// <summary>
/// Compile-time diagnostic descriptors emitted by the FileHelpers source generator.
/// All diagnostics use category "FileHelpers.Generator" for IDE filtering consistency.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <summary>
    /// MLFH001: Type used with FileHelperRegistry or FileHelperEngine lacks [DelimitedRecord]
    /// or [FixedLengthRecord].
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH001MissingRecordAttribute = new(
        id: "MLFH001",
        title: "Missing record attribute",
        messageFormat: "Type '{0}' is used with FileHelperRegistry but lacks [DelimitedRecord] or [FixedLengthRecord]. How to fix: Add [DelimitedRecord(\",\")] or [FixedLengthRecord(FixedMode.FixedLength)] to the type declaration.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Add [DelimitedRecord(\",\")] for delimiter-separated files (CSV, TSV, pipe-delimited) or [FixedLengthRecord] for fixed-width column files. FileHelperRegistry<T> requires T to be annotated so the source generator can emit a type-safe descriptor at compile time.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/ATTRIBUTES.md");

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
        description: "Add [FieldFixedLength(N)] to the field, where N is the number of characters this field occupies in the file. For example, a 10-character name field: [FieldFixedLength(10)]. Every visible field in a [FixedLengthRecord] type must declare a positive width so the generator can compute column offsets. Hidden or discarded fields ([FieldHidden] / [FieldNotInFile] / [FieldValueDiscarded]) are exempt since they do not participate in the file layout.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/ATTRIBUTES.md");

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
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/ATTRIBUTES.md");

    /// <summary>
    /// MLFH004: <c>[FieldConverter(typeof(X))]</c> references a type that is neither a
    /// <c>ConverterBase</c> subclass nor (future) an <c>ISpanConverter&lt;T&gt;</c> implementor.
    /// The field will silently fall through to <c>default(T)</c> at runtime, so emit a warning
    /// at compile time.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH004UnrecognisedConverterType = new(
        id: "MLFH004",
        title: "Unrecognised [FieldConverter(typeof(X))] type",
        messageFormat: "Field '{0}' in type '{1}' uses [FieldConverter(typeof({2}))] but '{2}' is not a ConverterBase subclass. The field will fall through to default(T) at runtime. How to fix: Use a type that inherits from ConverterBase, or use the built-in converter types (Int32Converter, DateTimeConverter, etc.).",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Custom field converters must derive from ModernLibs.FileHelpers.Converters.ConverterBase. If the supplied type does not derive from ConverterBase (and is not one of the future ISpanConverter<T> implementors), the generator cannot wire up conversion and the field silently becomes default(T). Either derive your converter from ConverterBase or use one of the built-in ConverterKind enum values.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/CONVERTERS.md");

    /// <summary>
    /// MLFH005: <c>[ConditionalRecord(RecordCondition.IncludeIfRegex | ExcludeIfRegex, "pattern")]</c>
    /// uses an invalid regular expression pattern. CREC-11: Compile-time regex validation.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH005InvalidRegexPattern = new(
        id: "MLFH005",
        title: "Invalid ConditionalRecord regex pattern",
        messageFormat: "ConditionalRecord regex pattern '{0}' is invalid: {1}. How to fix: Provide a valid .NET regular expression pattern.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The regex pattern provided to ConditionalRecordAttribute is not a valid regular expression. Fix the pattern syntax or use a non-regex condition type (IncludeIfBegins, IncludeIfContains, IncludeIfEnds, ExcludeIfBegins, ExcludeIfContains, ExcludeIfEnds).",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/ATTRIBUTES.md");

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
        description: "The generated code creates converter instances via `new ConverterType()`, which requires a public parameterless constructor. Both ConverterBase subtypes and ISpanConverter implementors need this. Add a public parameterless constructor to the converter type, or use a built-in ConverterKind enum value instead.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/CONVERTERS.md");

    /// <summary>
    /// MLFH007: <c>[FieldFixedLength(N)]</c> exceeds the configured maximum width threshold.
    /// Default threshold is 1000 characters.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH007ExcessiveFieldWidth = new(
        id: "MLFH007",
        title: "FieldFixedLength exceeds configured maximum width",
        messageFormat: "Field '{0}' in type '{1}' has [FieldFixedLength({2})] which exceeds the configured maximum of {3} characters. How to fix: Reduce the field width, or increase the threshold via 'filehelpers.max_field_width' in .editorconfig.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Excessively wide fixed-length fields may indicate a configuration error (e.g., confusing character count with byte count). The default threshold is 1000 characters. Reduce the field width or increase the threshold via 'filehelpers.max_field_width' in .editorconfig.",
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/ATTRIBUTES.md");

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
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/ATTRIBUTES.md");

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
        helpLinkUri: "https://github.com/ModernLibs/FileHelpers/blob/main/docs/ATTRIBUTES.md");
}
