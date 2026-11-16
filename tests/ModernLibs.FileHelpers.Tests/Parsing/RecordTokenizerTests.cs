// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests.
#pragma warning disable CA1849
// CA1859: Interface-typed variables are intentional — tests verify interface-based usage.
#pragma warning disable CA1859

using System.Globalization;
using ModernLibs.FileHelpers.Parsing;
using ModernLibs.FileHelpers.Tests.Fuzz.DiffFuzz;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// <see cref="RecordTokenizer.TokenizePlain"/>: the vector path (<c>vectors: true</c>) and the one-<c>IndexOf</c>-per-field
/// path (<c>vectors: false</c>) give the same field count and ends, and both equal the boundaries of
/// <see cref="string.Split(char)"/>, cut at the capacity of <c>ends</c>. The vector path runs whatever this machine
/// accelerates (<c>Vector256</c>, <c>Vector128</c>, or none with <c>DOTNET_EnableHWIntrinsic=0</c>).
/// </summary>
[Category("Parsing")]
public sealed class RecordTokenizerTests
{
    private static readonly int[] EdgeLengths = [0, 1, 15, 16, 17, 31, 32, 33, 63, 64, 65, 200];
    private static readonly int[] EdgeDelimiters = [15, 16, 31, 32];
    private static readonly int[] ExtraFieldEnds = [1, 3, 5];

    /// <summary>
    /// Tokenizes <paramref name="line"/> with both paths at capacity <paramref name="capacity"/>; returns
    /// <see langword="null"/> when both equal the split, otherwise a description of the first difference.
    /// </summary>
    private static string? Compare(string line, int capacity)
    {
        string[] parts = line.Split(',');
        int expectedCount = Math.Min(parts.Length, capacity);
        var expected = new int[expectedCount];
        int end = -1;
        for (int k = 0; k < expectedCount; k++)
        {
            end += parts[k].Length + 1;
            expected[k] = end;
        }

        foreach (bool vectors in new[] { true, false })
        {
            var ends = new int[capacity];
            int count = RecordTokenizer.TokenizePlain(line, ',', ends, vectors);
            if (count != expectedCount)
                return Describe(line, capacity, vectors, $"count {count}, expected {expectedCount}");
            for (int k = 0; k < count; k++)
            {
                if (ends[k] != expected[k])
                    return Describe(line, capacity, vectors, $"end of field {k} is {ends[k]}, expected {expected[k]}");
            }
        }

        return null;
    }

    private static string Describe(string line, int capacity, bool vectors, string what) =>
        string.Create(CultureInfo.InvariantCulture, $"vectors: {vectors}, capacity {capacity}, line ({line.Length} chars) '{Escape(line)}': {what}");

    private static string Escape(string line)
    {
        var text = new System.Text.StringBuilder(line.Length);
        foreach (char c in line)
        {
            if (c is < ' ' or > '~')
                text.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:X4}");
            else
                text.Append(c);
        }

        return text.ToString();
    }

    /// <summary>The capacities each line is checked at: room for every field and one more, exactly the fields, and fewer.</summary>
    private static IEnumerable<int> Capacities(string line)
    {
        int fields = line.AsSpan().Count(',') + 1;
        yield return fields + 1;
        yield return fields;
        if (fields > 1)
            yield return Math.Max(1, fields / 2);
    }

    private static string? FirstDifference(IEnumerable<string> lines)
    {
        foreach (string line in lines)
        {
            foreach (int capacity in Capacities(line))
            {
                if (Compare(line, capacity) is { } difference)
                    return difference;
            }
        }

        return null;
    }

    [Test]
    public async Task Vector_And_IndexOf_Paths_Agree_On_Generated_Input()
    {
        var generator = new LineGenerator(4915);
        var lines = new List<string>();
        for (int i = 0; i < 20_000; i++)
            lines.Add(generator.NextRecord(1 + (i % 40)));

        string repeated = string.Concat(Enumerable.Repeat("ab,", 24));
        for (int length = 0; length <= 70; length++)
            lines.Add(repeated[..length]);

        await Assert.That(FirstDifference(lines)).IsNull();
    }

    [Test]
    public async Task Vector_And_IndexOf_Paths_Agree_On_Block_Edges()
    {
        const string alphabet = "ab,ĬⰬ ";
        var random = new Random(4910);
        var lines = new List<string>();
        foreach (int length in EdgeLengths)
        {
            lines.Add(new string(',', length));
            lines.Add(new string('a', length));
            lines.Add(new string('Ĭ', length));
            lines.Add(new string('Ⱜ', length));
            for (int i = 0; i < 2_000; i++)
            {
                var chars = new char[length];
                double commaShare = (i % 4) * 0.1;
                for (int k = 0; k < length; k++)
                    chars[k] = random.NextDouble() < commaShare ? ',' : alphabet[random.Next(alphabet.Length)];
                foreach (int at in EdgeDelimiters)
                {
                    if (at < length && random.Next(2) == 0)
                        chars[at] = ',';
                }

                lines.Add(new string(chars));
            }
        }

        await Assert.That(lines.Any(l => l.Contains('Ĭ', StringComparison.Ordinal))).IsTrue();
        await Assert.That(lines.Any(l => l.Contains('Ⱜ', StringComparison.Ordinal))).IsTrue();
        await Assert.That(FirstDifference(lines)).IsNull();
    }

    [Test]
    public async Task One_Extra_Field_Is_Detectable()
    {
        foreach (bool vectors in new[] { true, false })
        {
            var ends = new int[3];
            int count = RecordTokenizer.TokenizePlain("a,b,c,d", ',', ends, vectors);
            await Assert.That(count).IsEqualTo(3);
            await Assert.That(ends).IsEquivalentTo(ExtraFieldEnds);
        }
    }

    [Test]
    public async Task Empty_Record_Has_One_Empty_Field()
    {
        foreach (bool vectors in new[] { true, false })
        {
            var ends = new int[] { -7, -7 };
            int count = RecordTokenizer.TokenizePlain(string.Empty, ',', ends, vectors);
            await Assert.That(count).IsEqualTo(1);
            await Assert.That(ends[0]).IsEqualTo(0);
        }
    }

    [Test]
    public async Task No_Room_Gives_Zero_And_Never_Throws()
    {
        foreach (bool vectors in new[] { true, false })
        {
            int shortLine = RecordTokenizer.TokenizePlain("a,b", ',', [], vectors);
            int longLine = RecordTokenizer.TokenizePlain(new string(',', 40), ',', [], vectors);
            await Assert.That(shortLine).IsEqualTo(0);
            await Assert.That(longLine).IsEqualTo(0);
        }
    }

    /// <summary>A long record with a delimiter in the last char and in the scalar tail after the last block.</summary>
    [Test]
    public async Task Delimiters_After_The_Last_Block_Are_Found()
    {
        string[] lines =
        [
            new string('a', 2_047) + ",",
            new string('a', 2_040) + ",bc,de,",
            "," + new string('a', 2_000) + ",x",
        ];
        await Assert.That(FirstDifference(lines)).IsNull();
    }
}
