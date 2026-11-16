using System.Buffers;

namespace ModernLibs.FileHelpers.Tests.Descriptors;

/// <summary>
/// Copies, with the locals renamed, of the code the 4.9 generator emits (taken from
/// <c>Generated49/Compat49QuoteAlways.DelimitedDescriptor.g.cs</c> and <c>Compat49ConverterBase.DelimitedDescriptor.g.cs</c>),
/// the reference for the hidden read helpers of <see cref="RecordParsing"/>.
/// </summary>
internal static class LegacyEmittedCopies
{
    /// <summary>The emitted <c>[FieldQuoted] strip + unescape</c> loop, ending in <c>new string</c>.</summary>
    public static string Strip(ReadOnlySpan<char> inner, char quote)
    {
        char[]? rent = null;
        try
        {
            ReadOnlySpan<char> value = inner;
            bool hasDoubled = false;
            for (int si = 0; si < inner.Length - 1; si++)
            {
                if (inner[si] == quote && inner[si + 1] == quote)
                {
                    hasDoubled = true;
                    break;
                }
            }
            if (hasDoubled)
            {
                var buf = ArrayPool<char>.Shared.Rent(inner.Length);
                rent = buf;
                int len = 0;
                for (int qi = 0; qi < inner.Length; qi++)
                {
                    char qc = inner[qi];
                    if (qc == quote && qi + 1 < inner.Length && inner[qi + 1] == quote)
                    {
                        buf[len++] = quote;
                        qi++;
                    }
                    else
                    {
                        buf[len++] = qc;
                    }
                }
                value = new ReadOnlySpan<char>(buf, 0, len);
            }
            return new string(value);
        }
        finally
        {
            if (rent is not null)
                ArrayPool<char>.Shared.Return(rent);
        }
    }

    public static int Column(ReadOnlySpan<char> lines, ReadOnlySpan<char> value)
    {
        if (System.MemoryExtensions.Overlaps(lines, value, out int offset))
            return offset + 1;
        if (!value.IsEmpty)
            return -1;
        nint bytes = System.Runtime.CompilerServices.Unsafe.ByteOffset(
            ref System.Runtime.InteropServices.MemoryMarshal.GetReference(lines),
            ref System.Runtime.InteropServices.MemoryMarshal.GetReference(value));
        return bytes >= 0 && bytes <= (nint)lines.Length * sizeof(char) ? (int)(bytes / sizeof(char)) + 1 : -1;
    }

    public static ConvertException ConvertError(ReadOnlySpan<char> lines, ReadOnlySpan<char> value, Type type, string field, string? extra, Exception? inner) =>
        new(new string(value), type, field, -1, Column(lines, value), extra, inner);

    public static NullValueNotFoundException NullValueNotFound(ReadOnlySpan<char> lines, ReadOnlySpan<char> value, string field, string recordType) =>
        new(-1, Column(lines, value), "No value found for the value type field: '" + field + "' Class: '" + recordType + "'. You must use the [FieldNullValue] attribute because this is a value type and can't be null or use a Nullable Type instead of the current type.");

    public static ConvertException ConverterFailure(ReadOnlySpan<char> lines, ReadOnlySpan<char> value, Type type, string field, string converter, Exception ex)
    {
        if (ex is ConvertException convert)
            return ConvertError(lines, value, type, field, convert.MessageExtra, convert);
        return ConvertError(lines, value, type, field, "Your custom converter: " + converter + " throws an " + ex.GetType().Name + " with the message: " + ex.Message, ex);
    }
}
