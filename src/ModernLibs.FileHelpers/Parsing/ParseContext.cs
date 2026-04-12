namespace ModernLibs.FileHelpers;

/// <summary>
/// Stack-allocated parse state carrier passed by the batch engine for each line.
/// A ref struct — never heap-allocated (D-07, PIPE-05).
/// </summary>
public ref struct ParseContext
{
    /// <summary>The 1-based line number being parsed.</summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// When set to <see langword="true"/> by the engine, instructs the generated
    /// Parse method to skip this record.
    /// </summary>
    public bool SkipRecord { get; set; }
}
