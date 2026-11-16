using System.ComponentModel;
using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>
/// The fields of one record, as the runtime found them, for <see cref="ITokenizedDescriptor{T}"/>. Infrastructure
/// for source-generated code; not intended for direct use.
/// </summary>
/// <remarks>
/// <para>Only the runtime creates tokens. <c>default(RecordTokens)</c> has <see cref="Count"/> 0. No member
/// throws: an index outside <c>[0, Count)</c> gives <see langword="false"/>, an empty span or
/// <see cref="string.Empty"/>.</para>
/// <para>The tokens and every span <see cref="GetValue"/> returns are valid until the runtime tokenizes the next
/// record of the same read; generated code never stores them.</para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly ref struct RecordTokens
{
    // The layout is internal and may change in a patch (design 4.3): the record, one end entry per field found
    // (bits 0-29: end offset of the raw field text; bit 31: quoted; bit 30: the quoted value has a doubled
    // quote), the delimiter length, the quote char, and the buffer that owns the scratch for unescaped values.
    private readonly ReadOnlySpan<char> _record;
    private readonly ReadOnlySpan<int> _ends;
    private readonly int _delimiterLength;
    private readonly char _quote;
    private readonly TokenBuffer? _scratch;

    private const int QuotedDoubled = TokenBuffer.QuotedBit | TokenBuffer.DoubledBit;

    internal RecordTokens(ReadOnlySpan<char> record, ReadOnlySpan<int> ends, int delimiterLength, char quote, TokenBuffer? scratch)
    {
        _record = record;
        _ends = ends;
        _delimiterLength = delimiterLength;
        _quote = quote;
        _scratch = scratch;
    }

    /// <summary>The number of fields found. The runtime passes tokens only when it is between the mandatory and the total field count.</summary>
    public int Count => _ends.Length;

    /// <summary>
    /// <see langword="true"/> when field <paramref name="index"/> is a <see cref="FieldQuotedAttribute"/> field whose
    /// value was quoted; <see langword="false"/> for an index outside <c>[0, Count)</c>.
    /// </summary>
    /// <param name="index">The field index.</param>
    /// <returns>Whether the value was quoted.</returns>
    public bool IsQuoted(int index) => (uint)index < (uint)_ends.Length && _ends[index] < 0;

    /// <summary>
    /// The value of field <paramref name="index"/>: the slice of the record (also when empty, so a present empty
    /// field keeps its position), the slice between the quotes for a quoted value, or the unescaped copy of a quoted
    /// value with doubled quotes (not part of the record). An empty span for an index outside <c>[0, Count)</c>.
    /// </summary>
    /// <param name="index">The field index.</param>
    /// <returns>The field value.</returns>
    public ReadOnlySpan<char> GetValue(int index)
    {
        if ((uint)index >= (uint)_ends.Length)
            return default;

        int entry = _ends[index];
        var raw = Raw(index, entry);
        if (entry >= 0)
            return raw;

        var inner = Inner(raw);
        if ((entry & TokenBuffer.DoubledBit) == 0)
            return inner;

        return _scratch is { } scratch ? scratch.Unquote(inner, _quote) : RecordParsing.Unquote(inner, _quote);
    }

    /// <summary>
    /// The text between the quotes of a quoted entry's raw text. Empty when the raw text is shorter than the two
    /// quotes: the runtime never makes such an entry (an unclosed quote has no tokens), and no member throws.
    /// </summary>
    private static ReadOnlySpan<char> Inner(ReadOnlySpan<char> raw) => raw.Length < 2 ? default : raw[1..^1];

    /// <summary>
    /// The value of field <paramref name="index"/> as a string (see <see cref="GetValue"/>): <see cref="string.Empty"/>
    /// for an empty value or an index outside <c>[0, Count)</c>, a shared string for one char below 128, otherwise a
    /// new string; a quoted value with doubled quotes is unescaped straight into the string.
    /// </summary>
    /// <param name="index">The field index.</param>
    /// <returns>The field value.</returns>
    public string GetString(int index)
    {
        if ((uint)index < (uint)_ends.Length && _ends[index] is var entry && (entry & QuotedDoubled) == QuotedDoubled)
            return RecordParsing.UnquoteToString(Inner(Raw(index, entry)), _quote);

        return RecordParsing.CreateString(GetValue(index));
    }

    /// <summary>The raw text of field <paramref name="index"/> (quotes included), whose end entry is <paramref name="entry"/>.</summary>
    private ReadOnlySpan<char> Raw(int index, int entry)
    {
        int start = index == 0 ? 0 : (_ends[index - 1] & TokenBuffer.EndMask) + _delimiterLength;
        return _record.Slice(start, (entry & TokenBuffer.EndMask) - start);
    }
}
