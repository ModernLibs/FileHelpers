using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        // Discover [DelimitedRecord] types using ForAttributeWithMetadataName.
        // This filters at Roslyn's internal attribute index — skips ~99% of all nodes.
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

        context.RegisterSourceOutput(validDelimited, static (spc, model) =>
            DescriptorEmitter.Emit(spc, model));
    }
}
