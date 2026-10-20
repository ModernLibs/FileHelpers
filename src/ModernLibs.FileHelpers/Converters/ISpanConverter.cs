namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Zero-allocation span-based converter between a field's text and <typeparamref name="T"/>.
/// </summary>
/// <remarks>
/// The built-in converters implement it and expose a shared <c>Instance</c>. A custom implementation is
/// used with <c>[FieldConverter(typeof(MyConverter))]</c>; the generated code creates one instance per field
/// through the public parameterless constructor. Unlike a <see cref="ConverterBase"/>, it also receives empty
/// and whitespace-only input unless the field has a <see cref="FieldNullValueAttribute"/>.
/// </remarks>
/// <typeparam name="T">The type to convert to/from a span of characters.</typeparam>
public interface ISpanConverter<T>
{
    /// <summary>Parses a value from the given character span. Returns false if parsing fails; sets <paramref name="value"/> to default on failure.</summary>
    /// <param name="span">The field text.</param>
    /// <param name="value">The parsed value.</param>
    /// <returns><see langword="true"/> when the text was converted.</returns>
    bool TryParse(ReadOnlySpan<char> span, out T value);

    /// <summary>Formats <paramref name="value"/> into <paramref name="destination"/>. Returns false if the destination is too small.</summary>
    /// <param name="value">The value to format.</param>
    /// <param name="destination">The buffer to write to; callers retry with a larger buffer when this returns <see langword="false"/>.</param>
    /// <param name="charsWritten">The number of characters written.</param>
    /// <returns><see langword="true"/> when the value fit into <paramref name="destination"/>.</returns>
    bool TryFormat(T value, Span<char> destination, out int charsWritten);
}
