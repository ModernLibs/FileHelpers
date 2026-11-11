using System.Buffers;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;

/// <summary>
/// Unquoting only (design B3, R3): every quoted value of 10,000 lines of the seeded quoted set, from the text between
/// its outer quotes to the field's string.
/// </summary>
/// <remarks>
/// <see cref="Unquote_Current"/> is the strip pass the generator emits today for a <c>[FieldQuoted]</c> field: a
/// scalar search for a doubled quote, then, when there is one, a scalar copy into a rented buffer, then
/// <c>new string</c>. <see cref="Unquote_IndexOf"/> is the R3 helper of design 4.6 (<c>UnquoteToString</c>): an
/// <c>IndexOf</c> test, and with a doubled quote one <c>string.Create</c> that copies the blocks between quotes, with
/// no intermediate buffer. Both return the total length of the strings; each setup compares every string with
/// <c>Replace("\"\"", "\"")</c> of the input.
/// </remarks>
[MemoryDiagnoser]
public class UnquoteBenchmarks
{
    private const char Quote = '"';

    // (line, start, length) of the text between the outer quotes of every quoted field.
    private string[] _lines = [];
    private int[] _inner = [];

    private void Prepare()
    {
        _lines = DataCheck.DataLines(DatasetGenerator.EnsureQuotedFieldsRandom(ComponentData.DataDir(), 10_000), hasHeader: true);
        var inner = new List<int>();
        for (int li = 0; li < _lines.Length; li++)
        {
            foreach (var (start, end, quoted) in ComponentData.QuotedFields(_lines[li]))
            {
                if (quoted)
                    inner.AddRange([li, start + 1, end - start - 2]);
            }
        }
        _inner = inner.ToArray();
        DataCheck.That(_inner.Length > 0, "the quoted set has no quoted field");
    }

    private void Check(string name, Func<ReadOnlySpan<char>, string> unquote, long checksum)
    {
        long length = 0;
        int doubled = 0;
        for (int k = 0; k < _inner.Length; k += 3)
        {
            string raw = _lines[_inner[k]].Substring(_inner[k + 1], _inner[k + 2]);
            string expected = raw.Replace("\"\"", "\"", StringComparison.Ordinal);
            if (expected.Length != raw.Length)
                doubled++;
            length += expected.Length;
            DataCheck.Equal(expected, unquote(raw), $"{name} line {_inner[k]} value at {_inner[k + 1]}");
        }
        DataCheck.That(doubled > 0, "the quoted set has no doubled quote");
        DataCheck.Equal(length, checksum, name + " total length");
    }

    [GlobalSetup(Target = nameof(Unquote_Current))]
    public void SetupCurrent()
    {
        Prepare();
        Check(nameof(Unquote_Current), UnquoteCurrent, Unquote_Current());
    }

    [GlobalSetup(Target = nameof(Unquote_IndexOf))]
    public void SetupIndexOf()
    {
        Prepare();
        Check(nameof(Unquote_IndexOf), UnquoteIndexOf, Unquote_IndexOf());
    }

    [Benchmark(Baseline = true)]
    public long Unquote_Current()
    {
        long length = 0;
        string[] lines = _lines;
        int[] inner = _inner;
        for (int k = 0; k < inner.Length; k += 3)
            length += UnquoteCurrent(lines[inner[k]].AsSpan(inner[k + 1], inner[k + 2])).Length;
        return length;
    }

    [Benchmark]
    public long Unquote_IndexOf()
    {
        long length = 0;
        string[] lines = _lines;
        int[] inner = _inner;
        for (int k = 0; k < inner.Length; k += 3)
            length += UnquoteIndexOf(lines[inner[k]].AsSpan(inner[k + 1], inner[k + 2])).Length;
        return length;
    }

    /// <summary>The emitted strip pass of today (the <c>[FieldQuoted] strip + unescape</c> block of the generated parser).</summary>
    internal static string UnquoteCurrent(ReadOnlySpan<char> inner)
    {
        char[]? rent = null;
        try
        {
            ReadOnlySpan<char> value = inner;
            bool hasDoubled = false;
            for (int i = 0; i < inner.Length - 1; i++)
            {
                if (inner[i] == Quote && inner[i + 1] == Quote)
                {
                    hasDoubled = true;
                    break;
                }
            }
            if (hasDoubled)
            {
                var buffer = ArrayPool<char>.Shared.Rent(inner.Length);
                rent = buffer;
                int length = 0;
                for (int i = 0; i < inner.Length; i++)
                {
                    char c = inner[i];
                    if (c == Quote && i + 1 < inner.Length && inner[i + 1] == Quote)
                    {
                        buffer[length++] = Quote;
                        i++;
                    }
                    else
                    {
                        buffer[length++] = c;
                    }
                }
                value = new ReadOnlySpan<char>(buffer, 0, length);
            }
            return new string(value);
        }
        finally
        {
            if (rent is not null)
                ArrayPool<char>.Shared.Return(rent);
        }
    }

    /// <summary>
    /// The R3 helper: <c>IndexOf</c> for the first quote; none, or no doubled one, gives <c>new string</c>; otherwise
    /// one <c>string.Create</c> of the final length that copies the blocks between quotes. A lone quote is kept.
    /// </summary>
    internal static string UnquoteIndexOf(ReadOnlySpan<char> inner)
    {
        int first = inner.IndexOf(Quote);
        if (first < 0)
            return new string(inner);

        int pairs = 0;
        for (int at = first; at >= 0;)
        {
            int next;
            if (at + 1 < inner.Length && inner[at + 1] == Quote)
            {
                pairs++;
                next = at + 2;
            }
            else
            {
                next = at + 1;
            }
            int found = inner.Slice(next).IndexOf(Quote);
            at = found < 0 ? -1 : next + found;
        }
        if (pairs == 0)
            return new string(inner);

        return string.Create(inner.Length - pairs, inner, static (destination, source) =>
        {
            while (true)
            {
                int at = source.IndexOf(Quote);
                if (at < 0)
                {
                    source.CopyTo(destination);
                    return;
                }
                source.Slice(0, at + 1).CopyTo(destination);
                destination = destination.Slice(at + 1);
                int skip = at + 1 < source.Length && source[at + 1] == Quote ? at + 2 : at + 1;
                source = source.Slice(skip);
            }
        });
    }
}
