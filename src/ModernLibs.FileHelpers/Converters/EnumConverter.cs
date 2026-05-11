namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Zero-allocation span-based converter for any <see cref="Enum"/> type.
/// Parsing is case-insensitive (FileHelpers 3.x default).
/// </summary>
/// <typeparam name="TEnum">The enum type to convert.</typeparam>
public sealed class EnumConverter<TEnum> : ISpanConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Singleton instance for this enum type.</summary>
    public static readonly EnumConverter<TEnum> Instance = new();

    private EnumConverter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out TEnum value)
        => Enum.TryParse<TEnum>(span, ignoreCase: true, out value);

    /// <inheritdoc/>
    public bool TryFormat(TEnum value, Span<char> destination, out int charsWritten)
    {
        var name = value.ToString();
        if (name.Length > destination.Length)
        {
            charsWritten = 0;
            return false;
        }
        name.AsSpan().CopyTo(destination);
        charsWritten = name.Length;
        return true;
    }
}
