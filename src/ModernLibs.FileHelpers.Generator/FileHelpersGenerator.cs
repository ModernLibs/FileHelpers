using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModernLibs.FileHelpers.Generator.Diagnostics;
using ModernLibs.FileHelpers.Generator.Emission;
using ModernLibs.FileHelpers.Generator.Extraction;
using ModernLibs.FileHelpers.Generator.Models;

namespace ModernLibs.FileHelpers.Generator;

/// <summary>
/// IIncrementalGenerator that discovers types annotated with [DelimitedRecord] or [FixedLengthRecord]
/// and emits source-generated IRecordDescriptor&lt;T&gt; implementations.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class FileHelpersGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // ---- .editorconfig threshold pipeline (MLFH007) ----
        IncrementalValueProvider<int> configProvider = CreateConfigProvider(context);

        // ---- [DelimitedRecord] pipeline arm ----
        // ForAttributeWithMetadataName filters at Roslyn's internal attribute index (~99% skipped).
        // All lambdas MUST be static to prevent closure capture (breaks caching).
        IncrementalValuesProvider<RecordModel?> rawDelimited = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: "ModernLibs.FileHelpers.DelimitedRecordAttribute",
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, ct) => RecordModelExtractor.ExtractDelimited(ctx, ct))
            .WithTrackingName(TrackingNames.DelimitedModels);

        IncrementalValuesProvider<RecordModel> validDelimited = rawDelimited
            .Where(static m => m is not null)
            .Select(static (m, _) => m!)
            .WithTrackingName(TrackingNames.DelimitedFiltered);

        var delimitedWithConfig = validDelimited.Combine(configProvider);
        context.RegisterSourceOutput(delimitedWithConfig, static (spc, tuple) => EmitDelimited(spc, tuple.Left, tuple.Right));

        // ---- [FixedLengthRecord] pipeline arm (SGEN-06, SGEN-08) ----
        IncrementalValuesProvider<RecordModel?> rawFixed = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: "ModernLibs.FileHelpers.FixedLengthRecordAttribute",
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, ct) => RecordModelExtractor.ExtractFixedLength(ctx, ct))
            .WithTrackingName(TrackingNames.FixedLengthModels);

        IncrementalValuesProvider<RecordModel> validFixed = rawFixed
            .Where(static m => m is not null)
            .Select(static (m, _) => m!)
            .WithTrackingName(TrackingNames.FixedLengthFiltered);

        var fixedWithConfig = validFixed.Combine(configProvider);
        context.RegisterSourceOutput(fixedWithConfig, static (spc, tuple) => EmitFixedLength(spc, tuple.Left, tuple.Right));

        // ---- MLFH001 engine-usage detection arm (SGEN-07) ----
        // Detects FileHelperRegistry<T> or FileHelperEngine<T> where T lacks a record attribute.
        // Uses CreateSyntaxProvider because the trigger is a generic type reference, not an attribute.
        IncrementalValuesProvider<string?> rawMissingAttr = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) =>
                node is GenericNameSyntax gns &&
                gns.Identifier.Text is "FileHelperRegistry" or "FileHelperEngine",
            transform: static (ctx, ct) => FileHelpersGenerator.ExtractMissingAttributeUsage(ctx, ct));

        IncrementalValuesProvider<string> missingAttr = rawMissingAttr
            .Where(static s => s is not null)
            .Select(static (s, _) => s!);

        context.RegisterSourceOutput(missingAttr, static (spc, typeName) =>
            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MLFH001MissingRecordAttribute,
                Location.None,
                typeName)));
    }

    private static IncrementalValueProvider<int> CreateConfigProvider(IncrementalGeneratorInitializationContext context)
    {
        return context.AnalyzerConfigOptionsProvider
            .Select(static (options, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                if (options.GlobalOptions.TryGetValue("build_property.filehelpers_max_field_width", out string? raw)
                    && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int threshold)
                    && threshold > 0)
                {
                    return threshold;
                }
                return 1000; // default threshold
            })
            .WithTrackingName("FileHelpers.ConfigThreshold");
    }

    /// <summary>
    /// RegisterSourceOutput handler for the Delimited arm.
    /// Checks MLFH003 (duplicate [FieldOrder]), MLFH004 (unrecognised [FieldConverter(typeof)]
    /// type), MLFH005 (invalid ConditionalRecord regex), MLFH006 (missing parameterless ctor),
    /// MLFH007 (excessive field width), MLFH008 (field order gaps), and MLFH009 (no public fields)
    /// before emitting the descriptor.
    /// </summary>
    private static void EmitDelimited(SourceProductionContext spc, RecordModel model, int maxFieldWidth)
    {
        // MLFH009: warn if record has no public fields (non-suppressing)
        ReportNoPublicFields(spc, model);

        // MLFH003: consecutive-pairs duplicate detection on the already-sorted Fields array.
        if (ReportDuplicateOrders(spc, model)) return; // suppress descriptor — layout is ambiguous

        // MLFH004 (IN-02): warn on unrecognised FieldConverter types. Non-suppressing — the
        // field falls through to default(T) at runtime, but we surface the mis-annotation
        // at compile time so users don't silently get defaulted values.
        ReportUnrecognisedConverters(spc, model);

        // MLFH005 (CREC-11): validate ConditionalRecord regex patterns at compile time
        if (ReportInvalidConditionalRecordRegex(spc, model)) return; // suppress descriptor for invalid regex

        // MLFH006: warn on recognized converter types lacking parameterless constructor (non-suppressing)
        ReportMissingParameterlessConstructor(spc, model);

        // MLFH007: warn on excessive field width (non-suppressing)
        ReportExcessiveFieldWidth(spc, model, maxFieldWidth);

        // MLFH008: warn on gaps in FieldOrder sequence (non-suppressing)
        ReportFieldOrderGaps(spc, model);

        DescriptorEmitter.Emit(spc, model);
    }

    /// <summary>
    /// RegisterSourceOutput handler for the FixedLength arm.
    /// Checks MLFH002 (missing [FieldFixedLength]), MLFH003 (duplicate [FieldOrder]),
    /// MLFH004 (unrecognised [FieldConverter(typeof)] type), MLFH005 (invalid ConditionalRecord
    /// regex), MLFH006 (missing parameterless ctor), MLFH007 (excessive field width),
    /// MLFH008 (field order gaps), and MLFH009 (no public fields) before emitting.
    /// </summary>
    private static void EmitFixedLength(SourceProductionContext spc, RecordModel model, int maxFieldWidth)
    {
        // MLFH009: warn if record has no public fields (non-suppressing)
        ReportNoPublicFields(spc, model);

        // MLFH002: emit diagnostic for each field missing / non-positive [FieldFixedLength].
        // WR-05: widened the check from `< 0` (attribute missing) to `<= 0` (missing OR
        // explicitly zero-width). A zero-width field silently reads empty slices and writes
        // a 1-char space — both asymmetric with the declared schema and invisible to users.
        // Hidden / NotInFile fields are skipped because they don't participate in the file layout.
        bool hasMissingFixedLength = false;
        foreach (var field in model.Fields)
        {
            if (field.IsHidden || field.IsValueDiscarded) continue;
            if (field.FixedLength <= 0)
            {
                hasMissingFixedLength = true;
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MLFH002MissingFixedLength,
                    Location.None,
                    field.Name,
                    model.TypeName));
            }
        }

        // MLFH003: detect duplicate [FieldOrder] values (applies to fixed-length records too).
        bool hasDuplicateOrder = ReportDuplicateOrders(spc, model);

        if (hasMissingFixedLength || hasDuplicateOrder) return;

        // MLFH004 (IN-02): warn on unrecognised FieldConverter types. Non-suppressing.
        ReportUnrecognisedConverters(spc, model);

        // MLFH005 (CREC-11): validate ConditionalRecord regex patterns at compile time
        if (ReportInvalidConditionalRecordRegex(spc, model)) return; // suppress descriptor for invalid regex

        // MLFH006: warn on recognized converter types lacking parameterless constructor (non-suppressing)
        ReportMissingParameterlessConstructor(spc, model);

        // MLFH007: warn on excessive field width (non-suppressing)
        ReportExcessiveFieldWidth(spc, model, maxFieldWidth);

        // MLFH008: warn on gaps in FieldOrder sequence (non-suppressing)
        ReportFieldOrderGaps(spc, model);

        DescriptorEmitter.Emit(spc, model);
    }

    /// <summary>
    /// IN-02: Reports MLFH004 for every field that has an unrecognised <c>[FieldConverter(typeof(X))]</c>
    /// type — one that is neither a <c>ConverterBase</c> subclass nor a future
    /// <c>ISpanConverter&lt;T&gt;</c> implementor. Non-suppressing: descriptor emission continues and
    /// the field falls through to <c>default(T)</c>, but the mis-annotation is now visible at compile time.
    /// </summary>
    private static void ReportUnrecognisedConverters(SourceProductionContext spc, RecordModel model)
    {
        foreach (var field in model.Fields)
        {
            if (field.UnrecognisedConverterType is null) continue;
            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MLFH004UnrecognisedConverterType,
                Location.None,
                field.Name,
                model.TypeName,
                field.UnrecognisedConverterType));
        }
    }

    /// <summary>
    /// Walks the already-sorted Fields array looking for consecutive pairs with the same Order.
    /// Emits MLFH003 for each duplicate pair found. Returns true if any duplicates were found.
    /// </summary>
    private static bool ReportDuplicateOrders(SourceProductionContext spc, RecordModel model)
    {
        bool hasDuplicateOrder = false;
        for (int i = 1; i < model.Fields.Length; i++)
        {
            if (model.Fields[i].Order == model.Fields[i - 1].Order)
            {
                hasDuplicateOrder = true;
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MLFH003AmbiguousFieldOrder,
                    Location.None,
                    $"{model.Fields[i - 1].Name}, {model.Fields[i].Name}",
                    model.TypeName,
                    model.Fields[i].Order));
            }
        }

        return hasDuplicateOrder;
    }

    /// <summary>
    /// CREC-11: Validates ConditionalRecord regex patterns at compile time.
    /// If the condition is IncludeIfRegex (5) or ExcludeIfRegex (10), attempts to compile
    /// the pattern. If compilation fails, emits MLFH005 and returns true to suppress
    /// descriptor emission (the pattern is invalid and would crash at runtime).
    /// </summary>
#pragma warning disable MA0009 // User-provided regex pattern validation - we test-compile to catch errors early
    private static bool ReportInvalidConditionalRecordRegex(SourceProductionContext spc, RecordModel model)
    {
        // RecordCondition.IncludeIfRegex = 5, ExcludeIfRegex = 10
        if (model.Condition is not 5 and not 10)
            return false; // Not a regex condition — no validation needed

        string pattern = model.ConditionPattern;
        if (string.IsNullOrEmpty(pattern))
            return false; // Empty pattern will be handled at runtime (matches nothing)

        try
        {
            // Attempt to compile the regex to validate the pattern
            _ = new Regex(pattern);
            return false; // Valid regex
        }
        catch (ArgumentException ex)
        {
            // Invalid regex pattern — emit MLFH005
            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MLFH005InvalidRegexPattern,
                Location.None,
                pattern,
                ex.Message));
            return true; // Suppress descriptor emission
        }
    }
#pragma warning restore MA0009

    /// <summary>
    /// Phase 57 DX-01: Reports MLFH006 for every field where a recognized converter type
    /// (ConverterBase subtype or ISpanConverter implementor) lacks a public parameterless constructor.
    /// Non-suppressing: descriptor emission continues.
    /// </summary>
    private static void ReportMissingParameterlessConstructor(SourceProductionContext spc, RecordModel model)
    {
        foreach (var field in model.Fields)
        {
            if (field.MissingParameterlessCtorTypeName is null) continue;
            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MLFH006MissingParameterlessCtor,
                Location.None,
                field.Name,
                model.TypeName,
                field.MissingParameterlessCtorTypeName));
        }
    }

    /// <summary>
    /// Phase 57 DX-01: Reports MLFH007 for fields with [FieldFixedLength] exceeding the
    /// configured threshold. Non-suppressing.
    /// </summary>
    private static void ReportExcessiveFieldWidth(SourceProductionContext spc, RecordModel model, int threshold)
    {
        foreach (var field in model.Fields)
        {
            if (field.FixedLength <= 0) continue; // skip unset sentinel and non-fixed fields
            if (field.FixedLength > threshold)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MLFH007ExcessiveFieldWidth,
                    Location.None,
                    field.Name,
                    model.TypeName,
                    field.FixedLength,
                    threshold));
            }
        }
    }

    /// <summary>
    /// Phase 57 DX-01: Reports MLFH008 when [FieldOrder] values have gaps in the sequence.
    /// Only fires when at least one field has an explicit [FieldOrder] annotation.
    /// Non-suppressing.
    /// </summary>
    private static void ReportFieldOrderGaps(SourceProductionContext spc, RecordModel model)
    {
        if (model.Fields.Length < 2) return;

        // Check if any field has an explicit FieldOrder (differs from declaration index)
        bool hasExplicitOrder = false;
        foreach (var field in model.Fields)
        {
            if (field.Order != field.DeclarationIndex)
            {
                hasExplicitOrder = true;
                break;
            }
        }
        if (!hasExplicitOrder) return; // implicit 0,1,2,3 ordering has no gaps

        // Sweep through sorted fields looking for gaps
        for (int i = 1; i < model.Fields.Length; i++)
        {
            if (model.Fields[i].Order - model.Fields[i - 1].Order > 1)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MLFH008FieldOrderGaps,
                    Location.None,
                    model.TypeName,
                    model.Fields[i - 1].Order,
                    model.Fields[i].Order));
            }
        }
    }

    /// <summary>
    /// Phase 57 DX-01: Reports MLFH009 when a record type has no public fields or properties.
    /// Non-suppressing.
    /// </summary>
    private static void ReportNoPublicFields(SourceProductionContext spc, RecordModel model)
    {
        if (model.Fields.Length == 0)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.MLFH009NoPublicFields,
                Location.None,
                model.TypeName,
                model.TypeKind));
        }
    }

    /// <summary>
    /// Checks whether the single type argument of a FileHelperRegistry&lt;T&gt; or
    /// FileHelperEngine&lt;T&gt; generic name lacks [DelimitedRecord] or [FixedLengthRecord].
    /// Returns the type name if MLFH001 should fire; returns null otherwise.
    /// </summary>
    private static string? ExtractMissingAttributeUsage(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (ctx.Node is not GenericNameSyntax gns) return null;
        if (gns.TypeArgumentList.Arguments.Count != 1) return null;

        var typeArg = ctx.SemanticModel.GetTypeInfo(
            gns.TypeArgumentList.Arguments[0], ct).Type;
        if (typeArg is not INamedTypeSymbol typeSymbol) return null;

        // Return null (no diagnostic) if the type has a record attribute.
        foreach (var attr in typeSymbol.GetAttributes())
        {
            string? attrName = attr.AttributeClass?.ToDisplayString();
            if (attrName is "ModernLibs.FileHelpers.DelimitedRecordAttribute"
                         or "ModernLibs.FileHelpers.FixedLengthRecordAttribute")
                return null;
        }

        return typeSymbol.Name; // No record attribute found — report MLFH001
    }
}
