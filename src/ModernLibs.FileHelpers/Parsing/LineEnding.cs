namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// The physical terminator that ended a line returned by the internal line sources.
/// Multiline quoted records are re-joined with the terminator that actually separated the
/// physical lines, so every read path produces the same field values for the same input.
/// </summary>
internal enum LineEnding : byte
{
    /// <summary>Last line of the input, not followed by a terminator.</summary>
    None = 0,

    /// <summary><c>\n</c>.</summary>
    Lf = 1,

    /// <summary>Bare <c>\r</c>.</summary>
    Cr = 2,

    /// <summary><c>\r\n</c>.</summary>
    CrLf = 3,
}
