namespace ModernLibs.FileHelpers;

/// <summary>
/// Marks a record type with a filter condition that determines which lines
/// are included or excluded during parsing. Filtering runs before parse attempt.
/// CREC-01: Attribute definition for record-level filtering.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ConditionalRecordAttribute : Attribute
{
    /// <summary>
    /// The condition type determining how to filter lines.
    /// CREC-01: Condition type for record filtering.
    /// </summary>
    public RecordCondition Condition { get; }

    /// <summary>
    /// The pattern to match (prefix, suffix, substring, or regex).
    /// CREC-01: Pattern string for condition matching.
    /// </summary>
    public string ConditionPattern { get; }

    /// <summary>
    /// Creates a conditional record filter.
    /// CREC-01: Constructor accepting condition type and pattern string.
    /// </summary>
    /// <param name="condition">The condition type (IncludeIfBegins, ExcludeIfContains, etc.)</param>
    /// <param name="conditionPattern">The pattern to match against each line.</param>
    public ConditionalRecordAttribute(RecordCondition condition, string conditionPattern)
    {
        Condition = condition;
        ConditionPattern = conditionPattern ?? string.Empty;
    }
}
