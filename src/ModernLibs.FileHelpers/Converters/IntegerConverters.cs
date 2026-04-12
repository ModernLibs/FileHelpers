// MA0048: File name intentionally does not match type names — all integer converters grouped in one file.
#pragma warning disable MA0048
using System.Globalization;

namespace ModernLibs.FileHelpers.Converters;

/// <summary>Converts <see cref="byte"/> fields using span-based parsing and formatting. Zero allocations.</summary>
public sealed class ByteConverter : ISpanConverter<byte>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly ByteConverter Instance = new();

    private ByteConverter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out byte value)
        => byte.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <inheritdoc/>
    public bool TryFormat(byte value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);
}

/// <summary>Converts <see cref="sbyte"/> fields using span-based parsing and formatting. Zero allocations.</summary>
public sealed class SByteConverter : ISpanConverter<sbyte>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly SByteConverter Instance = new();

    private SByteConverter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out sbyte value)
        => sbyte.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <inheritdoc/>
    public bool TryFormat(sbyte value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);
}

/// <summary>Converts <see cref="short"/> fields using span-based parsing and formatting. Zero allocations.</summary>
public sealed class Int16Converter : ISpanConverter<short>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly Int16Converter Instance = new();

    private Int16Converter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out short value)
        => short.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <inheritdoc/>
    public bool TryFormat(short value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);
}

/// <summary>Converts <see cref="int"/> fields using span-based parsing and formatting. Zero allocations.</summary>
public sealed class Int32Converter : ISpanConverter<int>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly Int32Converter Instance = new();

    private Int32Converter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out int value)
        => int.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <inheritdoc/>
    public bool TryFormat(int value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);
}

/// <summary>Converts <see cref="long"/> fields using span-based parsing and formatting. Zero allocations.</summary>
public sealed class Int64Converter : ISpanConverter<long>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly Int64Converter Instance = new();

    private Int64Converter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out long value)
        => long.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <inheritdoc/>
    public bool TryFormat(long value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);
}

/// <summary>Converts <see cref="ushort"/> fields using span-based parsing and formatting. Zero allocations.</summary>
public sealed class UInt16Converter : ISpanConverter<ushort>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly UInt16Converter Instance = new();

    private UInt16Converter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out ushort value)
        => ushort.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <inheritdoc/>
    public bool TryFormat(ushort value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);
}

/// <summary>Converts <see cref="uint"/> fields using span-based parsing and formatting. Zero allocations.</summary>
public sealed class UInt32Converter : ISpanConverter<uint>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly UInt32Converter Instance = new();

    private UInt32Converter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out uint value)
        => uint.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <inheritdoc/>
    public bool TryFormat(uint value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);
}

/// <summary>Converts <see cref="ulong"/> fields using span-based parsing and formatting. Zero allocations.</summary>
public sealed class UInt64Converter : ISpanConverter<ulong>
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly UInt64Converter Instance = new();

    private UInt64Converter() { }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out ulong value)
        => ulong.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <inheritdoc/>
    public bool TryFormat(ulong value, Span<char> destination, out int charsWritten)
        => value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);
}
