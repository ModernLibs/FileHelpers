namespace ModernLibs.FileHelpers;

/// <summary>
/// Specifies the character width of a field in a fixed-length record.
/// Required on every field of a type marked with <see cref="FixedLengthRecordAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldFixedLengthAttribute : Attribute
{
    /// <summary>The number of characters this field occupies in the record line.</summary>
    public int Length { get; }

    /// <summary>
    /// Controls behavior when a field value exceeds <see cref="Length"/> on write.
    /// Default is <see cref="OverflowMode.DiscardEnd"/> (truncate).
    /// </summary>
    public OverflowMode OverflowMode { get; init; } = OverflowMode.DiscardEnd;

    /// <summary>Specifies the character width of this fixed-length field.</summary>
    /// <param name="length">The number of characters this field occupies.</param>
    public FieldFixedLengthAttribute(int length)
    {
        Length = length;
    }
}
