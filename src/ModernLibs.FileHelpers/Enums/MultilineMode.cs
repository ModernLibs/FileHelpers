namespace ModernLibs.FileHelpers;

/// <summary>
/// Indicates whether quoted fields can span multiple lines.
/// Reserved for v2 multiline support (ADVP-01).
/// </summary>
public enum MultilineMode
{
    /// <summary>Multiline fields are allowed both for reading and writing.</summary>
    AllowForBoth = 0,

    /// <summary>Multiline fields are allowed for reading only.</summary>
    AllowForRead = 1,

    /// <summary>Multiline fields are allowed for writing only.</summary>
    AllowForWrite = 2,

    /// <summary>Multiline fields are not allowed.</summary>
    NotAllow = 3,
}
