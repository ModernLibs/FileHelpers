namespace ModernLibs.FileHelpers;

/// <summary>
/// Marks a record type with a filter condition that determines which lines
/// are included or excluded during parsing. Filtering runs before the parse attempt, so excluded
/// lines never raise conversion errors.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ConditionalRecordAttribute : Attribute
{
    /// <summary>
    /// The condition type determining how to filter lines.
    /// </summary>
    public RecordCondition Condition { get; }

    /// <summary>
    /// The pattern to match (prefix, suffix, substring, or regex).
    /// </summary>
    public string ConditionPattern { get; }

    /// <summary>
    /// Creates a conditional record filter.
    /// </summary>
    /// <param name="condition">The condition type (IncludeIfBegins, ExcludeIfContains, etc.)</param>
    /// <param name="conditionPattern">The pattern to match against each line.</param>
    public ConditionalRecordAttribute(RecordCondition condition, string conditionPattern)
    {
        Condition = condition;
        ConditionPattern = conditionPattern ?? string.Empty;
    }
}
