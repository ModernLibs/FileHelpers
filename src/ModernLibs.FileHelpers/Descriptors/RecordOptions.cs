using System.ComponentModel;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Compile-time record options surfaced on <see cref="IRecordDescriptor{T}"/> so the engine can
/// honor record-level filtering attributes (<see cref="IgnoreFirstAttribute"/>,
/// <see cref="IgnoreLastAttribute"/>, <see cref="IgnoreEmptyLinesAttribute"/>,
/// <see cref="IgnoreCommentedLinesAttribute"/>, <see cref="ConditionalRecordAttribute"/>)
/// without reflection at runtime.
/// Infrastructure for source-generated code; not intended for direct use.
/// </summary>
/// <remarks>
/// Values are captured by the source generator at compile time and emitted into the descriptor via an
/// object initializer; generated code in consumer assemblies binds to the <c>init</c> accessors, so they
/// stay stable in 4.x. Defaults produce no filtering.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
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
    /// Whether the <see cref="CommentMarker"/> may be preceded by whitespace (from
    /// <see cref="IgnoreCommentedLinesAttribute.AnyPlace"/>, default <see langword="true"/> in the attribute).
    /// <see langword="false"/> (default of this struct) means the marker must be the first character.
    /// </summary>
    public bool CommentAnyPlace { get; init; }

    /// <summary>
    /// Record delimiter string (captured for engine awareness; mirrors
    /// <see cref="DelimitedRecordAttribute"/>). Empty string for fixed-length records.
    /// </summary>
    public string Delimiter { get; init; }

    /// <summary>
    /// Record-level filter condition (from <see cref="ConditionalRecordAttribute"/>).
    /// Default: <see cref="RecordCondition.None"/> (no filtering).
    /// </summary>
    public RecordCondition Condition { get; init; }

    /// <summary>
    /// Pattern string for record filtering (from <see cref="ConditionalRecordAttribute"/>).
    /// Default: empty string.
    /// </summary>
    public string ConditionPattern { get; init; }

    /// <summary>
    /// Number of physical lines per logical record (from <see cref="FieldInNewLineAttribute"/> count).
    /// Default: 1 (single-line record).
    /// LineCountPerLogicalRecord = 1 + count of fields where IsInNewLine = true.
    /// </summary>
    public int LineCountPerLogicalRecord { get; init; }

    /// <summary>
    /// Record-level multiline mode: <see cref="DelimitedRecordAttribute.MultilineMode"/> when set, otherwise
    /// combined from the quoted fields' <see cref="FieldDescriptor.QuoteMultiline"/> (reading/writing allowed
    /// when any quoted field allows it). The engine assembles multiline records only when it allows reading.
    /// </summary>
    public MultilineMode MultilineMode { get; init; }

    /// <summary>
    /// Whether lines containing only whitespace count as empty for <see cref="IgnoreEmptyLines"/>
    /// (from <see cref="IgnoreEmptyLinesAttribute.IgnoreSpaces"/>). Default: false.
    /// </summary>
    public bool IgnoreEmptySpaces { get; init; }

    /// <summary>
    /// Quote character of the record's first quoted field (from <see cref="FieldQuotedAttribute.QuoteChar"/>);
    /// <c>'\0'</c> (default) when no field is quoted. The engine uses the per-field
    /// <see cref="FieldDescriptor.QuoteChar"/>; this value is only used for descriptors whose fields declare
    /// no quote char, in which case every field is quoted with it.
    /// </summary>
    public char QuoteChar { get; init; }

    /// <inheritdoc/>
    public bool Equals(RecordOptions other) =>
        IgnoreFirst == other.IgnoreFirst
        && IgnoreLast == other.IgnoreLast
        && IgnoreEmptyLines == other.IgnoreEmptyLines
        && CommentMarker == other.CommentMarker
        && CommentAnyPlace == other.CommentAnyPlace
        && string.Equals(Delimiter, other.Delimiter, StringComparison.Ordinal)
        && Condition == other.Condition
        && string.Equals(ConditionPattern, other.ConditionPattern, StringComparison.Ordinal)
        && LineCountPerLogicalRecord == other.LineCountPerLogicalRecord
        && MultilineMode == other.MultilineMode
        && IgnoreEmptySpaces == other.IgnoreEmptySpaces
        && QuoteChar == other.QuoteChar;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is RecordOptions other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = HashCode.Combine(IgnoreFirst, IgnoreLast, IgnoreEmptyLines, CommentMarker, Delimiter, Condition, ConditionPattern, LineCountPerLogicalRecord);
        return HashCode.Combine(hash, MultilineMode, IgnoreEmptySpaces, QuoteChar, CommentAnyPlace);
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(RecordOptions left, RecordOptions right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(RecordOptions left, RecordOptions right) => !left.Equals(right);
}
