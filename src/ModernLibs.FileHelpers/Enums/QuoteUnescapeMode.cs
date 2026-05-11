namespace ModernLibs.FileHelpers;

/// <summary>
/// Specifies how quoted fields are unescaped during parsing.
/// Inspired by Sep's Unescape option for RFC 4180 quote handling.
/// </summary>
public enum QuoteUnescapeMode
{
    /// <summary>
    /// No unescaping — quotes are preserved as-is.
    /// Fastest path, zero allocation for quoted fields.
    /// </summary>
    None = 0,

    /// <summary>
    /// Standard RFC 4180 unescaping: outer quotes removed, "" → ".
    /// Allocates a new string only when escape sequences are present.
    /// </summary>
    Standard = 1,

    /// <summary>
    /// Lenient unescaping: same as Standard but tolerates malformed input
    /// (unclosed quotes, quotes in wrong positions) without throwing.
    /// </summary>
    Lenient = 2,
}
