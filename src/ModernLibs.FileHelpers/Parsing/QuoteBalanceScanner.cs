// Phase 18 MULT-07: Quote balance state machine for multiline quoted field detection
// Scans a span and determines if quotes are balanced (closed) or unbalanced (open)

// MA0048: QuoteBalanceState and QuoteBalance are internal helper types in the same file
// to reduce file count - intentional grouping by functional area.
#pragma warning disable MA0048

namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// Internal state for quote balance tracking.
/// </summary>
internal enum QuoteBalanceState
{
    /// <summary>Not inside any quoted field.</summary>
    OutsideQuotes,

    /// <summary>Inside a quoted field, seeking closing quote.</summary>
    InsideQuotes,
}

/// <summary>
/// Result of a quote balance scan.
/// </summary>
internal readonly record struct QuoteBalance(bool IsBalanced, int NetOpenQuotes)
{
    /// <summary>Returns true if currently inside an unclosed quoted field.</summary>
    public bool IsInsideQuotes => NetOpenQuotes > 0;
}

/// <summary>
/// Ref struct scanner that determines quote balance in a line.
/// Implements RFC 4180 quote handling with "" escape sequence support.
/// MULT-07: Core state machine for multiline quoted field detection.
/// </summary>
internal ref struct QuoteBalanceScanner
{
    /// <summary>
    /// Scans the span and returns quote balance information.
    /// </summary>
    /// <param name="span">The line to scan.</param>
    /// <returns>QuoteBalance with IsBalanced and NetOpenQuotes.</returns>
    public static QuoteBalance Scan(ReadOnlySpan<char> span)
    {
        int netOpenQuotes = 0;
        bool inQuotes = false;

        for (int i = 0; i < span.Length; i++)
        {
            char c = span[i];

            if (c == '"')
            {
                if (inQuotes)
                {
                    // Check for "" escape sequence
                    if (i + 1 < span.Length && span[i + 1] == '"')
                    {
                        // Skip the second quote of the pair
                        i++;
                        continue;
                    }

                    // Closing quote found
                    inQuotes = false;
                    netOpenQuotes--;
                }
                else
                {
                    // Opening quote found
                    inQuotes = true;
                    netOpenQuotes++;
                }
            }
        }

        return new QuoteBalance(netOpenQuotes == 0, netOpenQuotes);
    }

    /// <summary>
    /// Returns true if the span has balanced quotes (all opening quotes have matching closing quotes).
    /// </summary>
    public static bool IsBalanced(ReadOnlySpan<char> span)
    {
        return Scan(span).IsBalanced;
    }

    /// <summary>
    /// Returns the net count of unclosed quotes (0 = balanced, 1 = inside single quote, etc.).
    /// </summary>
    public static int CountOpenQuotes(ReadOnlySpan<char> span)
    {
        return Scan(span).NetOpenQuotes;
    }

    /// <summary>
    /// Returns true if the span ends inside an unclosed quoted field.
    /// This indicates a multiline field that needs additional lines to complete.
    /// </summary>
    public static bool HasUnclosedQuote(ReadOnlySpan<char> span)
    {
        return Scan(span).IsInsideQuotes;
    }
}
