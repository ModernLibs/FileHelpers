namespace ModernLibs.FileHelpers;

/// <summary>
/// The condition used to include or exclude records during parsing.
/// Used by <see cref="ConditionalRecordAttribute"/> to specify the matching strategy.
/// </summary>
public enum RecordCondition
{
    /// <summary>No condition; all records are processed. Default value.</summary>
    None = 0,

    /// <summary>
    /// Include the record if the line contains the specified string.
    /// Include matching lines.
    /// </summary>
    IncludeIfContains = 1,

    /// <summary>
    /// Include the record if the line begins with the specified string.
    /// Include lines starting with pattern.
    /// </summary>
    IncludeIfBegins = 2,

    /// <summary>
    /// Include the record if the line ends with the specified string.
    /// Include lines ending with pattern.
    /// </summary>
    IncludeIfEnds = 3,

    /// <summary>
    /// Include the record if the line matches the specified regular expression.
    /// Include lines matching regex pattern.
    /// </summary>
    IncludeIfRegex = 5,

    /// <summary>
    /// Exclude the record if the line contains the specified string.
    /// Exclude matching lines.
    /// </summary>
    ExcludeIfContains = 6,

    /// <summary>
    /// Exclude the record if the line begins with the specified string.
    /// Exclude lines starting with pattern.
    /// </summary>
    ExcludeIfBegins = 7,

    /// <summary>
    /// Exclude the record if the line ends with the specified string.
    /// Exclude lines ending with pattern.
    /// </summary>
    ExcludeIfEnds = 8,

    /// <summary>
    /// Exclude the record if the line matches the specified regular expression.
    /// Exclude lines matching regex pattern.
    /// </summary>
    ExcludeIfRegex = 10,
}
