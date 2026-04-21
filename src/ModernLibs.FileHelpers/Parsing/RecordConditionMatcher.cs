using System.Text.RegularExpressions;

namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// Evaluates record condition filters against input lines.
/// CREC-10: Filtering runs before parse attempt.
/// CREC-11: Regex patterns compiled once per engine instance.
/// </summary>
internal sealed class RecordConditionMatcher
{
    private readonly RecordCondition _condition;
    private readonly string _pattern;
    private readonly Regex? _regex;

    /// <summary>
    /// Creates a matcher for the specified condition and pattern.
    /// </summary>
    /// <param name="condition">The condition type to apply.</param>
    /// <param name="pattern">The pattern to match (prefix, suffix, substring, or regex).</param>
    public RecordConditionMatcher(RecordCondition condition, string pattern)
    {
        _condition = condition;
        _pattern = pattern ?? string.Empty;

        // CREC-11: Compile regex once if needed
        // MA0009: User-provided regex patterns are accepted; compile-time validation via MFH004
        // and documentation guidance address DoS concerns for patterns in ConditionalRecord.
#pragma warning disable MA0009
        if (condition is RecordCondition.IncludeIfRegex or RecordCondition.ExcludeIfRegex
            && !string.IsNullOrEmpty(pattern))
        {
            _regex = new Regex(pattern, RegexOptions.Compiled);
        }
#pragma warning restore MA0009
    }

    /// <summary>
    /// Determines whether the line should be included based on the condition.
    /// </summary>
    /// <param name="line">The line to evaluate.</param>
    /// <returns>True if the line should be included; false to skip it.</returns>
    public bool ShouldInclude(ReadOnlySpan<char> line)
    {
        return _condition switch
        {
            RecordCondition.None => true,
            RecordCondition.IncludeIfBegins => line.StartsWith(_pattern.AsSpan(), StringComparison.Ordinal),
            RecordCondition.IncludeIfContains => line.Contains(_pattern.AsSpan(), StringComparison.Ordinal),
            RecordCondition.IncludeIfEnds => line.EndsWith(_pattern.AsSpan(), StringComparison.Ordinal),
            RecordCondition.IncludeIfRegex => _regex?.IsMatch(line) ?? true,
            RecordCondition.ExcludeIfBegins => !line.StartsWith(_pattern.AsSpan(), StringComparison.Ordinal),
            RecordCondition.ExcludeIfContains => !line.Contains(_pattern.AsSpan(), StringComparison.Ordinal),
            RecordCondition.ExcludeIfEnds => !line.EndsWith(_pattern.AsSpan(), StringComparison.Ordinal),
            RecordCondition.ExcludeIfRegex => !(_regex?.IsMatch(line) ?? false),
            _ => true
        };
    }
}
