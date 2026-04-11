namespace ModernLibs.FileHelpers;

/// <summary>
/// Skips empty lines encountered during parsing.
/// When <see cref="IgnoreSpaces"/> is <see langword="true"/>, lines containing only whitespace are also skipped.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class IgnoreEmptyLinesAttribute : Attribute
{
    /// <summary>
    /// When <see langword="true"/>, lines containing only whitespace characters are treated as empty and skipped.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool IgnoreSpaces { get; }

    /// <summary>Skips empty lines (zero-length only) during parsing.</summary>
    public IgnoreEmptyLinesAttribute() : this(false) { }

    /// <summary>Skips empty lines during parsing.</summary>
    /// <param name="ignoreSpaces">
    /// When <see langword="true"/>, lines containing only whitespace are also skipped.
    /// </param>
    public IgnoreEmptyLinesAttribute(bool ignoreSpaces)
    {
        IgnoreSpaces = ignoreSpaces;
    }
}
