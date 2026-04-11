namespace ModernLibs.FileHelpers;

/// <summary>
/// Skips lines that begin with (or contain, depending on <see cref="AnyPlace"/>) the specified comment marker character.
/// The marker is a single <see langword="char"/>, not a string, to avoid multi-character ambiguity.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class IgnoreCommentedLinesAttribute : Attribute
{
    /// <summary>The character that marks a line as a comment.</summary>
    public char CommentMarker { get; }

    /// <summary>
    /// When <see langword="true"/>, any line containing the marker character anywhere is skipped.
    /// When <see langword="false"/>, only lines that begin with the marker are skipped.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool AnyPlace { get; }

    /// <summary>Skips lines that contain the specified comment marker character (anywhere in the line).</summary>
    /// <param name="commentMarker">The single character that marks a line as a comment (e.g., <c>'#'</c>, <c>';'</c>).</param>
    public IgnoreCommentedLinesAttribute(char commentMarker) : this(commentMarker, true) { }

    /// <summary>Skips lines that are marked as comments by the specified character.</summary>
    /// <param name="commentMarker">The single character that marks a line as a comment.</param>
    /// <param name="anyPlace">
    /// <see langword="true"/> to skip any line containing the marker;
    /// <see langword="false"/> to skip only lines that begin with the marker.
    /// </param>
    public IgnoreCommentedLinesAttribute(char commentMarker, bool anyPlace)
    {
        CommentMarker = commentMarker;
        AnyPlace = anyPlace;
    }
}
