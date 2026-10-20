namespace ModernLibs.FileHelpers;

/// <summary>
/// Configures quoted-field handling for a delimited record field, as in FileHelpers 3.x.
/// </summary>
/// <remarks>
/// <para>Read: a value that starts with the quote character (after blanks only when the field's
/// <see cref="FieldTrimAttribute"/> trims the start) is unquoted and doubled quotes are unescaped; the closing
/// quote must be followed by the delimiter (blanks in between are allowed when the field trims the end).
/// <see cref="ModernLibs.FileHelpers.QuoteMode.AlwaysQuoted"/> and
/// <see cref="ModernLibs.FileHelpers.QuoteMode.OptionalForWrite"/> reject values that are not quoted.</para>
/// <para>Write: <see cref="ModernLibs.FileHelpers.QuoteMode.AlwaysQuoted"/> and
/// <see cref="ModernLibs.FileHelpers.QuoteMode.OptionalForRead"/> always quote; the other modes quote only
/// values that contain the delimiter or a line break.</para>
/// <para>Quoted values may span several lines according to <see cref="QuoteMultiline"/> (default
/// <see cref="MultilineMode.AllowForBoth"/>). <see cref="DelimitedRecordAttribute.MultilineMode"/>, when set,
/// overrides it for every quoted field of the record.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldQuotedAttribute : Attribute
{
    /// <summary>The character used to quote the field value. Default is <c>'"'</c>.</summary>
    public char QuoteChar { get; }

    /// <summary>Controls when quotes are required during read and write operations.</summary>
    public QuoteMode QuoteMode { get; }

    /// <summary>Whether the quoted value may contain line breaks on read and/or write. Default: <see cref="MultilineMode.AllowForBoth"/>.</summary>
    public MultilineMode QuoteMultiline { get; }

    /// <summary>Uses double-quote (<c>"</c>) with <see cref="QuoteMode.OptionalForRead"/> mode.</summary>
    public FieldQuotedAttribute() : this('"') { }

    /// <summary>Uses the specified quote character with <see cref="QuoteMode.OptionalForRead"/> mode.</summary>
    /// <param name="quoteChar">The quote character.</param>
    public FieldQuotedAttribute(char quoteChar) : this(quoteChar, QuoteMode.OptionalForRead) { }

    /// <summary>Uses double-quote (<c>"</c>) with the specified quote mode.</summary>
    /// <param name="mode">The quote mode.</param>
    public FieldQuotedAttribute(QuoteMode mode) : this('"', mode) { }

    /// <summary>Uses double-quote (<c>"</c>) with the specified quote and multiline modes.</summary>
    /// <param name="mode">The quote mode.</param>
    /// <param name="multiline">Whether the value may span several lines.</param>
    public FieldQuotedAttribute(QuoteMode mode, MultilineMode multiline) : this('"', mode, multiline) { }

    /// <summary>Uses the specified quote character and mode.</summary>
    /// <param name="quoteChar">The quote character.</param>
    /// <param name="mode">The quote mode.</param>
    public FieldQuotedAttribute(char quoteChar, QuoteMode mode) : this(quoteChar, mode, MultilineMode.AllowForBoth) { }

    /// <summary>Uses the specified quote character, quote mode and multiline mode.</summary>
    /// <param name="quoteChar">The quote character.</param>
    /// <param name="mode">The quote mode.</param>
    /// <param name="multiline">Whether the value may span several lines.</param>
    public FieldQuotedAttribute(char quoteChar, QuoteMode mode, MultilineMode multiline)
    {
        QuoteChar = quoteChar;
        QuoteMode = mode;
        QuoteMultiline = multiline;
    }

    /// <summary>Uses double-quote (<c>"</c>) with <see cref="QuoteMode.OptionalForRead"/> and the specified multiline mode.</summary>
    /// <param name="multiline">Whether the value may span several lines.</param>
    public FieldQuotedAttribute(MultilineMode multiline) : this('"', QuoteMode.OptionalForRead, multiline) { }
}
