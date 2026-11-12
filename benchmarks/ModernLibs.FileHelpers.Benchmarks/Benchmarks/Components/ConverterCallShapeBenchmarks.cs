using System.Buffers;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;
using ModernLibs.FileHelpers.Converters;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks.Components;

/// <summary>
/// Three ways for emitted code to write a converted field (design 2.5 item 2): one record of an <c>int</c>, a
/// <see cref="DateTime"/> (format <c>"s"</c>) and a <c>string</c> field, written 1,000 times into the engine's
/// <see cref="RentedBufferWriter"/> through <see cref="IBufferWriter{T}"/>, as a descriptor's <c>Format</c> is called.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="Format_GenericHelper"/>: today's emitted shape. Each converted field formats into its own
/// <c>stackalloc char[64]</c> through the private generic helper <c>__Format&lt;TConverter, TValue&gt;</c>
/// (<c>DescriptorEmitter.cs:1280-1295</c>, which grows into pooled arrays) and is then copied with <c>GetSpan</c> and
/// <c>Advance</c>; a delimiter is one <c>GetSpan(1)</c> and <c>Advance(1)</c>.</item>
/// <item><see cref="Format_GenericWriter"/>: a <c>ref struct</c> writer over one free span of the buffer with a generic
/// <c>Format&lt;TConverter, TValue&gt;</c> constrained to <see cref="ISpanConverter{T}"/> (the <c>RecordWriter</c> of
/// design 5.1): the converter formats straight into the buffer; on <see langword="false"/> it grows and retries.</item>
/// <item><see cref="Format_DirectCall"/>: the caller takes one span and calls <c>Int32Converter.Instance.TryFormat</c>
/// and the date converter's <c>TryFormat</c> directly, then advances once.</item>
/// </list>
/// Each benchmark returns the number of characters written. Its setup formats every record through the same method
/// the benchmark calls and compares the text with the value formatted by the BCL.
/// </remarks>
[MemoryDiagnoser]
public class ConverterCallShapeBenchmarks : IDisposable
{
    private const int Records = 1_000;

    private static readonly DateTimeConverter s_date = new(DatasetGenerator.DateFormat);

    private readonly int[] _ints = new int[Records];
    private readonly DateTime[] _dates = new DateTime[Records];
    private readonly string[] _strings = new string[Records];
    private RentedBufferWriter _buffer = null!;

    private void Prepare()
    {
        var random = new Random(42);
        for (int i = 0; i < Records; i++)
        {
            _ints[i] = random.Next(0, 10_000_000);
            _dates[i] = DatasetGenerator.BaseDate.AddDays(-random.Next(0, 3650)).AddSeconds(random.Next(0, 86_400));
            _strings[i] = string.Create(CultureInfo.InvariantCulture, $"https://example.com/{i}");
        }
        _buffer?.Dispose();
        _buffer = new RentedBufferWriter(4096);
    }

    /// <summary>Formats every record through <paramref name="write"/> and compares it with the BCL's text.</summary>
    private void Check(string name, Action<IBufferWriter<char>, int> write, long checksum)
    {
        long expectedLength = 0;
        for (int i = 0; i < Records; i++)
        {
            string expected = string.Create(CultureInfo.InvariantCulture, $"{_ints[i]},{_dates[i]:s},{_strings[i]}");
            expectedLength += expected.Length;
            _buffer.Reset();
            write(_buffer, i);
            DataCheck.Equal(expected, _buffer.WrittenSpan.ToString(), $"{name} record {i}");
        }
        DataCheck.Equal(expectedLength, checksum, name + " characters written");
    }

    [GlobalSetup(Target = nameof(Format_GenericHelper))]
    public void SetupGenericHelper()
    {
        Prepare();
        Check(nameof(Format_GenericHelper), WriteGenericHelper, Format_GenericHelper());
    }

    [GlobalSetup(Target = nameof(Format_GenericWriter))]
    public void SetupGenericWriter()
    {
        Prepare();
        Check(nameof(Format_GenericWriter), WriteGenericWriter, Format_GenericWriter());
    }

    [GlobalSetup(Target = nameof(Format_DirectCall))]
    public void SetupDirectCall()
    {
        Prepare();
        Check(nameof(Format_DirectCall), WriteDirectCall, Format_DirectCall());
    }

    [GlobalCleanup]
    public void Cleanup() => Dispose();

    public void Dispose()
    {
        _buffer?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public long Format_GenericHelper()
    {
        long written = 0;
        var buffer = _buffer;
        for (int i = 0; i < Records; i++)
        {
            buffer.Reset();
            WriteGenericHelper(buffer, i);
            written += buffer.WrittenSpan.Length;
        }
        return written;
    }

    [Benchmark]
    public long Format_GenericWriter()
    {
        long written = 0;
        var buffer = _buffer;
        for (int i = 0; i < Records; i++)
        {
            buffer.Reset();
            WriteGenericWriter(buffer, i);
            written += buffer.WrittenSpan.Length;
        }
        return written;
    }

    [Benchmark]
    public long Format_DirectCall()
    {
        long written = 0;
        var buffer = _buffer;
        for (int i = 0; i < Records; i++)
        {
            buffer.Reset();
            WriteDirectCall(buffer, i);
            written += buffer.WrittenSpan.Length;
        }
        return written;
    }

    // (a) The emitted shape of today (DescriptorEmitter.cs:1280-1295, :1349-1385).
    private void WriteGenericHelper(IBufferWriter<char> writer, int i)
    {
        {
            Span<char> b0 = stackalloc char[64];
            char[]? r0 = null;
            scoped ReadOnlySpan<char> t0;
            t0 = EmittedFormat(Int32Converter.Instance, _ints[i], b0, ref r0);
            EmittedWriteText(writer, t0);
            if (r0 is not null)
                ArrayPool<char>.Shared.Return(r0);
        }
        { writer.GetSpan(1)[0] = ','; writer.Advance(1); }
        {
            Span<char> b1 = stackalloc char[64];
            char[]? r1 = null;
            scoped ReadOnlySpan<char> t1;
            t1 = EmittedFormat(s_date, _dates[i], b1, ref r1);
            EmittedWriteText(writer, t1);
            if (r1 is not null)
                ArrayPool<char>.Shared.Return(r1);
        }
        { writer.GetSpan(1)[0] = ','; writer.Advance(1); }
        {
            var t2 = _strings[i].AsSpan();
            EmittedWriteText(writer, t2);
        }
    }

    private static void EmittedWriteText(IBufferWriter<char> writer, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return;
        text.CopyTo(writer.GetSpan(text.Length));
        writer.Advance(text.Length);
    }

    private static ReadOnlySpan<char> EmittedFormat<TConverter, TValue>(TConverter converter, TValue value, Span<char> buffer, ref char[]? rented)
        where TConverter : ISpanConverter<TValue>
    {
        int written;
        while (!converter.TryFormat(value, buffer, out written))
        {
            if (buffer.Length >= 0x100000)
                throw new FormatException("The field value could not be formatted by " + converter.GetType().Name + ".");
            int size = buffer.Length * 2;
            if (rented is not null)
                ArrayPool<char>.Shared.Return(rented);
            rented = ArrayPool<char>.Shared.Rent(size);
            buffer = rented;
        }
        return buffer.Slice(0, written);
    }

    // (b) A generic Format on a ref struct writer (design 5.1).
    private void WriteGenericWriter(IBufferWriter<char> writer, int i)
    {
        var record = new SpanRecordWriter(writer);
        record.Format(Int32Converter.Instance, _ints[i]);
        record.Write(',');
        record.Format(s_date, _dates[i]);
        record.Write(',');
        record.Write(_strings[i].AsSpan());
        record.Commit();
    }

    // (c) Direct converter calls into one span, one Advance.
    private void WriteDirectCall(IBufferWriter<char> writer, int i)
    {
        string text = _strings[i];
        Span<char> span = writer.GetSpan(256);
        int position = 0;
        if (Int32Converter.Instance.TryFormat(_ints[i], span, out int n)
            && (uint)(position + n) < (uint)span.Length)
        {
            position += n;
            span[position++] = ',';
            if (s_date.TryFormat(_dates[i], span.Slice(position), out n)
                && (uint)(position + n) < (uint)span.Length)
            {
                position += n;
                span[position++] = ',';
                if (text.AsSpan().TryCopyTo(span.Slice(position)))
                {
                    writer.Advance(position + text.Length);
                    return;
                }
            }
        }

        // Does not fit in 256 characters: never with this data, but the shape must stay correct.
        WriteGenericWriter(writer, i);
    }

    /// <summary>
    /// The prototype of design 5.1's <c>RecordWriter</c>: a free span of the target and a position. Growth commits
    /// what was written and asks the target for a larger span.
    /// </summary>
    private ref struct SpanRecordWriter
    {
        private readonly IBufferWriter<char> _target;
        private Span<char> _span;
        private int _position;

        public SpanRecordWriter(IBufferWriter<char> target)
        {
            _target = target;
            _span = target.GetSpan(256);
            _position = 0;
        }

        public void Write(char value)
        {
            if ((uint)_position >= (uint)_span.Length)
                Grow(1);
            _span[_position++] = value;
        }

        public void Write(ReadOnlySpan<char> text)
        {
            if (!text.TryCopyTo(_span.Slice(_position)))
            {
                Grow(text.Length);
                text.CopyTo(_span.Slice(_position));
            }
            _position += text.Length;
        }

        public void Format<TConverter, TValue>(TConverter converter, TValue value)
            where TConverter : ISpanConverter<TValue>
        {
            int written;
            while (!converter.TryFormat(value, _span.Slice(_position), out written))
            {
                if (_span.Length - _position >= 0x100000)
                    throw new FormatException("The field value could not be formatted by " + converter.GetType().Name + ".");
                Grow(Math.Max(64, (_span.Length - _position) * 2));
            }
            _position += written;
        }

        public void Commit()
        {
            _target.Advance(_position);
            _position = 0;
            _span = default;
        }

        private void Grow(int needed)
        {
            _target.Advance(_position);
            _position = 0;
            _span = _target.GetSpan(Math.Max(needed, _span.Length * 2));
        }
    }
}
