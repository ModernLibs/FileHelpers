using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using ModernLibs.FileHelpers.Generator.Models;

namespace ModernLibs.FileHelpers.Generator.Diagnostics;

/// <summary>
/// The record validation rules, shared by <see cref="FileHelpersAnalyzer"/> (which reports them) and
/// <see cref="FileHelpersGenerator"/> (which only needs to know whether the descriptor may be emitted).
/// </summary>
internal static class RecordValidator
{
    /// <summary>
    /// Runs every rule over <paramref name="model"/> and passes the diagnostics to <paramref name="report"/>
    /// (when given). Returns true when the descriptor can be emitted.
    /// </summary>
    internal static bool Validate(RecordModel model, int maxFieldWidth, ReportValidation? report)
    {
        report ??= static (_, _, _) => { };
        if (model.Failure is not null) return false; // the generator reports MLFH020

        // Extraction diagnostics; any error suppresses the descriptor.
        bool hasError = false;
        foreach (var info in model.Diagnostics)
        {
            var descriptor = DiagnosticDescriptors.ById[info.Id];
            hasError |= descriptor.DefaultSeverity == DiagnosticSeverity.Error;
            report(descriptor, info.Location, ToArgs(info.Args));
        }
        if (hasError) return false;

        bool valid = string.Equals(model.RecordKind, "FixedLength", StringComparison.Ordinal)
            ? ValidateFixedLength(model, maxFieldWidth, report)
            : ValidateDelimited(model, maxFieldWidth, report);

        // An unresolved field or converter type already fails the build (CS0246); the descriptor is not
        // emitted, and no FileHelpers diagnostic is added.
        return valid && !model.HasUnresolvedTypes;
    }

    /// <summary>Delimited: MLFH003, MLFH004, MLFH005, MLFH006, MLFH007, MLFH009, MLFH010.</summary>
    private static bool ValidateDelimited(RecordModel model, int maxFieldWidth, ReportValidation report)
    {
        ReportNoPublicFields(model, report);                          // MLFH009 (non-suppressing)
        if (ReportDuplicateOrders(model, report)) return false;       // MLFH003: ambiguous layout
        if (ReportInvalidConditionalRecordRegex(model, report)) return false; // MLFH005
        bool unusableAttribute = ReportAttributeErrors(model, report); // MLFH004, MLFH006, MLFH010
        ReportExcessiveFieldWidth(model, maxFieldWidth, report);      // MLFH007 (non-suppressing)
        ReportFirstFieldInNewLineIgnoredAsEmpty(model, report);       // MLFH029 (non-suppressing)
        return !unusableAttribute;
    }

    /// <summary>
    /// MLFH004, MLFH006, MLFH010: a [FieldConverter] or [FieldNullValue] that cannot be applied. The record would
    /// be read without it, so these errors block the descriptor like the other errors.
    /// </summary>
    private static bool ReportAttributeErrors(RecordModel model, ReportValidation report)
    {
        bool unrecognised = ReportUnrecognisedConverters(model, report);
        bool noConstructor = ReportMissingParameterlessConstructor(model, report);
        bool nullValue = ReportIncompatibleNullValues(model, report);
        return unrecognised || noConstructor || nullValue;
    }

    /// <summary>Fixed-length: MLFH002 plus the delimited checks.</summary>
    private static bool ValidateFixedLength(RecordModel model, int maxFieldWidth, ReportValidation report)
    {
        ReportNoPublicFields(model, report);

        // MLFH002: every field in the layout (discarded ones included — they occupy their width) needs
        // a positive [FieldFixedLength]. [FieldHidden] members are not part of the layout.
        bool hasMissingFixedLength = false;
        foreach (var field in model.Fields)
        {
            if (field.FixedLength > 0) continue;
            hasMissingFixedLength = true;
            report(DiagnosticDescriptors.MLFH002MissingFixedLength, FieldLocation(model, field), field.Name, model.TypeName);
        }

        bool hasDuplicateOrder = ReportDuplicateOrders(model, report);
        if (hasMissingFixedLength || hasDuplicateOrder) return false;

        if (ReportInvalidConditionalRecordRegex(model, report)) return false;
        bool unusableAttribute = ReportAttributeErrors(model, report);
        ReportExcessiveFieldWidth(model, maxFieldWidth, report);
        ReportFirstFieldInNewLineIgnoredAsEmpty(model, report);
        return !unusableAttribute;
    }

    /// <summary>
    /// MLFH029: with a [FieldInNewLine] first field every record starts with a blank line, which
    /// [IgnoreEmptyLines] skips, so written files cannot be read back (same as 3.x). Non-suppressing.
    /// </summary>
    private static void ReportFirstFieldInNewLineIgnoredAsEmpty(RecordModel model, ReportValidation report)
    {
        if (!model.IgnoreEmptyLines || model.Fields.Length == 0 || !model.Fields[0].IsInNewLine) return;
        report(DiagnosticDescriptors.MLFH029FirstFieldInNewLineIgnoredAsEmpty, FieldLocation(model, model.Fields[0]),
            model.Fields[0].Name, model.TypeName);
    }

    private static object[] ToArgs(EquatableArray<string> args)
    {
        var result = new object[args.Length];
        for (int i = 0; i < args.Length; i++)
            result[i] = args[i];
        return result;
    }

    private static LocationInfo? FieldLocation(RecordModel model, FieldModel field) => field.Location ?? model.Location;

    /// <summary>MLFH004: [FieldConverter(typeof(X))] that is neither a ConverterBase nor an ISpanConverter for the field.</summary>
    private static bool ReportUnrecognisedConverters(RecordModel model, ReportValidation report)
    {
        bool any = false;
        foreach (var field in model.Fields)
        {
            if (field.UnrecognisedConverterType is null) continue;
            any = true;
            report(DiagnosticDescriptors.MLFH004UnrecognisedConverterType, FieldLocation(model, field),
                field.Name, model.TypeName, field.UnrecognisedConverterType);
        }
        return any;
    }

    /// <summary>MLFH003: consecutive fields of the sorted array sharing an Order (reported at the later field).</summary>
    private static bool ReportDuplicateOrders(RecordModel model, ReportValidation report)
    {
        bool hasDuplicateOrder = false;
        for (int i = 1; i < model.Fields.Length; i++)
        {
            if (model.Fields[i].Order != model.Fields[i - 1].Order) continue;
            hasDuplicateOrder = true;
            report(DiagnosticDescriptors.MLFH003AmbiguousFieldOrder, FieldLocation(model, model.Fields[i]),
                $"{model.Fields[i - 1].Name}, {model.Fields[i].Name}", model.TypeName, model.Fields[i].Order);
        }

        return hasDuplicateOrder;
    }

    /// <summary>MLFH005: validates IncludeIfRegex (5) / ExcludeIfRegex (10) patterns at compile time.</summary>
#pragma warning disable MA0009 // User-provided regex pattern validation - we test-compile to catch errors early
    private static bool ReportInvalidConditionalRecordRegex(RecordModel model, ReportValidation report)
    {
        if (model.Condition is not 5 and not 10)
            return false;

        string pattern = model.ConditionPattern;
        if (string.IsNullOrEmpty(pattern))
            return false; // Empty pattern will be handled at runtime (matches nothing)

        try
        {
            _ = new Regex(pattern);
            return false;
        }
        catch (ArgumentException ex)
        {
            report(DiagnosticDescriptors.MLFH005InvalidRegexPattern, model.Location, pattern, ex.Message);
            return true;
        }
    }
#pragma warning restore MA0009

    /// <summary>MLFH006: converter type without a public parameterless constructor (and no converter arguments).</summary>
    private static bool ReportMissingParameterlessConstructor(RecordModel model, ReportValidation report)
    {
        bool any = false;
        foreach (var field in model.Fields)
        {
            if (field.MissingParameterlessCtorTypeName is null) continue;
            any = true;
            report(DiagnosticDescriptors.MLFH006MissingParameterlessCtor, FieldLocation(model, field),
                field.Name, model.TypeName, field.MissingParameterlessCtorTypeName);
        }
        return any;
    }

    /// <summary>MLFH010: [FieldNullValue] constant not convertible to the field type.</summary>
    private static bool ReportIncompatibleNullValues(RecordModel model, ReportValidation report)
    {
        bool any = false;
        foreach (var field in model.Fields)
        {
            if (field.IncompatibleNullValueType is null) continue;
            any = true;
            report(DiagnosticDescriptors.MLFH010IncompatibleNullValue, FieldLocation(model, field),
                field.Name, model.TypeName, field.IncompatibleNullValueType, field.TypeName);
        }
        return any;
    }

    /// <summary>MLFH007: [FieldFixedLength] above the configured threshold. Non-suppressing.</summary>
    private static void ReportExcessiveFieldWidth(RecordModel model, int threshold, ReportValidation report)
    {
        foreach (var field in model.Fields)
        {
            if (field.FixedLength <= threshold) continue;
            report(DiagnosticDescriptors.MLFH007ExcessiveFieldWidth, FieldLocation(model, field),
                field.Name, model.TypeName, field.FixedLength, threshold);
        }
    }

    /// <summary>MLFH009: record without mappable fields or properties. Non-suppressing.</summary>
    private static void ReportNoPublicFields(RecordModel model, ReportValidation report)
    {
        if (model.Fields.Length != 0) return;
        report(DiagnosticDescriptors.MLFH009NoPublicFields, model.Location, model.TypeName, model.TypeKind);
    }

    /// <summary>
    /// MLFH007 threshold. Documented sources, in priority order: the <c>FileHelpersMaxFieldWidth</c> MSBuild
    /// property, the <c>filehelpers.max_field_width</c> key of a <c>.globalconfig</c>, and the historical
    /// <c>filehelpers_max_field_width</c> build property.
    /// </summary>
    internal static int ReadMaxFieldWidth(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions globalOptions)
    {
        foreach (string key in new[] { MaxFieldWidthBuildProperty, MaxFieldWidthConfigKey, "build_property.filehelpers_max_field_width" })
        {
            if (globalOptions.TryGetValue(key, out string? raw)
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int threshold)
                && threshold > 0)
            {
                return threshold;
            }
        }
        return DefaultMaxFieldWidth;
    }

    /// <summary>MSBuild property (made compiler-visible by the package's buildTransitive props) for MLFH007.</summary>
    internal const string MaxFieldWidthBuildProperty = "build_property.FileHelpersMaxFieldWidth";

    /// <summary>Global analyzer config key (<c>.globalconfig</c>) for MLFH007.</summary>
    internal const string MaxFieldWidthConfigKey = "filehelpers.max_field_width";

    private const int DefaultMaxFieldWidth = 1000;
}
