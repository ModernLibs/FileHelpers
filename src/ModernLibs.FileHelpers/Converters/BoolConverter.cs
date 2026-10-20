namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="bool"/> fields with the FileHelpers 3.x <c>BooleanConverter</c> semantics.
/// </summary>
/// <remarks>
/// <para>
/// Default values (<see cref="Instance"/>): the input is trimmed and compared case-insensitively;
/// <c>"true"</c>, <c>"1"</c>, <c>"y"</c>, <c>"t"</c> parse as <see langword="true"/> and
/// <c>"false"</c>, <c>"0"</c>, <c>"n"</c>, <c>"f"</c> and the empty string parse as
/// <see langword="false"/>. Formats as <c>"True"</c> / <c>"False"</c>.
/// </para>
/// <para>
/// Custom values (<see cref="BoolConverter(string, string)"/>): only the configured strings are
/// accepted (case-insensitive, with a trimmed retry) and they are written verbatim.
/// </para>
/// Parsing and formatting are allocation-free.
/// </remarks>
public sealed class BoolConverter : ISpanConverter<bool>
{
    /// <summary>Shared singleton instance using the 3.x default true/false values.</summary>
    public static readonly BoolConverter Instance = new();

    private readonly string? _trueString;
    private readonly string? _falseString;

    private BoolConverter() { }

    /// <summary>
    /// Initializes a converter that accepts and writes only the given strings
    /// (3.x <c>[FieldConverter(ConverterKind.Boolean, trueStr, falseStr)]</c>).
    /// </summary>
    /// <param name="trueString">Text representing <see langword="true"/>.</param>
    /// <param name="falseString">Text representing <see langword="false"/>.</param>
    public BoolConverter(string trueString, string falseString)
    {
        ArgumentNullException.ThrowIfNull(trueString);
        ArgumentNullException.ThrowIfNull(falseString);
        _trueString = trueString;
        _falseString = falseString;
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out bool value)
    {
        if (_trueString is null || _falseString is null)
            return TryParseDefault(span.Trim(), out value);

        // 3.x: exact (case-insensitive) match first, then retry on the trimmed input.
        if (TryParseCustom(span, _trueString, _falseString, out value))
            return true;
        return TryParseCustom(span.Trim(), _trueString, _falseString, out value);
    }

    /// <inheritdoc/>
    public bool TryFormat(bool value, Span<char> destination, out int charsWritten)
    {
        ReadOnlySpan<char> text = value ? _trueString ?? "True" : _falseString ?? "False";
        if (!text.TryCopyTo(destination))
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = text.Length;
        return true;
    }

    private static bool TryParseDefault(ReadOnlySpan<char> trimmed, out bool value)
    {
        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("1", StringComparison.Ordinal) ||
            trimmed.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("t", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        value = false;
        return trimmed.IsEmpty ||
               trimmed.Equals("false", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("0", StringComparison.Ordinal) ||
               trimmed.Equals("n", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("f", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseCustom(ReadOnlySpan<char> span, string trueString, string falseString, out bool value)
    {
        value = span.Equals(trueString, StringComparison.OrdinalIgnoreCase);
        return value || span.Equals(falseString, StringComparison.OrdinalIgnoreCase);
    }
}
