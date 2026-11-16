using System.Runtime.ExceptionServices;

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
        int iterations, Func<string> next, Func<string, string> readA, Func<string, string> readB, string? baselineInput = null) =>
        Run(iterations, next, readA, readB, readC: null, baselineInput);

    /// <summary>
    /// <see cref="Run(int, Func{string}, Func{string, string}, Func{string, string}, string?)"/> with a third read:
    /// each input is read once by each function, and <paramref name="readC"/> must give the outcome of
    /// <paramref name="readB"/> as well (B is the reference). A difference of C is reported with C in the place of A.
    /// </summary>
    public static Result Run(
        int iterations, Func<string> next, Func<string, string> readA, Func<string, string> readB, Func<string, string>? readC, string? baselineInput)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(iterations);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(readA);
        ArgumentNullException.ThrowIfNull(readB);

        (int baseRecords, int baseErrors) = baselineInput is null ? (0, 0) : Count(readA(baselineInput));
        int withRecord = 0, withError = 0;
        string lastA = string.Empty;
        string ReadA(string input) => lastA = readA(input);

        // B's outcome, replayed for the comparison with C, so that B reads each input once.
        string? lastB = null;
        ExceptionDispatchInfo? errorB = null;
        string ReadB(string input)
        {
            (lastB, errorB) = (null, null);
            try
            {
                return lastB = readB(input);
            }
            catch (Exception ex)
            {
                errorB = ExceptionDispatchInfo.Capture(ex);
                throw;
            }
        }

        string ReplayB(string _)
        {
            errorB?.Throw();
            return lastB!;
        }

        for (int i = 0; i < iterations; i++)
        {
            lastA = string.Empty;                                 // stays empty when the A read throws
            string input = next();
            string? difference = ReadComparer.Compare(input, ReadA, ReadB);
            if (difference is null && readC is not null && ReadComparer.Compare(input, readC, ReplayB) is { } differenceC)
                difference = "The third read (C) differs from B; below, A is C.\n" + differenceC;
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

    /// <summary>
    /// The shape-aware loop: <paramref name="iterations"/> inputs of <see cref="ShapeInput"/> for the shape
    /// <paramref name="name"/> from <paramref name="seed"/>, counted against <see cref="ShapeInput.Baseline"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a shape of the matrix.</exception>
    public static Result RunShape(
        string name, int iterations, int seed, Func<string, string> readA, Func<string, string> readB, Func<string, string>? readC = null)
    {
        var generator = ShapeInput.CreateGenerator(name, seed);
        return Run(iterations, () => ShapeInput.Next(name, generator), readA, readB, readC, ShapeInput.Baseline(name));
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
