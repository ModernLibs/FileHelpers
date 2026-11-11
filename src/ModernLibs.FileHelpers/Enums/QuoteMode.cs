namespace ModernLibs.FileHelpers;

/// <summary>Indicates how quoted fields are handled during read and write operations.</summary>
public enum QuoteMode
{
    /// <summary>Field must always be quoted on read; always written with quotes.</summary>
    AlwaysQuoted = 0,

    /// <summary>Field may be unquoted on read; always written with quotes.</summary>
    OptionalForRead = 1,

    /// <summary>
    /// Field must be quoted on read; quotes are added on write only when needed.
    /// A value written without quotes is therefore rejected on read: this mode cannot read back its own
    /// unquoted output (as in FileHelpers 3.x). Use <see cref="OptionalForBoth"/> for a round trip.
    /// </summary>
    OptionalForWrite = 2,

    /// <summary>Field may be unquoted on read; quotes added on write only when needed.</summary>
    OptionalForBoth = 3,
}
