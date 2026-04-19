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
        messageFormat: "Type '{0}' is used with FileHelperRegistry but lacks [DelimitedRecord] or [FixedLengthRecord]",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Add [DelimitedRecord(\",\")] for delimiter-separated files (CSV, TSV, pipe-delimited) or [FixedLengthRecord] for fixed-width column files. FileHelperRegistry<T> requires T to be annotated so the source generator can emit a type-safe descriptor at compile time.");

    /// <summary>
    /// MLFH002: A field in a [FixedLengthRecord] type lacks [FieldFixedLength] or declares
    /// a non-positive width. A fixed-length record with unmeasured / zero-width fields has
    /// an undefined or silently-incorrect layout.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH002MissingFixedLength = new(
        id: "MLFH002",
        title: "Missing or non-positive [FieldFixedLength]",
        messageFormat: "Field '{0}' in fixed-length record '{1}' must be annotated with [FieldFixedLength(N)] where N > 0",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Add [FieldFixedLength(N)] to the field, where N is the number of characters this field occupies in the file. For example, a 10-character name field: [FieldFixedLength(10)]. Every visible field in a [FixedLengthRecord] type must declare a positive width so the generator can compute column offsets. Hidden or discarded fields ([FieldHidden] / [FieldNotInFile] / [FieldValueDiscarded]) are exempt since they do not participate in the file layout.");

    /// <summary>
    /// MLFH003: Two or more fields in a record share the same [FieldOrder] value.
    /// Descriptor emission is suppressed — the type has an ambiguous layout.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH003AmbiguousFieldOrder = new(
        id: "MLFH003",
        title: "Ambiguous field order",
        messageFormat: "Fields '{0}' in type '{1}' share the same [FieldOrder] value {2}. Field order must be unique.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Assign a unique integer to each [FieldOrder] in the record. For example, rename the second [FieldOrder(1)] to [FieldOrder(2)]. Field order values determine parse and format column positions — duplicates create an ambiguous layout with no correct resolution.");

    /// <summary>
    /// MLFH004: <c>[FieldConverter(typeof(X))]</c> references a type that is neither a
    /// <c>ConverterBase</c> subclass nor (future) an <c>ISpanConverter&lt;T&gt;</c> implementor.
    /// The field will silently fall through to <c>default(T)</c> at runtime, so emit a warning
    /// at compile time.
    /// </summary>
    internal static readonly DiagnosticDescriptor MLFH004UnrecognisedConverterType = new(
        id: "MLFH004",
        title: "Unrecognised [FieldConverter(typeof(X))] type",
        messageFormat: "Field '{0}' in type '{1}' uses [FieldConverter(typeof({2}))] but '{2}' is not a ConverterBase subclass. The field will fall through to default(T) at runtime.",
        category: "FileHelpers.Generator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Custom field converters must derive from ModernLibs.FileHelpers.Converters.ConverterBase. If the supplied type does not derive from ConverterBase (and is not one of the future ISpanConverter<T> implementors), the generator cannot wire up conversion and the field silently becomes default(T). Either derive your converter from ConverterBase or use one of the built-in ConverterKind enum values.");
}
