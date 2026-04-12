namespace ModernLibs.FileHelpers.Generator;

/// <summary>
/// Constants for naming incremental pipeline steps. Used with WithTrackingName() to enable
/// the caching test (SGEN-10) to inspect step run reasons via TrackedOutputSteps.
/// </summary>
internal static class TrackingNames
{
    internal const string DelimitedModels = nameof(DelimitedModels);
    internal const string DelimitedFiltered = nameof(DelimitedFiltered);
}
