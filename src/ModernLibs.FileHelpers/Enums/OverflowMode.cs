namespace ModernLibs.FileHelpers;

/// <summary>Controls behavior when a field value exceeds its declared fixed length during write.</summary>
public enum OverflowMode
{
    /// <summary>Truncate the value to fit within the declared length. Default.</summary>
    DiscardEnd = 0,

    /// <summary>Throw an exception when the value exceeds the declared length.</summary>
    Error = 1,
}
