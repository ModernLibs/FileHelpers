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
    /// CREC-03: Include matching lines.
    /// </summary>
    IncludeIfContains = 1,

    /// <summary>
    /// Include the record if the line begins with the specified string.
    /// CREC-02: Include lines starting with pattern.
    /// </summary>
    IncludeIfBegins = 2,

    /// <summary>
    /// Include the record if the line ends with the specified string.
    /// CREC-04: Include lines ending with pattern.
    /// </summary>
    IncludeIfEnds = 3,

    /// <summary>
    /// Include the record if the line matches the specified regular expression.
    /// CREC-05: Include lines matching regex pattern.
    /// </summary>
    IncludeIfRegex = 5,

    /// <summary>
    /// Exclude the record if the line contains the specified string.
    /// CREC-07: Exclude matching lines.
    /// </summary>
    ExcludeIfContains = 6,

    /// <summary>
    /// Exclude the record if the line begins with the specified string.
    /// CREC-06: Exclude lines starting with pattern.
    /// </summary>
    ExcludeIfBegins = 7,

    /// <summary>
    /// Exclude the record if the line ends with the specified string.
    /// CREC-08: Exclude lines ending with pattern.
    /// </summary>
    ExcludeIfEnds = 8,

    /// <summary>
    /// Exclude the record if the line matches the specified regular expression.
    /// CREC-09: Exclude lines matching regex pattern.
    /// </summary>
    ExcludeIfRegex = 10,
}
