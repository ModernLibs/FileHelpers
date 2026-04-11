namespace ModernLibs.FileHelpers;

/// <summary>
/// The condition used to include or exclude records during parsing.
/// Used by <c>ConditionalRecordAttribute</c> to specify the matching strategy.
/// </summary>
public enum RecordCondition
{
    /// <summary>No condition; all records are processed.</summary>
    None = 0,

    /// <summary>Include the record if the line contains the specified string.</summary>
    IncludeIfContains = 1,

    /// <summary>Include the record if the line begins with the specified string.</summary>
    IncludeIfBegins = 2,

    /// <summary>Include the record if the line ends with the specified string.</summary>
    IncludeIfEnds = 3,

    /// <summary>Include the record if the line is enclosed by the specified strings.</summary>
    IncludeIfEnclosed = 4,

    /// <summary>Include the record if the line matches the specified regular expression.</summary>
    IncludeIfMatchRegex = 5,

    /// <summary>Exclude the record if the line contains the specified string.</summary>
    ExcludeIfContains = 6,

    /// <summary>Exclude the record if the line begins with the specified string.</summary>
    ExcludeIfBegins = 7,

    /// <summary>Exclude the record if the line ends with the specified string.</summary>
    ExcludeIfEnds = 8,

    /// <summary>Exclude the record if the line is enclosed by the specified strings.</summary>
    ExcludeIfEnclosed = 9,

    /// <summary>Exclude the record if the line matches the specified regular expression.</summary>
    ExcludeIfMatchRegex = 10,
}
