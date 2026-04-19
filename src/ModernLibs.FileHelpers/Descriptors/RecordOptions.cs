namespace ModernLibs.FileHelpers;

/// <summary>
/// Compile-time record options surfaced on <see cref="IRecordDescriptor{T}"/> so the engine can
/// honor record-level filtering attributes (<see cref="IgnoreFirstAttribute"/>,
/// <see cref="IgnoreLastAttribute"/>, <see cref="IgnoreEmptyLinesAttribute"/>,
/// <see cref="IgnoreCommentedLinesAttribute"/>) without reflection at runtime.
///
/// D-07: values are captured by the source generator at compile time and emitted into the
/// descriptor via an object initializer. Defaults produce no filtering.
/// </summary>
public readonly struct RecordOptions : IEquatable<RecordOptions>
{
    /// <summary>Number of leading lines to skip (from <see cref="IgnoreFirstAttribute"/>). Default: 0.</summary>
    public int IgnoreFirst { get; init; }

    /// <summary>Number of trailing lines to skip (from <see cref="IgnoreLastAttribute"/>). Default: 0.</summary>
    public int IgnoreLast { get; init; }

    /// <summary>Whether empty lines are skipped (from <see cref="IgnoreEmptyLinesAttribute"/>). Default: false.</summary>
    public bool IgnoreEmptyLines { get; init; }

    /// <summary>
    /// Comment marker character (from <see cref="IgnoreCommentedLinesAttribute"/>); lines starting
    /// with this character are skipped. <c>'\0'</c> (default) means "no comment marker".
    /// </summary>
    public char CommentMarker { get; init; }

    /// <summary>
    /// Record delimiter string (captured for engine awareness; mirrors
    /// <see cref="DelimitedRecordAttribute"/>). Empty string for fixed-length records.
    /// </summary>
    public string Delimiter { get; init; }

    /// <inheritdoc/>
    public bool Equals(RecordOptions other) =>
        IgnoreFirst == other.IgnoreFirst
        && IgnoreLast == other.IgnoreLast
        && IgnoreEmptyLines == other.IgnoreEmptyLines
        && CommentMarker == other.CommentMarker
        && string.Equals(Delimiter, other.Delimiter, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RecordOptions other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(IgnoreFirst, IgnoreLast, IgnoreEmptyLines, CommentMarker, Delimiter);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(RecordOptions left, RecordOptions right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(RecordOptions left, RecordOptions right) => !left.Equals(right);
}
