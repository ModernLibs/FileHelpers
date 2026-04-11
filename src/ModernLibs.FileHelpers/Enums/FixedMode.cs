namespace ModernLibs.FileHelpers;

/// <summary>Indicates the behavior when variable-length records are found in a fixed-length file.</summary>
public enum FixedMode
{
    /// <summary>Records must have length equal to the sum of all field lengths. Default.</summary>
    ExactLength = 0,

    /// <summary>Records may contain more characters after the last field.</summary>
    AllowMoreChars = 1,

    /// <summary>Records may contain fewer characters; use with [FieldOptional] for trailing fields.</summary>
    AllowLessChars = 2,

    /// <summary>Records may contain more or fewer characters in the last field.</summary>
    AllowVariableLength = 3,
}
