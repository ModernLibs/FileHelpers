using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ModernLibs.FileHelpers.Generator.Models;

/// <summary>
/// Source location of a record, member or attribute, carried through the extraction so diagnostics get
/// real squiggles and honour <c>#pragma warning disable</c>.
/// </summary>
/// <remarks>
/// Validation diagnostics are reported by <see cref="FileHelpersAnalyzer"/>, which extracts a fresh
/// model from the current compilation and keeps the <see cref="SyntaxTree"/> (<c>bindToTree</c>), so its
/// locations are in-source locations of the analysed compilation. The generator's cached models never
/// keep a tree: a cached diagnostic bound to an older tree makes the driver throw "SyntaxTree is not part
/// of the compilation" on the next incremental run. The generator only reports MLFH020, through
/// <see cref="ToExternalLocation"/> (file path and span, not bound to a tree). Equality uses the file
/// path and spans only, so models stay cache-friendly.
/// </remarks>
internal sealed class LocationInfo : IEquatable<LocationInfo>
{
    private readonly SyntaxTree? _tree;

    private LocationInfo(SyntaxTree? tree, string filePath, TextSpan span, LinePositionSpan lineSpan)
    {
        _tree = tree;
        FilePath = filePath;
        Span = span;
        LineSpan = lineSpan;
    }

    public string FilePath { get; }

    public TextSpan Span { get; }

    public LinePositionSpan LineSpan { get; }

    /// <summary>Captures <paramref name="location"/>; the tree is kept only when <paramref name="bindToTree"/> is set (analyzer).</summary>
    public static LocationInfo? From(Location? location, bool bindToTree)
    {
        if (location is null || !location.IsInSource || location.SourceTree is null)
            return null;
        return new LocationInfo(bindToTree ? location.SourceTree : null, location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }

    public static LocationInfo? From(SyntaxReference? reference, bool bindToTree, CancellationToken ct) =>
        reference is null ? null : From(reference.GetSyntax(ct).GetLocation(), bindToTree);

    /// <summary>In-source location when the tree was kept (analyzer models), otherwise an external location.</summary>
    public Location ToLocation() => _tree is not null ? Location.Create(_tree, Span) : ToExternalLocation();

    /// <summary>File path and span, not bound to a syntax tree (safe to report from cached generator steps).</summary>
    public Location ToExternalLocation() => Location.Create(FilePath, Span, LineSpan);

    public bool Equals(LocationInfo? other) =>
        other is not null
        && string.Equals(FilePath, other.FilePath, StringComparison.Ordinal)
        && Span.Equals(other.Span)
        && LineSpan.Equals(other.LineSpan);

    public override bool Equals(object? obj) => obj is LocationInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(FilePath) * 31 + Span.GetHashCode()) * 31 + LineSpan.GetHashCode();
        }
    }
}
