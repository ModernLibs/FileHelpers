using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
/// <remarks>
/// Validation diagnostics are reported by <see cref="FileHelpersAnalyzer"/>. The generator runs the
/// same rules (<see cref="RecordValidator"/>) only to replace the descriptor of a record with errors by one
/// that throws (see <see cref="InvalidRecordEmitter"/>), and reports MLFH020 alone, with a location that is
/// not bound to a syntax tree (see <see cref="LocationInfo"/>).
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class FileHelpersGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Generated code needs C# 11 (file-local types, scoped locals); the analyzer reports MLFH026.
        IncrementalValueProvider<bool> languageSupported = context.ParseOptionsProvider
            .Select(static (options, _) => options is not CSharpParseOptions csharp || csharp.LanguageVersion >= LanguageVersion.CSharp11);

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

        context.RegisterSourceOutput(validDelimited.Combine(languageSupported), static (spc, input) =>
        {
            if (input.Right) EmitSafely(spc, input.Left);
        });

        // ---- [FixedLengthRecord] pipeline arm ----
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

        context.RegisterSourceOutput(validFixed.Combine(languageSupported), static (spc, input) =>
        {
            if (input.Right) EmitSafely(spc, input.Left);
        });
    }

    /// <summary>
    /// One record type must never take down the whole generator. Records with validation errors get no
    /// working descriptor (the analyzer reports the errors): a descriptor that throws a
    /// <c>BadUsageException</c> naming the errors is registered instead, so hiding the errors never leads to
    /// a silent failure (see <see cref="InvalidRecordEmitter"/>). Unexpected failures are reported as
    /// MLFH020 instead of escaping (which would drop ALL generated output).
    /// </summary>
    private static void EmitSafely(SourceProductionContext spc, RecordModel model)
    {
        if (model.Failure is not null)
        {
            ReportFailure(spc, model, model.Failure);
            return;
        }

        try
        {
            // The MLFH007 threshold only affects a warning, never emission.
            var errors = new List<string>();
            bool valid = RecordValidator.Validate(model, int.MaxValue, (descriptor, _, args) =>
            {
                if (descriptor.DefaultSeverity == DiagnosticSeverity.Error)
                    errors.Add(descriptor.Id + ": " + string.Format(CultureInfo.InvariantCulture, descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture), args));
            });
            if (!valid)
            {
                // Records the generated code cannot reference, and records whose only problem is an
                // unresolved type (a compiler error already), get nothing.
                if (model.IsReferenceable && errors.Count > 0)
                    InvalidRecordEmitter.Emit(spc, model, errors);
                return;
            }
            DescriptorEmitter.Emit(spc, model);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportFailure(spc, model, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void ReportFailure(SourceProductionContext spc, RecordModel model, string reason) =>
        spc.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MLFH020GeneratorFailure,
            model.Location?.ToExternalLocation() ?? Location.None,
            model.TypeName,
            reason));
}
