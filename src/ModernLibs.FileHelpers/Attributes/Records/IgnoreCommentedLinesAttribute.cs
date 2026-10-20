namespace ModernLibs.FileHelpers;

/// <summary>
/// Skips lines that start with the specified comment marker character (optionally after whitespace, see
/// <see cref="AnyPlace"/>), as in FileHelpers 3.x.
/// The marker is a single <see langword="char"/> (3.x uses a string).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class IgnoreCommentedLinesAttribute : Attribute
{
    /// <summary>The character that marks a line as a comment.</summary>
    public char CommentMarker { get; }

    /// <summary>
    /// When <see langword="true"/> (default), the marker may be preceded by spaces or tabs.
    /// When <see langword="false"/>, only lines whose first character is the marker are skipped.
    /// A marker in the middle of a line never makes it a comment.
    /// </summary>
    public bool AnyPlace { get; }

    /// <summary>Skips lines that start with the marker, optionally after whitespace.</summary>
    /// <param name="commentMarker">The single character that marks a line as a comment (e.g., <c>'#'</c>, <c>';'</c>).</param>
    public IgnoreCommentedLinesAttribute(char commentMarker) : this(commentMarker, true) { }

    /// <summary>Skips lines that are marked as comments by the specified character.</summary>
    /// <param name="commentMarker">The single character that marks a line as a comment.</param>
    /// <param name="anyPlace">
    /// <see langword="true"/> to allow whitespace before the marker;
    /// <see langword="false"/> to skip only lines that begin with the marker.
    /// </param>
    public IgnoreCommentedLinesAttribute(char commentMarker, bool anyPlace)
    {
        CommentMarker = commentMarker;
        AnyPlace = anyPlace;
    }
}
