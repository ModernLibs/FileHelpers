namespace ModernLibs.FileHelpers;

/// <summary>Indicates the trimming behavior applied after reading a field.</summary>
public enum TrimMode
{
    /// <summary>No trimming is performed.</summary>
    None = 0,

    /// <summary>The field is trimmed on both sides.</summary>
    Both = 1,

    /// <summary>The field is trimmed on the left.</summary>
    Left = 2,

    /// <summary>The field is trimmed on the right.</summary>
    Right = 3,
}
