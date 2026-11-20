#if !FILEHELPERS_BASELINE
using System.Buffers;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;

/// <summary>
/// In-place quoting of the record writer (T23 review, G2): 1,000 values of <see cref="Length"/> chars, each written as
/// one field of its own record into the engine's <see cref="RentedBufferWriter"/> through
/// <see cref="RecordWriter"/>, as a generated always-quoted string field writes it. <see cref="Write_Only"/> writes the
/// value without quoting; <see cref="Quote_Clean"/> quotes values without quote chars (one scan and a shift by one);
/// <see cref="Quote_WithQuotes"/> quotes values with a quote char every 8 chars (the doubling of G3). The difference to
/// <see cref="Write_Only"/> is the cost of <see cref="RecordWriter.Quote"/>. <see cref="WriteQuoted_Clean"/> and
/// <see cref="WriteQuoted_WithQuotes"/> write the same values with <see cref="RecordWriter.WriteQuoted"/> (freeze
/// Revision 1), the call a generated quoted string field makes: tested in the string and copied once.
/// </summary>
/// <remarks>
/// <see cref="RecordWriter"/> and <c>IRecordWriterDescriptor&lt;T&gt;</c> are new in 4.10: a build against the 4.9.1
/// package or source (<c>FILEHELPERS_BASELINE</c>) has no such class. Each method returns the number of chars
/// written; each setup checks every value's text against <see cref="string.Replace(string, string)"/>.
/// </remarks>
[MemoryDiagnoser]
public class QuoteBenchmarks : IDisposable
{
    private const int Values = 1_000;

    private string[] _values = [];
    private ValueDescriptor _descriptor = null!;
    private RentedBufferWriter _buffer = null!;

    [Params(16, 64, 256)]
    public int Length { get; set; }

    private void Prepare(bool withQuotes, QuoteKind quote)
    {
        var random = new Random(2311);
        _values = new string[Values];
        for (int i = 0; i < Values; i++)
        {
            var chars = new char[Length];
            for (int c = 0; c < chars.Length; c++)
                chars[c] = withQuotes && c % 8 == 7 ? '"' : (char)('a' + random.Next(26));
            _values[i] = new string(chars);
        }

        _descriptor = new ValueDescriptor(quote);
        _buffer?.Dispose();
        _buffer = new RentedBufferWriter(4096);
    }

    private void Check(string name, long written)
    {
        long expected = 0;
        foreach (string value in _values)
        {
            _buffer.Reset();
            _buffer.Format(_descriptor, value);
            string text = _descriptor.Kind != QuoteKind.None ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
            DataCheck.Equal(text, _buffer.WrittenSpan.ToString(), name);
            expected += text.Length;
        }

        DataCheck.Equal(expected, written, name + " characters written");
    }

    [GlobalSetup(Target = nameof(Write_Only))]
    public void SetupWriteOnly()
    {
        Prepare(withQuotes: false, QuoteKind.None);
        Check(nameof(Write_Only), Write_Only());
    }

    [GlobalSetup(Target = nameof(Quote_Clean))]
    public void SetupClean()
    {
        Prepare(withQuotes: false, QuoteKind.InPlace);
        Check(nameof(Quote_Clean), Quote_Clean());
    }

    [GlobalSetup(Target = nameof(Quote_WithQuotes))]
    public void SetupWithQuotes()
    {
        Prepare(withQuotes: true, QuoteKind.InPlace);
        Check(nameof(Quote_WithQuotes), Quote_WithQuotes());
    }

    [GlobalSetup(Target = nameof(WriteQuoted_Clean))]
    public void SetupWriteQuotedClean()
    {
        Prepare(withQuotes: false, QuoteKind.WriteQuoted);
        Check(nameof(WriteQuoted_Clean), WriteQuoted_Clean());
    }

    [GlobalSetup(Target = nameof(WriteQuoted_WithQuotes))]
    public void SetupWriteQuotedWithQuotes()
    {
        Prepare(withQuotes: true, QuoteKind.WriteQuoted);
        Check(nameof(WriteQuoted_WithQuotes), WriteQuoted_WithQuotes());
    }

    [GlobalCleanup]
    public void Cleanup() => Dispose();

    public void Dispose()
    {
        _buffer?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public long Write_Only() => FormatAll();

    [Benchmark]
    public long Quote_Clean() => FormatAll();

    [Benchmark]
    public long Quote_WithQuotes() => FormatAll();

    [Benchmark]
    public long WriteQuoted_Clean() => FormatAll();

    [Benchmark]
    public long WriteQuoted_WithQuotes() => FormatAll();

    private long FormatAll()
    {
        long written = 0;
        var descriptor = _descriptor;
        var buffer = _buffer;
        foreach (string value in _values)
        {
            buffer.Reset();
            buffer.Format(descriptor, value);
            written += buffer.WrittenCount;
        }

        return written;
    }

    /// <summary>How <see cref="ValueDescriptor"/> writes its field.</summary>
    private enum QuoteKind
    {
        None,
        InPlace,
        WriteQuoted,
    }

    /// <summary>One string field: unquoted, quoted in place with <c>Quote</c>, or written with <c>WriteQuoted</c>.</summary>
    private sealed class ValueDescriptor(QuoteKind kind) : IRecordWriterDescriptor<string>
    {
        public QuoteKind Kind => kind;

        public ReadOnlySpan<FieldDescriptor> Fields => throw new NotSupportedException();

        public RecordOptions Options => throw new NotSupportedException();

        public string Parse(ReadOnlySpan<char> lines, int lineCount) => throw new NotSupportedException();

        public string CreateDefaultInstance() => throw new NotSupportedException();

        public void Format(string record, IBufferWriter<char> writer) => RecordFormatting.Format(this, record, writer);

        public void Format(string record, ref RecordWriter writer)
        {
            if (kind == QuoteKind.WriteQuoted)
            {
                writer.WriteQuoted(record.AsSpan(), '"', true, true, ",", "Value");
                return;
            }

            int start = writer.Position;
            writer.Write(record.AsSpan());
            if (kind == QuoteKind.InPlace)
                writer.Quote(start, '"', true, true, ",", "Value");
        }
    }
}
#endif
