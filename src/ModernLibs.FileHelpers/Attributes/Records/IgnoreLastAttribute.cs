namespace ModernLibs.FileHelpers;

/// <summary>
/// Skips the last N lines of the input file after all records have been parsed.
/// Used to skip footer rows that are not part of the data.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class IgnoreLastAttribute : Attribute
{
    /// <summary>The number of lines to skip at the end of the file. Default is 1.</summary>
    public int NumberOfLines { get; }

    /// <summary>Skips the last line of the input file.</summary>
    public IgnoreLastAttribute() : this(1) { }

    /// <summary>Skips the last <paramref name="numberOfLines"/> lines of the input file.</summary>
    /// <param name="numberOfLines">The number of lines to skip.</param>
    public IgnoreLastAttribute(int numberOfLines)
    {
        NumberOfLines = numberOfLines;
    }
}
