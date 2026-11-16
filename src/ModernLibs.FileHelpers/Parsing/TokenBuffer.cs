namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// The memory behind <see cref="RecordTokens"/>: one end entry per field and the scratch for unescaped values.
/// Owned by one reader (a read session, a multi-record entry, one record type of a master-detail reader), never
/// by <c>RecordReadCore&lt;T&gt;</c>, which parallel chunks share (design 4.3).
/// </summary>
/// <param name="fieldCount">The field count of the record type: <see cref="Ends"/> holds one entry more, so one extra field is detectable.</param>
internal sealed class TokenBuffer(int fieldCount)
{
    /// <summary>Record types with more fields are not tokenized (freeze F-2; internal, not contract).</summary>
    public const int MaxFieldCount = 4096;

    /// <summary>Longer records are not tokenized: an end offset must fit in bits 0-29.</summary>
    public const int MaxRecordLength = (1 << 30) - 1;

    /// <summary>Bit 31 of an end entry: the field is quoted.</summary>
    public const int QuotedBit = 1 << 31;

    /// <summary>Bit 30 of an end entry: the quoted value contains a doubled quote.</summary>
    public const int DoubledBit = 1 << 30;

    /// <summary>Bits 0-29 of an end entry: the end offset of the field's raw text (the delimiter after it, or the record length).</summary>
    public const int EndMask = DoubledBit - 1;

    private char[] _scratch = [];
    private int _scratchUsed;

    /// <summary>The end entries; <c>fieldCount + 1</c> long.</summary>
    public int[] Ends { get; } = new int[fieldCount + 1];

    /// <summary>
    /// Makes the scratch at least <paramref name="length"/> chars long. Called by the tokenizer before the
    /// descriptor runs, when a field of the record has <see cref="DoubledBit"/> (at least the record length), never
    /// by <see cref="RecordTokens.GetValue"/>: every span of one record stays valid together (freeze F-6).
    /// </summary>
    /// <param name="length">The scratch length needed.</param>
    public void EnsureScratch(int length)
    {
        if (_scratch.Length < length)
            _scratch = new char[length];
    }

    /// <summary>
    /// The tokens of <paramref name="record"/>, whose first <paramref name="count"/> entries of <see cref="Ends"/> the
    /// caller has written. Starts the scratch over: the spans of the record before are no longer valid.
    /// </summary>
    /// <param name="record">The record text.</param>
    /// <param name="count">The fields found, at most <c>Ends.Length</c>.</param>
    /// <param name="delimiterLength">The length of the delimiter between two fields.</param>
    /// <param name="quote">The quote char of the quoted fields, <c>'\0'</c> when there are none.</param>
    /// <returns>The tokens.</returns>
    public RecordTokens Tokens(ReadOnlySpan<char> record, int count, int delimiterLength, char quote = '\0')
    {
        _scratchUsed = 0;
        return new RecordTokens(record, Ends.AsSpan(0, count), delimiterLength, quote, this);
    }

    /// <summary>
    /// <paramref name="inner"/> with each doubled <paramref name="quote"/> collapsed (as
    /// <see cref="RecordParsing.Unquote"/>), appended to the scratch; a new array when the scratch has no room left.
    /// </summary>
    internal ReadOnlySpan<char> Unquote(ReadOnlySpan<char> inner, char quote)
    {
        if (_scratch.Length - _scratchUsed < inner.Length)
            return RecordParsing.Unquote(inner, quote);

        var destination = _scratch.AsSpan(_scratchUsed);
        int written = 0;
        while (true)
        {
            int at = inner.IndexOf(quote);
            if (at < 0)
            {
                inner.CopyTo(destination.Slice(written));
                written += inner.Length;
                break;
            }

            // Keep the quote, drop its double when one follows; a lone quote is kept as it is.
            inner.Slice(0, at + 1).CopyTo(destination.Slice(written));
            written += at + 1;
            inner = inner.Slice(at + 1 < inner.Length && inner[at + 1] == quote ? at + 2 : at + 1);
        }

        var value = destination.Slice(0, written);
        _scratchUsed += written;
        return value;
    }
}
