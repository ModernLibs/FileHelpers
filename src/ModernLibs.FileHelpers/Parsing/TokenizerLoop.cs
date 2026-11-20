namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// The loop <see cref="RecordTokenizer"/> finds delimiters (and quotes) with (T21; internal, may change in any patch).
/// <see cref="RecordTokenizer.PlainLoop"/> and <see cref="RecordTokenizer.QuotedLoop"/> hold the default; the other
/// values exist so that the benchmarks and tests run each loop of the shipped code.
/// </summary>
internal enum TokenizerLoop
{
    /// <summary>
    /// None of the tokenizer's own vector code: one <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, T)"/> per
    /// field, or one <c>IndexOfAny</c> per delimiter or quote position, which the BCL may vectorize (freeze F-7, F-9).
    /// </summary>
    IndexOfHop,

    /// <summary>Blocks of 8 chars (<c>Vector128</c>), then a scalar tail.</summary>
    Vector128,

    /// <summary>Blocks of 16 chars (<c>Vector256</c>), then <see cref="Vector128"/> blocks and a scalar tail.</summary>
    Vector256,

    /// <summary>
    /// Blocks of 32 chars (<c>Vector512</c>), then as <see cref="Vector256"/>. Plain records only: the quoted walk runs
    /// <see cref="Vector256"/> blocks for it.
    /// </summary>
    Vector512,

    /// <summary>
    /// <see cref="Vector256"/> with a pre-test per 32 chars (design 2.5 item 16): two <c>Vector256</c> loads are
    /// narrowed to one vector of their low bytes and compared with the delimiter's low byte; only a block with a
    /// possible match pays for the two exact 16-bit compares. Plain records only: the quoted walk runs
    /// <see cref="Vector256"/> blocks for it.
    /// </summary>
    Vector256Packed,
}
