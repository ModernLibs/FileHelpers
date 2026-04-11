namespace ModernLibs.FileHelpers;

/// <summary>
/// Trims characters from the parsed field value after reading.
/// By default, trims space (<c>' '</c>) and tab (<c>'\t'</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldTrimAttribute : Attribute
{
    /// <summary>The trimming direction (start, end, or both).</summary>
    public TrimMode TrimMode { get; }

    /// <summary>The set of characters to trim. Defaults to space and tab.</summary>
    public char[] TrimChars { get; }

    /// <summary>Trims space and tab characters in the specified direction.</summary>
    /// <param name="mode">The trimming direction.</param>
    public FieldTrimAttribute(TrimMode mode) : this(mode, ' ', '\t') { }

    /// <summary>Trims the specified characters in the given direction.</summary>
    /// <param name="mode">The trimming direction.</param>
    /// <param name="chars">The characters to trim.</param>
    public FieldTrimAttribute(TrimMode mode, params char[] chars)
    {
        TrimMode = mode;
        TrimChars = chars;
    }
}
