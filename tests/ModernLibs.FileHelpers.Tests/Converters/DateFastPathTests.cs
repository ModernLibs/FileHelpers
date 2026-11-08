// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using System.Globalization;
using ModernLibs.FileHelpers.Converters;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Converters;

/// <summary>
/// The date converters' fast path for fixed-width formats must give exactly the result of
/// <c>DateTime.TryParseExact(trimmed, format, CultureInfo.InvariantCulture, DateTimeStyles.None)</c>:
/// same success or failure, same value, same <see cref="DateTimeKind"/>.
/// </summary>
[Category("Converters")]
public sealed class DateFastPathTests
{
    /// <summary>Formats with a fast-path plan.</summary>
    private static readonly string[] PlannedFormats =
    [
        "ddMMyyyy", "s", "yyyyMMdd", "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "dd/MM/yyyy", "MM/dd/yyyy",
        "yyyy-MM-ddTHH:mm:ss", "dd.MM.yyyy", "yyyyMMddHHmmss", "dd-MM-yyyy HH:mm", "yyyy_MM_dd",
    ];

    /// <summary>Formats the fast path must leave to <c>TryParseExact</c>.</summary>
    private static readonly string[] UnplannedFormats =
    [
        "d", "o", "yyyy-MM-dd'T'HH:mm:ss", "dd MMM yyyy", "d/M/yyyy", "yy-MM-dd", "HH:mm:ss", "yyyy-MM",
        "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-ddTHH:mm:ssK", "dd\\.MM\\.yyyy", "yyyyMMdd tt", "yyyyyMMdd",
        "yyyy-MM-dd HH:mm:ss yyyy",
    ];

    /// <summary>Text around a valid value: blanks, signs, zones, fractions, other digits and letters.</summary>
    private static readonly string[] Decorations =
    [
        "", " ", "  ", "\t", " ", "Z", ".0", "+", "-", "٢", "a", "0", "T", ":", "/", "  x",
    ];

    /// <summary>Characters used to mutate a valid value.</summary>
    private const string MutationChars = "0123456789-/:. T_Zz+٢５aA\t";

    private static (bool Ok, DateTime Value) Reference(string input, string format)
    {
        bool ok = DateTime.TryParseExact(input.AsSpan().Trim(), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value);
        return (ok, value);
    }

    private static IEnumerable<string> Inputs(string format, int seed, int mutations)
    {
        var random = new Random(seed);
        var edges = new[]
        {
            new DateTime(2024, 2, 29, 23, 59, 59), new DateTime(1900, 2, 28), new DateTime(2000, 2, 29),
            new DateTime(2100, 2, 28, 12, 0, 0), new DateTime(1, 1, 1), new DateTime(9999, 12, 31, 23, 59, 59),
            new DateTime(2026, 12, 25, 0, 0, 0), new DateTime(1970, 1, 1, 1, 2, 3),
        };

        var valid = new List<string>();
        foreach (var edge in edges)
            valid.Add(edge.ToString(format, CultureInfo.InvariantCulture));
        for (int i = 0; i < 2_000; i++)
        {
            var value = new DateTime(random.Next(1, 10_000), random.Next(1, 13), 1, random.Next(24), random.Next(60), random.Next(60));
            value = value.AddDays(random.Next(DateTime.DaysInMonth(value.Year, value.Month)));
            valid.Add(value.ToString(format, CultureInfo.InvariantCulture));
        }

        foreach (string text in valid)
            yield return text;

        // Impossible dates and times written in the same shape: day 0, 30 February, month 13, hour 24, second 60.
        string sample = valid[0];
        for (int i = 0; i < sample.Length; i++)
        {
            if (!char.IsAsciiDigit(sample[i]))
                continue;
            for (char digit = '0'; digit <= '9'; digit++)
                yield return string.Concat(sample.AsSpan(0, i), digit.ToString(), sample.AsSpan(i + 1));
        }

        foreach (string decoration in Decorations)
        {
            yield return decoration + sample;
            yield return sample + decoration;
            yield return sample.Insert(sample.Length / 2, decoration);
        }

        for (int i = 0; i < mutations; i++)
        {
            char[] chars = valid[random.Next(valid.Count)].ToCharArray();
            int position = random.Next(chars.Length);
            switch (random.Next(3))
            {
                case 0:
                    chars[position] = MutationChars[random.Next(MutationChars.Length)];
                    yield return new string(chars);
                    break;
                case 1:
                    yield return new string(chars).Remove(position, 1);
                    break;
                default:
                    yield return new string(chars).Insert(position, MutationChars[random.Next(MutationChars.Length)].ToString());
                    break;
            }
        }
    }

    private static async Task AssertMatchesTryParseExact(DateTimeConverter converter, string format, int seed)
    {
        int failures = 0;
        string? firstFailure = null;
        foreach (string input in Inputs(format, seed, mutations: 20_000))
        {
            var expected = Reference(input, format);
            bool ok = converter.TryParse(input, out var actual);
            if (ok != expected.Ok || (ok && (actual != expected.Value || actual.Kind != expected.Value.Kind)))
            {
                failures++;
                firstFailure ??= $"'{input}': expected ({expected.Ok}, {expected.Value:o}), got ({ok}, {actual:o})";
            }
        }

        await Assert.That(failures).IsEqualTo(0).Because($"format '{format}', first difference {firstFailure}");
    }

    [Test]
    [DisplayName("Every fixed-width format gets a fast-path plan; formats with other parts do not")]
    public async Task Plans_AreCreatedOnlyForFixedWidthFormats()
    {
        foreach (string format in PlannedFormats)
            await Assert.That(FixedDateFormat.TryCreate(format, CultureInfo.InvariantCulture)).IsNotNull().Because(format);
        foreach (string format in UnplannedFormats)
            await Assert.That(FixedDateFormat.TryCreate(format, CultureInfo.InvariantCulture)).IsNull().Because(format);

        // Another culture could map '/' and ':' to other separators: no plan, even when it is invariant-like.
        await Assert.That(FixedDateFormat.TryCreate("dd/MM/yyyy", CultureInfo.GetCultureInfo("en-US"))).IsNull();
        await Assert.That(FixedDateFormat.TryCreate("dd/MM/yyyy", CultureInfo.InvariantCulture.DateTimeFormat)).IsNull();
    }

    [Test]
    [DisplayName("A fast-path plan accepts only values TryParseExact accepts, with the same value")]
    public async Task Plans_AcceptOnlyWhatTryParseExactAccepts()
    {
        int seed = 1;
        foreach (string format in PlannedFormats)
        {
            var plan = FixedDateFormat.TryCreate(format, CultureInfo.InvariantCulture)!;
            int accepted = 0, wrong = 0;
            string? firstWrong = null;
            foreach (string input in Inputs(format, seed++, mutations: 20_000))
            {
                if (!plan.TryParse(input.AsSpan().Trim(), out var actual))
                    continue;
                accepted++;
                var expected = Reference(input, format);
                if (!expected.Ok || actual != expected.Value || actual.Kind != expected.Value.Kind)
                {
                    wrong++;
                    firstWrong ??= $"'{input}': TryParseExact ({expected.Ok}, {expected.Value:o}), plan {actual:o}";
                }
            }

            await Assert.That(wrong).IsEqualTo(0).Because($"format '{format}', first difference {firstWrong}");
            await Assert.That(accepted).IsGreaterThan(2_000).Because($"format '{format}': the plan must take the valid values");
        }
    }

    [Test]
    [DisplayName("DateTimeConverter with an explicit format matches TryParseExact, planned or not")]
    public async Task DateTimeConverter_MatchesTryParseExact()
    {
        int seed = 100;
        foreach (string format in PlannedFormats.Concat(UnplannedFormats).Where(f => f is not ("d" or "o")))
            await AssertMatchesTryParseExact(new DateTimeConverter(format), format, seed++);
    }

    [Test]
    [DisplayName("DateMultiFormatConverter matches TryParseExact over its formats in order")]
    public async Task DateMultiFormatConverter_MatchesTryParseExactInOrder()
    {
        // Arrange: a planned format, an unplanned one, then a planned one that could also match
        string[] formats = ["yyyyMMdd", "dd MMM yyyy", "ddMMyyyy", "yyyy-MM-dd HH:mm:ss"];
        var converter = new DateMultiFormatConverter(formats);
        var inputs = formats.SelectMany((format, i) => Inputs(format, 200 + i, mutations: 5_000));

        // Act / Assert: the first format TryParseExact accepts decides the value
        int failures = 0;
        string? firstFailure = null;
        foreach (string input in inputs)
        {
            (bool Ok, DateTime Value) expected = (false, default);
            foreach (string format in formats)
            {
                expected = Reference(input, format);
                if (expected.Ok)
                    break;
            }

            bool ok = converter.TryParse(input, out var actual);
            if (ok != expected.Ok || (ok && actual != expected.Value))
            {
                failures++;
                firstFailure ??= $"'{input}': expected ({expected.Ok}, {expected.Value:o}), got ({ok}, {actual:o})";
            }
        }

        await Assert.That(failures).IsEqualTo(0).Because($"first difference {firstFailure}");
    }

    [Test]
    [DisplayName("DateTimeConverter with a culture other than the invariant one matches TryParseExact with that culture")]
    public async Task DateTimeConverter_OtherCulture_UsesTheCulture()
    {
        // Arrange: Polish uses '.' as its date separator, so "dd/MM/yyyy" means dd.MM.yyyy there
        var culture = CultureInfo.GetCultureInfo("pl-PL");
        var converter = new DateTimeConverter("dd/MM/yyyy", culture);

        // Act
        bool slash = converter.TryParse("25/12/2026", out var fromSlash);
        bool expectedSlash = DateTime.TryParseExact("25/12/2026", "dd/MM/yyyy", culture, DateTimeStyles.None, out var expectedFromSlash);
        bool dot = converter.TryParse("25.12.2026", out var fromDot);
        bool expectedDot = DateTime.TryParseExact("25.12.2026", "dd/MM/yyyy", culture, DateTimeStyles.None, out var expectedFromDot);

        // Assert
        await Assert.That((slash, fromSlash)).IsEqualTo((expectedSlash, expectedFromSlash));
        await Assert.That((dot, fromDot)).IsEqualTo((expectedDot, expectedFromDot));
    }

    [Test]
    [NotInParallel]
    [DisplayName("The default DateTimeConverter follows DefaultDateTimeFormat as it switches between planned and unplanned formats")]
    public async Task DefaultConverter_FollowsDefaultDateTimeFormat()
    {
        // Arrange
        string previous = ConverterBase.DefaultDateTimeFormat;
        var converter = DateTimeConverter.Instance;
        try
        {
            // Act / Assert: each switch is seen by the next parse
            await Assert.That(converter.TryParse("25122026", out var legacy)).IsTrue();
            await Assert.That(legacy).IsEqualTo(new DateTime(2026, 12, 25));

            ConverterBase.DefaultDateTimeFormat = "dd MMM yyyy";
            await Assert.That(converter.TryParse("25 Dec 2026", out var named)).IsTrue();
            await Assert.That(named).IsEqualTo(new DateTime(2026, 12, 25));
            await Assert.That(converter.TryParse("25122026", out _)).IsFalse();

            ConverterBase.DefaultDateTimeFormat = "yyyy-MM-dd";
            await Assert.That(converter.TryParse(" 2026-12-25 ", out var iso)).IsTrue();
            await Assert.That(iso).IsEqualTo(new DateTime(2026, 12, 25));
            await Assert.That(converter.TryParse("25 Dec 2026", out _)).IsFalse();
        }
        finally
        {
            ConverterBase.DefaultDateTimeFormat = previous;
        }
    }
}
