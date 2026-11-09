namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// The shortcuts <see cref="QuoteBalanceScanner"/> may take for one set of quote rules, decided once per record
/// type by <see cref="QuoteBalanceScanner.Plan"/>.
/// </summary>
/// <param name="Quote">
/// The quote character of every quoted field when they all use the same one and no field has its own
/// delimiter; otherwise <c>'\0'</c> (no shortcut). A line without this character has no quoted value.
/// </param>
/// <param name="Vectorized">
/// The line can be scanned by a single pass over the positions of the delimiter and quote characters:
/// <see cref="Quote"/> is set, the delimiter is one character other than the quote, and no quoted field skips
/// trim characters before its opening quote.
/// </param>
internal readonly record struct QuoteScanPlan(char Quote, bool Vectorized);
