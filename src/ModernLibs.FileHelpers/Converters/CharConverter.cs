namespace ModernLibs.FileHelpers.Converters;

/// <summary>
/// Converts <see cref="char"/> fields with the FileHelpers 3.x <c>CharConverter</c> semantics:
/// the first character of the input (not trimmed) is taken and empty input parses as
/// <see cref="char.MinValue"/>. An optional format argument upper- or lower-cases the
/// character on both read and write. Zero allocations on both parse and format paths.
/// </summary>
public sealed class CharConverter : ISpanConverter<char>
{
    /// <summary>Shared singleton instance that keeps the character case unchanged.</summary>
    public static readonly CharConverter Instance = new();

    private readonly CharCase _case;

    private CharConverter() { }

    /// <summary>
    /// Initializes a converter with a 3.x case format
    /// (<c>[FieldConverter(ConverterKind.Char, format)]</c>).
    /// </summary>
    /// <param name="format">
    /// <c>""</c> (or <see langword="null"/>) keeps the case, <c>"x"</c> or <c>"lower"</c> lower-cases,
    /// <c>"X"</c> or <c>"upper"</c> upper-cases (invariant culture). Surrounding blanks are ignored.
    /// </param>
    /// <exception cref="BadUsageException">The format is not one of the values above.</exception>
    public CharConverter(string? format)
    {
        _case = (format ?? string.Empty).Trim() switch
        {
            "" => CharCase.NoChange,
            "x" or "lower" => CharCase.Lower,
            "X" or "upper" => CharCase.Upper,
            _ => throw new BadUsageException(
                "The format of the Char Converter must be \"\", \"x\" or \"lower\" for lower case, \"X\" or \"upper\" for upper case"),
        };
    }

    private enum CharCase
    {
        NoChange = 0,
        Lower,
        Upper,
    }

    /// <inheritdoc/>
    public bool TryParse(ReadOnlySpan<char> span, out char value)
    {
        value = span.IsEmpty ? char.MinValue : ApplyCase(span[0]);
        return true;
    }

    /// <inheritdoc/>
    public bool TryFormat(char value, Span<char> destination, out int charsWritten)
    {
        if (destination.IsEmpty)
        {
            charsWritten = 0;
            return false;
        }

        destination[0] = ApplyCase(value);
        charsWritten = 1;
        return true;
    }

    private char ApplyCase(char c) => _case switch
    {
        CharCase.Lower => char.ToLowerInvariant(c),
        CharCase.Upper => char.ToUpperInvariant(c),
        _ => c,
    };
}
