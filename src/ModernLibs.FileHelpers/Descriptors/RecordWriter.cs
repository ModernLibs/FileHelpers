using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ModernLibs.FileHelpers;

/// <summary>
/// The writer of one record for <see cref="IRecordWriterDescriptor{T}.Format(T, ref RecordWriter)"/>: generated code
/// appends the text of the record, formats converted values straight into its free space, and quotes or pads a field
/// in place. Infrastructure for source-generated code; not intended for direct use.
/// </summary>
/// <remarks>
/// <para>Only the runtime creates a writer. Every member of <c>default(RecordWriter)</c> throws
/// <see cref="InvalidOperationException"/>.</para>
/// <para>The record is contiguous and <see cref="Position"/> counts from its start; growth never changes it, so a
/// field start read from <see cref="Position"/> stays valid. A span read from <see cref="FreeSpan"/> is valid only
/// until the next call of another member.</para>
/// <para>A converted field is written as
/// <c>while (!converter.TryFormat(value, writer.FreeSpan, out n)) writer.Grow(converter); writer.Advance(n);</c>.</para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public ref struct RecordWriter
{
    /// <summary>The free space <see cref="Grow"/> gives up to; from this size on it throws (4.9: <c>__Format</c>).</summary>
    private const int MaxConverterBuffer = 0x100000;

    /// <summary>The least free space <see cref="Grow"/> gives (4.9: the 64-char stack buffer per field).</summary>
    private const int MinGrowth = 64;

    /// <summary>
    /// Up to this length <see cref="Quote"/> moves, and <see cref="WriteQuoted"/> copies, text that holds a quote char
    /// with one scalar loop instead of the block copies between quote chars (G2, T23 review; local smoke: faster at 16
    /// and 64 chars).
    /// </summary>
    private const int ScalarQuoteLength = 64;

    /// <summary>
    /// Up to this length <see cref="Quote"/> shifts text without a quote char with a scalar loop: the source and the
    /// destination overlap, and an overlapping <c>CopyTo</c> leaves the managed copy for the native <c>memmove</c>, a
    /// fixed cost a short field does not earn back (G2, T23 review; local smoke: the loop is faster at 16 chars and
    /// slower at 64).
    /// </summary>
    private const int ScalarShiftLength = 16;

    private readonly RecordWriteBuffer? _buffer;

    /// <summary>The storage from the start of the record to its end.</summary>
    private Span<char> _span;

    /// <summary>The chars of the record written so far.</summary>
    private int _position;

    internal RecordWriter(RecordWriteBuffer buffer, Span<char> span)
    {
        _buffer = buffer;
        _span = span;
        _position = 0;
    }

    /// <summary>The number of chars of this record written so far; 0 when <c>Format</c> is entered.</summary>
    /// <exception cref="InvalidOperationException">The writer is <c>default</c>.</exception>
    public readonly int Position
    {
        get
        {
            ThrowIfDefault();
            return _position;
        }
    }

    /// <summary>
    /// The writable space at <see cref="Position"/>, of any length (also 0). Read it again after any other call:
    /// growth moves the record.
    /// </summary>
    /// <exception cref="InvalidOperationException">The writer is <c>default</c>.</exception>
    public readonly Span<char> FreeSpan
    {
        get
        {
            ThrowIfDefault();
            return _span[_position..];
        }
    }

    /// <summary>Marks <paramref name="count"/> chars of <see cref="FreeSpan"/> as written.</summary>
    /// <param name="count">The number of chars written into <see cref="FreeSpan"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is below 0 or above the length of <see cref="FreeSpan"/>.</exception>
    /// <exception cref="InvalidOperationException">The writer is <c>default</c>.</exception>
    public void Advance(int count)
    {
        ThrowIfDefault();
        if ((uint)count > (uint)(_span.Length - _position))
            ThrowAdvanceOutOfRange(count);
        _position += count;
    }

    /// <summary>
    /// Called after <paramref name="converter"/> returned <see langword="false"/> for <see cref="FreeSpan"/>: makes the
    /// free space at least twice as large and at least 64 chars. <see cref="Position"/> and the text are kept.
    /// </summary>
    /// <param name="converter">The converter that did not fit; its type name is in the exception text.</param>
    /// <exception cref="FormatException">The free space is already 0x100000 chars or more (the 4.9 text: <c>The field
    /// value could not be formatted by &lt;converter type&gt;.</c>).</exception>
    /// <exception cref="InvalidOperationException">The writer is <c>default</c>.</exception>
    public void Grow(object converter)
    {
        ThrowIfDefault();
        ArgumentNullException.ThrowIfNull(converter);
        int free = _span.Length - _position;
        if (free >= MaxConverterBuffer)
            throw new FormatException("The field value could not be formatted by " + converter.GetType().Name + ".");
        GrowFree(Math.Max(free * 2, MinGrowth));
    }

    /// <summary>Capacity hint: makes at least <paramref name="count"/> chars free. Writes nothing.</summary>
    /// <param name="count">The chars the record still needs, for example the width of a fixed-length record.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is below 0.</exception>
    /// <exception cref="InvalidOperationException">The writer is <c>default</c>.</exception>
    public void Reserve(int count)
    {
        ThrowIfDefault();
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > _span.Length - _position)
            GrowFree(count);
    }

    /// <summary>Appends <paramref name="value"/>.</summary>
    /// <param name="value">The char to write.</param>
    /// <exception cref="InvalidOperationException">The writer is <c>default</c>.</exception>
    public void Write(char value)
    {
        ThrowIfDefault();
        if (_position == _span.Length)
            GrowFree(1);
        _span[_position++] = value;
    }

    /// <summary>Appends <paramref name="text"/>; the writer does not keep it.</summary>
    /// <param name="text">The text to write.</param>
    /// <exception cref="InvalidOperationException">The writer is <c>default</c>.</exception>
    public void Write(scoped ReadOnlySpan<char> text)
    {
        ThrowIfDefault();
        if (text.Length > _span.Length - _position)
            GrowFree(text.Length);
        text.CopyTo(_span[_position..]);
        _position += text.Length;
    }

    /// <summary>
    /// Appends the newline of the write session: the engine's <c>NewLineForWrite</c>; <c>"\r\n"</c> through
    /// <see cref="RecordFormatting.Format{T}"/> into another writer. Writes nothing when it is empty.
    /// </summary>
    /// <exception cref="InvalidOperationException">The writer is <c>default</c>.</exception>
    public void WriteNewLine()
    {
        ThrowIfDefault();
        Write(_buffer!.NewLine.AsSpan());
    }

    /// <summary>
    /// Quotes the field written since <paramref name="fieldStart"/> in place, as the 4.9 helpers
    /// <c>__WriteQuotedField</c> and <c>__WriteQuoted</c> did: when <paramref name="always"/> is set, the text holds a
    /// line break or the text holds <paramref name="separator"/>, it is enclosed in <paramref name="quote"/> and each
    /// quote char in it is doubled.
    /// </summary>
    /// <param name="fieldStart">The <see cref="Position"/> at the start of the field.</param>
    /// <param name="quote">The quote char.</param>
    /// <param name="always">Quote every value.</param>
    /// <param name="multilineWrite">A value with a line break may be written (quoted).</param>
    /// <param name="separator">The delimiter after the field; a value that holds it is quoted.</param>
    /// <param name="field">The field name, for the exception text.</param>
    /// <exception cref="BadUsageException">The value holds a line break and <paramref name="multilineWrite"/> is
    /// <see langword="false"/> (the 4.9 text).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fieldStart"/> is not in <c>[0, Position]</c>.</exception>
    /// <exception cref="InvalidOperationException">The writer is <c>default</c>.</exception>
    public void Quote(int fieldStart, char quote, bool always, bool multilineWrite, string separator, string field)
    {
        ThrowIfDefault();
        CheckFieldStart(fieldStart);
        ReadOnlySpan<char> text = _span[fieldStart.._position];
        if (!MustQuote(text, quote, always, multilineWrite, separator, field, out int quotes))
            return;

        int length = text.Length;
        int added = quotes + 2;
        if (added > _span.Length - _position)
            GrowFree(added);

        // The quoted text is longer, so the text moves right in place, from the end. Short text: one scalar loop that
        // writes each quote char twice (the limit depends on whether the text holds a quote char). Longer text: without
        // a quote char it shifts by one; otherwise the blocks between quote chars are copied, each quote char written
        // twice (G3). Every destination lies right of the text not yet moved; CopyTo handles the overlap of a block
        // with its own destination.
        Span<char> target = _span.Slice(fieldStart, length + added);
        if (length <= (quotes == 0 ? ScalarShiftLength : ScalarQuoteLength))
        {
            int to = length + quotes;
            for (int from = length - 1; from >= 0; from--)
            {
                char c = target[from];
                target[to--] = c;
                if (c == quote)
                    target[to--] = quote;
            }
        }
        else if (quotes == 0)
        {
            target[..length].CopyTo(target[1..]);
        }
        else
        {
            int to = length + quotes + 1;
            int end = length;
            while (true)
            {
                int at = target[..end].LastIndexOf(quote);
                int block = end - at - 1;
                to -= block;
                target.Slice(at + 1, block).CopyTo(target[to..]);
                if (at < 0)
                    break;
                target[--to] = quote;
                target[--to] = quote;
                end = at;
            }
        }

        target[0] = quote;
        target[^1] = quote;
        _position = fieldStart + length + added;
    }

    /// <summary>
    /// Appends <paramref name="text"/> as a quoted field, as the 4.9 helper <c>__WriteQuotedField</c> did: the same
    /// text, <see cref="Position"/> and errors as <see cref="Write(ReadOnlySpan{char})"/> followed by
    /// <see cref="Quote"/> from the <see cref="Position"/> before the call, but the text is tested in
    /// <paramref name="text"/> and copied once (freeze Revision 1). The writer does not keep <paramref name="text"/>,
    /// which must not be part of the writer's own storage.
    /// </summary>
    /// <param name="text">The field value.</param>
    /// <param name="quote">The quote char.</param>
    /// <param name="always">Quote every value.</param>
    /// <param name="multilineWrite">A value with a line break may be written (quoted).</param>
    /// <param name="separator">The delimiter after the field; a value that holds it is quoted.</param>
    /// <param name="field">The field name, for the exception text.</param>
    /// <exception cref="BadUsageException">The value holds a line break and <paramref name="multilineWrite"/> is
    /// <see langword="false"/> (the 4.9 text).</exception>
    /// <exception cref="InvalidOperationException">The writer is <c>default</c>.</exception>
    public void WriteQuoted(scoped ReadOnlySpan<char> text, char quote, bool always, bool multilineWrite, string separator, string field)
    {
        ThrowIfDefault();
        if (!MustQuote(text, quote, always, multilineWrite, separator, field, out int quotes))
        {
            Write(text);
            return;
        }

        int total = text.Length + quotes + 2;
        if (total > _span.Length - _position)
            GrowFree(total);

        // Source and destination do not overlap: one copy without a quote char; with quote chars, a scalar loop for
        // short text, otherwise forward copies of the blocks up to and including each quote char, which is then written
        // once more (local smoke: the loop is faster at 16 and 64 chars with a quote char every 8).
        Span<char> target = _span.Slice(_position, total);
        target[0] = quote;
        if (quotes == 0)
        {
            text.CopyTo(target[1..]);
        }
        else if (text.Length <= ScalarQuoteLength)
        {
            int to = 1;
            for (int from = 0; from < text.Length; from++)
            {
                char c = text[from];
                target[to++] = c;
                if (c == quote)
                    target[to++] = quote;
            }
        }
        else
        {
            int to = 1;
            ReadOnlySpan<char> rest = text;
            int at;
            while ((at = rest.IndexOf(quote)) >= 0)
            {
                rest[..(at + 1)].CopyTo(target[to..]);
                to += at + 1;
                target[to++] = quote;
                rest = rest[(at + 1)..];
            }

            rest.CopyTo(target[to..]);
        }

        target[^1] = quote;
        _position += total;
    }

    /// <summary>
    /// The quoting rule of 4.9's <c>__WriteQuotedField</c>, shared by <see cref="Quote"/> and <see cref="WriteQuoted"/>:
    /// a line break without <paramref name="multilineWrite"/> throws; otherwise the value is quoted when
    /// <paramref name="always"/> is set, it holds a line break or it holds <paramref name="separator"/>.
    /// <paramref name="quotes"/> is the number of quote chars in a value that is quoted. One scan finds the first quote
    /// char or line break, so a value with neither is not scanned again (G2).
    /// </summary>
    private static bool MustQuote(ReadOnlySpan<char> text, char quote, bool always, bool multilineWrite, string separator, string field, out int quotes)
    {
        quotes = 0;
        int first = text.IndexOfAny(quote, '\r', '\n');
        bool newLine = first >= 0 && text[first..].IndexOfAny('\r', '\n') >= 0;
        if (newLine && !multilineWrite)
        {
            throw new BadUsageException("One value for the field " + field + " has a new line inside. "
                + "To allow this value to be written you must add a FieldQuoted attribute with the multiline option set.");
        }

        if (!always && !newLine && text.IndexOf(separator, StringComparison.Ordinal) < 0)
            return false;

        if (first >= 0)
            quotes = text[first..].Count(quote);
        return true;
    }

    /// <summary>
    /// Pads the field written since <paramref name="fieldStart"/> to <paramref name="width"/> chars in place, as the
    /// 4.9 helper <c>__WritePadded</c> did: a longer value is cut at its end (or throws when
    /// <paramref name="overflowError"/> is set); <paramref name="align"/> 0 pads on the right, 1 on both sides, 2 on
    /// the left. Afterwards <see cref="Position"/> is <c>fieldStart + width</c>.
    /// </summary>
    /// <param name="fieldStart">The <see cref="Position"/> at the start of the field.</param>
    /// <param name="width">The field width.</param>
    /// <param name="align">0 left, 1 center, 2 right.</param>
    /// <param name="pad">The fill char.</param>
    /// <param name="overflowError">Throw instead of cutting a longer value.</param>
    /// <param name="field">The field name, for the exception text.</param>
    /// <exception cref="InvalidOperationException">The value is longer than <paramref name="width"/> and
    /// <paramref name="overflowError"/> is set (the 4.9 text), or the writer is <c>default</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fieldStart"/> is not in <c>[0, Position]</c>, or
    /// <paramref name="width"/> is below 0.</exception>
    public void Pad(int fieldStart, int width, int align, char pad, bool overflowError, string field)
    {
        ThrowIfDefault();
        CheckFieldStart(fieldStart);
        int length = _position - fieldStart;
        if (length > width)
        {
            if (overflowError)
                throw new InvalidOperationException($"Field '{field}' exceeds fixed length {width} (actual: {length} characters).");
            ArgumentOutOfRangeException.ThrowIfNegative(width);
            length = width;
        }

        int end = fieldStart + width;
        if (end > _span.Length)
            GrowFree(end - _position);

        Span<char> target = _span.Slice(fieldStart, width);
        int padding = width - length;
        int left = align == 2 ? padding : align == 1 ? padding / 2 : 0;
        if (left > 0)
            target[..length].CopyTo(target[left..]);
        target[..left].Fill(pad);
        target.Slice(left + length, padding - left).Fill(pad);
        _position = end;
    }

    /// <summary>
    /// Appends <paramref name="text"/> as a field of <paramref name="width"/> chars: the same text,
    /// <see cref="Position"/> and errors as <see cref="Write(ReadOnlySpan{char})"/> followed by <see cref="Pad"/> from
    /// the <see cref="Position"/> before the call, in one call that copies the text to its final place (a string
    /// field of a fixed-length record costs one call instead of three). <see cref="Position"/> after the
    /// <see cref="InvalidOperationException"/> for a longer value is not specified (the engine discards the record).
    /// The writer does not keep <paramref name="text"/>, which must not be part of the writer's own storage.
    /// </summary>
    /// <param name="text">The field value.</param>
    /// <param name="width">The field width.</param>
    /// <param name="align">0 left, 1 center, 2 right.</param>
    /// <param name="pad">The fill char.</param>
    /// <param name="overflowError">Throw instead of cutting a longer value.</param>
    /// <param name="field">The field name, for the exception text.</param>
    /// <exception cref="InvalidOperationException">The value is longer than <paramref name="width"/> and
    /// <paramref name="overflowError"/> is set (the 4.9 text), or the writer is <c>default</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> is below 0.</exception>
    public void WritePadded(scoped ReadOnlySpan<char> text, int width, int align, char pad, bool overflowError, string field)
    {
        ThrowIfDefault();
        if (text.Length > width)
        {
            if (overflowError)
                ThrowFieldTooLong(field, width, text.Length);
            ArgumentOutOfRangeException.ThrowIfNegative(width);
            text = text[..width];
        }

        if (width > _span.Length - _position)
            GrowFree(width);

        Span<char> target = _span.Slice(_position, width);
        int padding = width - text.Length;
        int left = align == 2 ? padding : align == 1 ? padding / 2 : 0;

        // Stryker disable once all : the test is for speed only (a value of the field width skips two Fill calls); without it both fills have length 0 and the text is the same.
        if (padding != 0)
        {
            target[..left].Fill(pad);
            target.Slice(left + text.Length, padding - left).Fill(pad);
        }

        text.CopyTo(target[left..]);
        _position += width;
    }

    [DoesNotReturn]
    private static void ThrowFieldTooLong(string field, int width, int length) =>
        throw new InvalidOperationException($"Field '{field}' exceeds fixed length {width} (actual: {length} characters).");

    /// <summary>Makes at least <paramref name="minFree"/> chars free after <see cref="Position"/>; keeps the record.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowFree(int minFree) => _span = _buffer!.GrowRecord(_position, minFree);

    private readonly void CheckFieldStart(int fieldStart)
    {
        if ((uint)fieldStart > (uint)_position)
            ThrowFieldStartOutOfRange(fieldStart);
    }

    private readonly void ThrowIfDefault()
    {
        if (_buffer is null)
            ThrowDefault();
    }

    [DoesNotReturn]
    private static void ThrowDefault() =>
        throw new InvalidOperationException("This RecordWriter is default. Only the runtime creates a RecordWriter for IRecordWriterDescriptor<T>.Format.");

    [DoesNotReturn]
    private static void ThrowAdvanceOutOfRange(int count) =>
        throw new ArgumentOutOfRangeException(nameof(count), count, "Advance count must be between 0 and the length of FreeSpan.");

    [DoesNotReturn]
    private static void ThrowFieldStartOutOfRange(int fieldStart) =>
        throw new ArgumentOutOfRangeException(nameof(fieldStart), fieldStart, "The field start must be between 0 and Position.");
}
