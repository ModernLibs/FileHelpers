namespace ModernLibs.FileHelpers;

/// <summary>
/// Skips the first N lines of the input file before record parsing begins.
/// Used to skip header rows that are not part of the data.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class IgnoreFirstAttribute : Attribute
{
    /// <summary>The number of lines to skip at the beginning of the file. Default is 1.</summary>
    public int NumberOfLines { get; }

    /// <summary>Skips the first line of the input file.</summary>
    public IgnoreFirstAttribute() : this(1) { }

    /// <summary>Skips the first <paramref name="numberOfLines"/> lines of the input file.</summary>
    /// <param name="numberOfLines">The number of lines to skip.</param>
    public IgnoreFirstAttribute(int numberOfLines)
    {
        NumberOfLines = numberOfLines;
    }
}
