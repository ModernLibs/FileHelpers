namespace ModernLibs.FileHelpers.Generator.Models;

/// <summary>
/// Equatable description of a diagnostic found during extraction. The descriptor is resolved by
/// <see cref="Id"/> when the diagnostic is reported (see <c>DiagnosticDescriptors.ById</c>).
/// </summary>
internal sealed record DiagnosticInfo(string Id, LocationInfo? Location, EquatableArray<string> Args)
{
    public static DiagnosticInfo Create(string id, LocationInfo? location, params string[] args) =>
        new(id, location, new EquatableArray<string>(System.Collections.Immutable.ImmutableArray.Create(args)));
}
