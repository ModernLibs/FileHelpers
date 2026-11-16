// The four write helpers of 4.9.1 generated code and the __WriteText helper they call: the oracle of
// RecordWriterTests (T22; an intended copy, controller rulings section 3). Copied verbatim from lines 35-103 of
// Compat/Generated49/Compat49QuoteOptionalWrite.DelimitedDescriptor.g.cs (4.9.1 generator, tag v4.9.1, commit
// 3a045e0; every Generated49 file holds the same text). Do not edit the copied block; the entry points after it
// only make the private helpers callable.

namespace ModernLibs.FileHelpers.Tests.Engines;

internal static class LegacyEmittedWriteCopies
{
    // ---- Begin of the verbatim copy ----
    private static void __WriteText(global::System.Buffers.IBufferWriter<char> writer, global::System.ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return;
        text.CopyTo(writer.GetSpan(text.Length));
        writer.Advance(text.Length);
    }

    private static void __WriteQuoted(global::System.Buffers.IBufferWriter<char> writer, global::System.ReadOnlySpan<char> text, char quote)
    {
        // Indexed loops: foreach over a span needs C# 13 against .NET 10, whose span enumerator implements interfaces.
        int count = global::System.MemoryExtensions.Count(text, quote);
        int total = text.Length + count + 2;
        var dest = writer.GetSpan(total);
        dest[0] = quote;
        int pos = 1;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            dest[pos++] = c;
            if (c == quote) dest[pos++] = quote;
        }
        dest[pos] = quote;
        writer.Advance(total);
    }

    private static void __WriteQuotedField(global::System.Buffers.IBufferWriter<char> writer, global::System.ReadOnlySpan<char> text, char quote, bool always, bool multilineWrite, string separator, string field)
    {
        bool newLine = global::System.MemoryExtensions.IndexOfAny(text, '\r', '\n') >= 0;
        if (newLine && !multilineWrite)
            throw new global::ModernLibs.FileHelpers.BadUsageException("One value for the field " + field + " has a new line inside. To allow this value to be written you must add a FieldQuoted attribute with the multiline option set.");
        if (always || newLine || global::System.MemoryExtensions.IndexOf(text, separator, global::System.StringComparison.Ordinal) >= 0)
            __WriteQuoted(writer, text, quote);
        else
            __WriteText(writer, text);
    }

    private static void __WritePadded(global::System.Buffers.IBufferWriter<char> writer, global::System.ReadOnlySpan<char> text, int width, int align, char pad, bool overflowError, string field)
    {
        if (text.Length > width)
        {
            if (overflowError)
                throw new global::System.InvalidOperationException($"Field '{field}' exceeds fixed length {width} (actual: {text.Length} characters).");
            text = text.Slice(0, width);
        }
        var dest = writer.GetSpan(width);
        int padding = width - text.Length;
        int left = align == 2 ? padding : align == 1 ? padding / 2 : 0;
        dest.Slice(0, left).Fill(pad);
        text.CopyTo(dest.Slice(left));
        dest.Slice(left + text.Length, padding - left).Fill(pad);
        writer.Advance(width);
    }

    private static global::System.ReadOnlySpan<char> __Format<TConverter, TValue>(TConverter converter, TValue value, global::System.Span<char> buffer, ref char[]? rented)
        where TConverter : global::ModernLibs.FileHelpers.Converters.ISpanConverter<TValue>
    {
        int written;
        while (!converter.TryFormat(value, buffer, out written))
        {
            if (buffer.Length >= 0x100000)
                throw new global::System.FormatException("The field value could not be formatted by " + converter.GetType().Name + ".");
            int size = buffer.Length * 2;
            if (rented is not null)
                global::System.Buffers.ArrayPool<char>.Shared.Return(rented);
            rented = global::System.Buffers.ArrayPool<char>.Shared.Rent(size);
            buffer = rented;
        }
        return buffer.Slice(0, written);
    }
    // ---- End of the verbatim copy ----

    public static void WriteText(global::System.Buffers.IBufferWriter<char> writer, global::System.ReadOnlySpan<char> text) =>
        __WriteText(writer, text);

    public static void WriteQuoted(global::System.Buffers.IBufferWriter<char> writer, global::System.ReadOnlySpan<char> text, char quote) =>
        __WriteQuoted(writer, text, quote);

    public static void WriteQuotedField(global::System.Buffers.IBufferWriter<char> writer, global::System.ReadOnlySpan<char> text, char quote, bool always, bool multilineWrite, string separator, string field) =>
        __WriteQuotedField(writer, text, quote, always, multilineWrite, separator, field);

    public static void WritePadded(global::System.Buffers.IBufferWriter<char> writer, global::System.ReadOnlySpan<char> text, int width, int align, char pad, bool overflowError, string field) =>
        __WritePadded(writer, text, width, align, pad, overflowError, field);

    /// <summary>
    /// The converted field of 4.9.1 generated code: <c>__Format</c> with the 64-char stack buffer, <c>__WriteText</c>,
    /// and the return of the rented buffer.
    /// </summary>
    public static void WriteConverted<TConverter, TValue>(global::System.Buffers.IBufferWriter<char> writer, TConverter converter, TValue value)
        where TConverter : global::ModernLibs.FileHelpers.Converters.ISpanConverter<TValue>
    {
        global::System.Span<char> buffer = stackalloc char[64];
        char[]? rented = null;
        try
        {
            __WriteText(writer, __Format(converter, value, buffer, ref rented));
        }
        finally
        {
            if (rented is not null)
                global::System.Buffers.ArrayPool<char>.Shared.Return(rented);
        }
    }
}
