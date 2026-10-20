namespace ModernLibs.FileHelpers.Generator;

/// <summary>
/// Constants for naming incremental pipeline steps. Used with WithTrackingName() to enable
/// the caching test to inspect step run reasons via TrackedOutputSteps.
/// </summary>
internal static class TrackingNames
{
    internal const string DelimitedModels = nameof(DelimitedModels);
    internal const string DelimitedFiltered = nameof(DelimitedFiltered);
    internal const string FixedLengthModels = nameof(FixedLengthModels);
    internal const string FixedLengthFiltered = nameof(FixedLengthFiltered);
}
