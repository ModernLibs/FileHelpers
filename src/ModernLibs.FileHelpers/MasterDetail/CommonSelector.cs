// MA0048: File contains two closely related types (enum + static factory class).
#pragma warning disable MA0048

namespace ModernLibs.FileHelpers;

/// <summary>
/// Predefined classification modes for master/detail selection.
/// Matches FileHelpers 3.x CommonSelector enum semantics.
/// </summary>
public enum CommonSelectorMode
{
    /// <summary>Record is master if the line contains the selector string.</summary>
    MasterIfContains = 0,

    /// <summary>Record is master if the line begins with the selector string.</summary>
    MasterIfBegins = 1,

    /// <summary>Record is master if the line ends with the selector string.</summary>
    MasterIfEnds = 2,

    /// <summary>Record is master if the line begins AND ends with the selector string.</summary>
    MasterIfEnclosed = 3,

    /// <summary>Record is detail if the line contains the selector string.</summary>
    DetailIfContains = 4,

    /// <summary>Record is detail if the line begins with the selector string.</summary>
    DetailIfBegins = 5,

    /// <summary>Record is detail if the line ends with the selector string.</summary>
    DetailIfEnds = 6,

    /// <summary>Record is detail if the line begins AND ends with the selector string.</summary>
    DetailIfEnclosed = 7,
}

/// <summary>
/// Provides common selector factory methods for master/detail classification.
/// Each method returns a <see cref="MasterDetailSelector"/> delegate.
/// </summary>
public static class CommonSelector
{
    /// <summary>Creates a selector: master if line begins with the specified prefix.</summary>
    public static MasterDetailSelector BeginsWith(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return line => line.StartsWith(prefix.AsSpan()) ? RecordAction.Master : RecordAction.Detail;
    }

    /// <summary>Creates a selector: master if line ends with the specified suffix.</summary>
    public static MasterDetailSelector EndsWith(string suffix)
    {
        ArgumentNullException.ThrowIfNull(suffix);
        return line => line.EndsWith(suffix.AsSpan()) ? RecordAction.Master : RecordAction.Detail;
    }

    /// <summary>Creates a selector: master if line contains the specified substring.</summary>
    public static MasterDetailSelector Contains(string substring)
    {
        ArgumentNullException.ThrowIfNull(substring);
        return line => line.IndexOf(substring.AsSpan(), StringComparison.Ordinal) >= 0 ? RecordAction.Master : RecordAction.Detail;
    }

    /// <summary>Creates a selector: master if line exactly matches the specified text.</summary>
    public static MasterDetailSelector Equals(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return line => line.SequenceEqual(text.AsSpan()) ? RecordAction.Master : RecordAction.Detail;
    }

    /// <summary>Creates a selector: master if line length is less than the threshold.</summary>
    public static MasterDetailSelector HasLengthLessThan(int length)
    {
        return line => line.Length < length ? RecordAction.Master : RecordAction.Detail;
    }

    /// <summary>Creates a selector: master if line length is more than the threshold.</summary>
    public static MasterDetailSelector HasLengthMoreThan(int length)
    {
        return line => line.Length > length ? RecordAction.Master : RecordAction.Detail;
    }
}
