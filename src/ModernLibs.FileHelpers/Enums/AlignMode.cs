namespace ModernLibs.FileHelpers;

/// <summary>Indicates the alignment of a fixed-length field when written.</summary>
public enum AlignMode
{
    /// <summary>The field is left-aligned (padded on the right).</summary>
    Left = 0,

    /// <summary>The field is centered (padded equally on both sides).</summary>
    Center = 1,

    /// <summary>The field is right-aligned (padded on the left).</summary>
    Right = 2,
}
