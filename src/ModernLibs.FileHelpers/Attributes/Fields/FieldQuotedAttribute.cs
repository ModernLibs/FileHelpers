namespace ModernLibs.FileHelpers;

/// <summary>
/// Configures quoted-field handling for a delimited record field.
/// The engine respects the quote character and mode when parsing and formatting this field.
/// Multiline quoted fields (ADVP-01) are deferred to v2.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FieldQuotedAttribute : Attribute
{
    /// <summary>The character used to quote the field value. Default is <c>'"'</c>.</summary>
    public char QuoteChar { get; }

    /// <summary>Controls when quotes are required during read and write operations.</summary>
    public QuoteMode QuoteMode { get; }

    /// <summary>Uses double-quote (<c>"</c>) with <see cref="QuoteMode.OptionalForRead"/> mode.</summary>
    public FieldQuotedAttribute() : this('"') { }

    /// <summary>Uses the specified quote character with <see cref="QuoteMode.OptionalForRead"/> mode.</summary>
    /// <param name="quoteChar">The quote character.</param>
    public FieldQuotedAttribute(char quoteChar) : this(quoteChar, QuoteMode.OptionalForRead) { }

    /// <summary>Uses double-quote (<c>"</c>) with the specified quote mode.</summary>
    /// <param name="mode">The quote mode.</param>
    public FieldQuotedAttribute(QuoteMode mode) : this('"', mode) { }

    /// <summary>Uses the specified quote character and mode.</summary>
    /// <param name="quoteChar">The quote character.</param>
    /// <param name="mode">The quote mode.</param>
    public FieldQuotedAttribute(char quoteChar, QuoteMode mode)
    {
        QuoteChar = quoteChar;
        QuoteMode = mode;
    }
}
