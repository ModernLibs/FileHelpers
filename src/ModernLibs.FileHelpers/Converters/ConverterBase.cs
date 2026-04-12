// MA0048: Two related types (ConverterBase + ConverterBaseAdapter<T>) grouped in one file intentionally.
#pragma warning disable MA0048

namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Legacy FileHelpers 3.x custom converter base class. Subclass and override
/// <see cref="StringToField"/> and <see cref="FieldToString"/>.
/// </summary>
/// <remarks>
/// For zero-allocation performance, implement <see cref="ISpanConverter{T}"/> directly instead.
/// Use <see cref="ConverterBaseAdapter{T}"/> to bridge a <see cref="ConverterBase"/> into the
/// <see cref="ISpanConverter{T}"/> pipeline.
/// </remarks>
public abstract class ConverterBase
{
    /// <summary>Converts a string field value to the target type.</summary>
    public abstract object? StringToField(string text);

    /// <summary>Converts the target type back to a string field value.</summary>
    public abstract string? FieldToString(object? field);
}

/// <summary>
/// Bridges a <see cref="ConverterBase"/> implementation into the <see cref="ISpanConverter{T}"/> pipeline.
/// </summary>
/// <remarks>
/// This adapter allocates one string per operation. Use <see cref="ISpanConverter{T}"/>
/// for zero-alloc performance.
/// </remarks>
public sealed class ConverterBaseAdapter<T> : ISpanConverter<T>
{
    private readonly ConverterBase _inner;

    /// <summary>Initializes a new adapter wrapping the given <see cref="ConverterBase"/> instance.</summary>
    public ConverterBaseAdapter(ConverterBase inner) => _inner = inner;

    /// <inheritdoc/>
    /// <remarks>Allocates one string per call (span → string for ConverterBase delegation).</remarks>
    public bool TryParse(ReadOnlySpan<char> span, out T value)
    {
        string s = new string(span);          // one allocation
        object? raw = _inner.StringToField(s);
        if (raw is T t) { value = t; return true; }
        value = default!;
        return false;
    }

    /// <inheritdoc/>
    /// <remarks>Allocates one string per call (ConverterBase returns string).</remarks>
    public bool TryFormat(T value, Span<char> destination, out int charsWritten)
    {
        string? s = _inner.FieldToString(value);     // one allocation (or null)
        if (s is null) { charsWritten = 0; return true; }
        if (!s.AsSpan().TryCopyTo(destination)) { charsWritten = 0; return false; }
        charsWritten = s.Length;
        return true;
    }
}
