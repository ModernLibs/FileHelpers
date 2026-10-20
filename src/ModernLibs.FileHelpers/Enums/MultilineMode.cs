namespace ModernLibs.FileHelpers;

/// <summary>
/// Indicates whether quoted fields can span multiple lines
/// (<see cref="DelimitedRecordAttribute.MultilineMode"/>).
/// </summary>
/// <remarks>
/// With <see cref="AllowForRead"/> or <see cref="AllowForBoth"/>, a line that ends inside a quoted field is
/// joined with the following lines (using their original terminators) until the quote closes.
/// </remarks>
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
