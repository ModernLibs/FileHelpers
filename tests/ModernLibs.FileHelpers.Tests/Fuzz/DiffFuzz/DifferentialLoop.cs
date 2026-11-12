namespace ModernLibs.FileHelpers.Tests.Fuzz.DiffFuzz;

/// <summary>
/// The differential loop shared by <c>DifferentialFuzzTests</c> and the <c>differential</c> target of
/// <c>ModernLibs.FileHelpers.Fuzz.Tests</c> (a linked source file): read each input with both functions, stop at the
/// first difference, and count how far the input reached.
/// </summary>
internal static class DifferentialLoop
{
    /// <summary>The outcome of <see cref="Run"/>.</summary>
    /// <param name="Inputs">The inputs read: all of them, or up to and including the first difference.</param>
    /// <param name="Difference">The <see cref="ReadComparer"/> message of the first difference, or <see langword="null"/>.</param>
    /// <param name="WithRecord">Inputs whose A read had more <c>record:</c> lines than the baseline.</param>
    /// <param name="WithError">Inputs whose A read had more <c>error:</c> lines than the baseline.</param>
    internal readonly record struct Result(int Inputs, string? Difference, int WithRecord, int WithError);

    /// <summary>
    /// Reads <paramref name="iterations"/> inputs of <paramref name="next"/> with <paramref name="readA"/> and
    /// <paramref name="readB"/> and stops at the first difference.
    /// </summary>
    /// <param name="baselineInput">
    /// An input whose A read gives the counts that an input must exceed to count as "with a record" or "with an
    /// error" (the valid rows around a fuzzed row); <see langword="null"/> for zero.
    /// </param>
    /// <remarks>
    /// The counts look at the lines <c>record:</c> and <c>error:</c> of the canonical text of
    /// <c>Compat49Matrix</c>: those of the array read with <c>ErrorMode.SaveAndContinue</c>. The
    /// <c>recycled-</c> and <c>thrown-</c> lines are compared, not counted.
    /// </remarks>
    public static Result Run(
        int iterations, Func<string> next, Func<string, string> readA, Func<string, string> readB, string? baselineInput = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(iterations);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(readA);
        ArgumentNullException.ThrowIfNull(readB);

        (int baseRecords, int baseErrors) = baselineInput is null ? (0, 0) : Count(readA(baselineInput));
        int withRecord = 0, withError = 0;
        string lastA = string.Empty;
        string ReadA(string input) => lastA = readA(input);

        for (int i = 0; i < iterations; i++)
        {
            lastA = string.Empty;                                 // stays empty when the A read throws
            string? difference = ReadComparer.Compare(next(), ReadA, readB);
            if (difference is not null)
                return new Result(i + 1, difference, withRecord, withError);

            (int records, int errors) = Count(lastA);
            if (records > baseRecords)
                withRecord++;
            if (errors > baseErrors)
                withError++;
        }

        return new Result(iterations, null, withRecord, withError);
    }

    /// <summary>The <c>record:</c> and <c>error:</c> lines of a canonical text (values in it have escaped line breaks).</summary>
    public static (int Records, int Errors) Count(string text)
    {
        int records = 0, errors = 0;
        foreach (ReadOnlySpan<char> line in text.AsSpan().EnumerateLines())
        {
            if (line.StartsWith("record:", StringComparison.Ordinal))
                records++;
            else if (line.StartsWith("error:", StringComparison.Ordinal))
                errors++;
        }

        return (records, errors);
    }
}
