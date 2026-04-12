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

        context.RegisterSourceOutput(validDelimited, static (spc, model) => EmitDelimited(spc, model));

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

        context.RegisterSourceOutput(validFixed, static (spc, model) => EmitFixedLength(spc, model));

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

    /// <summary>
    /// RegisterSourceOutput handler for the Delimited arm.
    /// Checks MLFH003 (duplicate [FieldOrder]) before emitting the descriptor.
    /// </summary>
    private static void EmitDelimited(SourceProductionContext spc, RecordModel model)
    {
        // MLFH003: consecutive-pairs duplicate detection on the already-sorted Fields array.
        if (ReportDuplicateOrders(spc, model)) return; // suppress descriptor — layout is ambiguous

        DescriptorEmitter.Emit(spc, model);
    }

    /// <summary>
    /// RegisterSourceOutput handler for the FixedLength arm.
    /// Checks MLFH002 (missing [FieldFixedLength]) and MLFH003 (duplicate [FieldOrder]) before emitting.
    /// </summary>
    private static void EmitFixedLength(SourceProductionContext spc, RecordModel model)
    {
        // MLFH002: emit diagnostic for each field missing [FieldFixedLength].
        bool hasMissingFixedLength = false;
        foreach (var field in model.Fields)
        {
            if (field.FixedLength < 0)
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

        DescriptorEmitter.Emit(spc, model);
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
