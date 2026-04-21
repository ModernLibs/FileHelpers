namespace ModernLibs.FileHelpers;

/// <summary>
/// Compile-time record options surfaced on <see cref="IRecordDescriptor{T}"/> so the engine can
/// honor record-level filtering attributes (<see cref="IgnoreFirstAttribute"/>,
/// <see cref="IgnoreLastAttribute"/>, <see cref="IgnoreEmptyLinesAttribute"/>,
/// <see cref="IgnoreCommentedLinesAttribute"/>, <see cref="ConditionalRecordAttribute"/>)
/// without reflection at runtime.
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

    /// <summary>
    /// Record-level filter condition (from <see cref="ConditionalRecordAttribute"/>).
    /// Default: <see cref="RecordCondition.None"/> (no filtering).
    /// CREC-01: Condition type for record filtering.
    /// </summary>
    public RecordCondition Condition { get; init; }

    /// <summary>
    /// Pattern string for record filtering (from <see cref="ConditionalRecordAttribute"/>).
    /// Default: empty string.
    /// CREC-01: Pattern for condition matching.
    /// </summary>
    public string ConditionPattern { get; init; }

    /// <summary>
    /// Number of physical lines per logical record (from <see cref="FieldInNewLineAttribute"/> count).
    /// Default: 1 (single-line record). Phase 14 MLIN-02.
    /// LineCountPerLogicalRecord = 1 + count of fields where IsInNewLine = true.
    /// </summary>
    public int LineCountPerLogicalRecord { get; init; }

    /// <summary>
    /// Multiline mode for quoted fields (from <see cref="DelimitedRecordAttribute.MultilineMode"/>).
    /// Default: <see cref="MultilineMode.NotAllow"/>.
    /// Phase 18 MULT-01: Enables RFC 4180 multiline quoted field support.
    /// </summary>
    public MultilineMode MultilineMode { get; init; }

    /// <inheritdoc/>
    public bool Equals(RecordOptions other) =>
        IgnoreFirst == other.IgnoreFirst
        && IgnoreLast == other.IgnoreLast
        && IgnoreEmptyLines == other.IgnoreEmptyLines
        && CommentMarker == other.CommentMarker
        && string.Equals(Delimiter, other.Delimiter, StringComparison.Ordinal)
        && Condition == other.Condition
        && string.Equals(ConditionPattern, other.ConditionPattern, StringComparison.Ordinal)
        && LineCountPerLogicalRecord == other.LineCountPerLogicalRecord
        && MultilineMode == other.MultilineMode;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RecordOptions other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // HashCode.Combine only supports up to 8 arguments - combine in two steps
        int hash = HashCode.Combine(IgnoreFirst, IgnoreLast, IgnoreEmptyLines, CommentMarker, Delimiter, Condition, ConditionPattern, LineCountPerLogicalRecord);
        return HashCode.Combine(hash, MultilineMode);
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(RecordOptions left, RecordOptions right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(RecordOptions left, RecordOptions right) => !left.Equals(right);
}
