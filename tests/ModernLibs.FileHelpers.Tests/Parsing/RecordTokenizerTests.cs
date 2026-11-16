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
using System.Runtime.Intrinsics;
using ModernLibs.FileHelpers.Parsing;
using ModernLibs.FileHelpers.Tests.Fuzz.DiffFuzz;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Parsing;

/// <summary>
/// <see cref="RecordTokenizer.TokenizePlain(ReadOnlySpan{char}, char, Span{int}, bool)"/>: the vector path
/// (<c>vectors: true</c>) and the one-<c>IndexOf</c>-per-field path (<c>vectors: false</c>) give the same field count
/// and ends, and both equal the boundaries of <see cref="string.Split(char)"/>, cut at the capacity of <c>ends</c>. The
/// vector path runs <see cref="RecordTokenizer.PlainLoop"/>, whatever this machine accelerates (<c>Vector256</c>,
/// <c>Vector128</c>, or none with <c>DOTNET_EnableHWIntrinsic=0</c>). The loop overload (T21) runs every
/// <see cref="TokenizerLoop"/> and load form on the same records.
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

    /// <summary>The input of <see cref="Vector_And_IndexOf_Paths_Agree_On_Generated_Input"/>.</summary>
    private static List<string> GeneratedLines()
    {
        var generator = new LineGenerator(4915);
        var lines = new List<string>();
        for (int i = 0; i < 20_000; i++)
            lines.Add(generator.NextRecord(1 + (i % 40)));

        string repeated = string.Concat(Enumerable.Repeat("ab,", 24));
        for (int length = 0; length <= 70; length++)
            lines.Add(repeated[..length]);

        return lines;
    }

    /// <summary>The input of <see cref="Vector_And_IndexOf_Paths_Agree_On_Block_Edges"/>.</summary>
    private static List<string> BlockEdgeLines()
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

        return lines;
    }

    /// <summary>The long records of <see cref="Delimiters_After_The_Last_Block_Are_Found"/>.</summary>
    private static string[] TailLines() =>
    [
        new string('a', 2_047) + ",",
        new string('a', 2_040) + ",bc,de,",
        "," + new string('a', 2_000) + ",x",
    ];

    [Test]
    public async Task Vector_And_IndexOf_Paths_Agree_On_Generated_Input()
    {
        await Assert.That(FirstDifference(GeneratedLines())).IsNull();
    }

    [Test]
    public async Task Vector_And_IndexOf_Paths_Agree_On_Block_Edges()
    {
        var lines = BlockEdgeLines();
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
        await Assert.That(FirstDifference(TailLines())).IsNull();
    }

    /// <summary>
    /// Whether this machine accelerates the vector width <paramref name="loop"/> needs (T21: a loop the machine does
    /// not accelerate gives the same result in software, but is not what the machine would run).
    /// </summary>
    internal static bool Accelerated(TokenizerLoop loop) => loop switch
    {
        TokenizerLoop.IndexOfHop => true,
        TokenizerLoop.Vector128 => Vector128.IsHardwareAccelerated,
        TokenizerLoop.Vector512 => Vector512.IsHardwareAccelerated,
        _ => Vector256.IsHardwareAccelerated,
    };

    /// <summary>
    /// Tokenizes <paramref name="line"/> at capacity <paramref name="capacity"/> with <paramref name="loop"/> and with
    /// <see cref="TokenizerLoop.IndexOfHop"/>; <see langword="null"/> when they agree, otherwise the first difference.
    /// </summary>
    private static string? CompareLoop(string line, int capacity, TokenizerLoop loop, bool unsafeLoad)
    {
        var expected = new int[capacity];
        int expectedCount = RecordTokenizer.TokenizePlain(line, ',', expected, TokenizerLoop.IndexOfHop, unsafeLoad: false);
        var ends = new int[capacity];
        int count = RecordTokenizer.TokenizePlain(line, ',', ends, loop, unsafeLoad);
        string form = string.Create(CultureInfo.InvariantCulture, $"{loop}, unsafeLoad: {unsafeLoad}, capacity {capacity}, line ({line.Length} chars) '{Escape(line)}'");
        if (count != expectedCount)
            return $"{form}: count {count}, IndexOfHop {expectedCount}";
        for (int k = 0; k < count; k++)
        {
            if (ends[k] != expected[k])
                return string.Create(CultureInfo.InvariantCulture, $"{form}: end of field {k} is {ends[k]}, IndexOfHop {expected[k]}");
        }

        return null;
    }

    private static string? FirstLoopDifference(IEnumerable<string> lines, TokenizerLoop loop, bool unsafeLoad)
    {
        foreach (string line in lines)
        {
            foreach (int capacity in Capacities(line))
            {
                if (CompareLoop(line, capacity, loop, unsafeLoad) is { } difference)
                    return difference;
            }
        }

        return null;
    }

    /// <summary>
    /// T21 step 1: every loop and load form of the shipped code gives the entries of <see cref="TokenizerLoop.IndexOfHop"/>
    /// on the generated-input, block-edge and tail records of the T15 tests above, at the same capacities. A loop
    /// whose vector width this machine does not accelerate is skipped with a message.
    /// </summary>
    [Test]
    [Arguments(nameof(TokenizerLoop.IndexOfHop), false)]
    [Arguments(nameof(TokenizerLoop.Vector128), false)]
    [Arguments(nameof(TokenizerLoop.Vector128), true)]
    [Arguments(nameof(TokenizerLoop.Vector256), false)]
    [Arguments(nameof(TokenizerLoop.Vector256), true)]
    [Arguments(nameof(TokenizerLoop.Vector512), false)]
    [Arguments(nameof(TokenizerLoop.Vector512), true)]
    [Arguments(nameof(TokenizerLoop.Vector256Packed), false)]
    [Arguments(nameof(TokenizerLoop.Vector256Packed), true)]
    public async Task Every_Loop_And_Load_Form_Agrees_With_IndexOfHop(string loopName, bool unsafeLoad)
    {
        var loop = Enum.Parse<TokenizerLoop>(loopName);
        Skip.Unless(Accelerated(loop), $"{loop}: this machine does not accelerate its vector width");

        await Assert.That(FirstLoopDifference(GeneratedLines(), loop, unsafeLoad)).IsNull();
        await Assert.That(FirstLoopDifference(BlockEdgeLines(), loop, unsafeLoad)).IsNull();
        await Assert.That(FirstLoopDifference(TailLines(), loop, unsafeLoad)).IsNull();
    }

    /// <summary>Every value of <see cref="TokenizerLoop"/> has a case in <see cref="Every_Loop_And_Load_Form_Agrees_With_IndexOfHop"/>.</summary>
    [Test]
    public async Task Every_Loop_Has_A_Case()
    {
        string[] cases =
        [
            nameof(TokenizerLoop.IndexOfHop), nameof(TokenizerLoop.Vector128), nameof(TokenizerLoop.Vector256),
            nameof(TokenizerLoop.Vector512), nameof(TokenizerLoop.Vector256Packed),
        ];
        await Assert.That(Enum.GetNames<TokenizerLoop>()).IsEquivalentTo(cases);
    }

    /// <summary>
    /// T21 step 1, the packed pre-test of <see cref="TokenizerLoop.Vector256Packed"/>: U+012C and U+2C2C have the low
    /// byte of ',' (0x2C), so they pass the pre-test; the exact compare must drop them. Each record has 2 fields, also
    /// with the pair placed in the first or second vector of a 32-char block, or across two blocks.
    /// </summary>
    [Test]
    public async Task Packed_Variant_Handles_Low_Byte_Matches()
    {
        foreach (string record in new[] { "aĬb,c", "aⰬb,c" })
        {
            foreach (int pad in new[] { 0, 1, 14, 15, 16, 29, 30, 31, 32, 60 })
            {
                foreach (int tail in new[] { 0, 64 })
                {
                    string line = new string('x', pad) + record + new string('y', tail);
                    foreach (bool unsafeLoad in new[] { false, true })
                    {
                        var ends = new int[3];
                        int count = RecordTokenizer.TokenizePlain(line, ',', ends, TokenizerLoop.Vector256Packed, unsafeLoad);
                        string because = string.Create(CultureInfo.InvariantCulture, $"'{Escape(line)}', unsafeLoad: {unsafeLoad}");
                        await Assert.That(count).IsEqualTo(2).Because(because);
                        await Assert.That(ends[0]).IsEqualTo(pad + 3).Because(because);
                        await Assert.That(ends[1]).IsEqualTo(line.Length).Because(because);
                    }
                }
            }
        }
    }
}
