// MA0004: ConfigureAwait not needed in benchmarks.
#pragma warning disable MA0004
// MA0051: Benchmark methods are intentionally concise.
#pragma warning disable MA0051

using System.Globalization;
using BenchmarkDotNet.Attributes;
using ModernLibs.FileHelpers.Benchmarks.Data;

namespace ModernLibs.FileHelpers.Benchmarks.Benchmarks;

/// <summary>
/// Writes a header and a detail record per row with <see cref="MultiRecordEngine"/> (two record types, one
/// formatter per type), to <see cref="TextWriter.Null"/>. Guards the multi-record write loop against the
/// .NET 11 inlining regression (dotnet/runtime#137552).
/// </summary>
[MemoryDiagnoser]
public class MultiRecordWriteBenchmarks
{
    private object[] _records = null!;
    private MultiRecordEngine _engine = null!;

    [Params(10_000, 100_000)]
    public int Rows { get; set; }

    private void Prepare()
    {
        _engine = new MultiRecordEngine(
            MultiRecordType.For<AsyncHeaderRecord>(line => line.StartsWith("H|", StringComparison.Ordinal)),
            MultiRecordType.For<AsyncDetailRecord>(line => line.StartsWith("D|", StringComparison.Ordinal)))
        {
            NewLineForWrite = "\n",
        };

        _records = new object[2 * Rows];
        for (int i = 0; i < Rows; i++)
        {
            _records[2 * i] = new AsyncHeaderRecord("H", "BATCH" + i.ToString(CultureInfo.InvariantCulture));
            _records[(2 * i) + 1] = new AsyncDetailRecord("D", i, i * 1.5m);
        }
    }

    // Correctness guard: the character count of the written text must equal the count computed from the
    // data. Each setup runs only the guard of its own benchmark (see WriteBenchmarks).
    [GlobalSetup(Target = nameof(WriteStream_Null))]
    public void SetupWriteStream()
    {
        Prepare();
        long expected = 0;
        for (int i = 0; i < Rows; i++)
        {
            expected += "H|BATCH".Length + i.ToString(CultureInfo.InvariantCulture).Length + 1;
            expected += string.Create(CultureInfo.InvariantCulture, $"D|{i}|{i * 1.5m}").Length + 1;
        }

        using var counter = new CountingWriter();
        _engine.WriteStream(counter, _records);
        DataCheck.Equal(expected, counter.Count, "MultiRecordEngine written character count");
        WriteStream_Null();
    }

    [Benchmark]
    public void WriteStream_Null() => _engine.WriteStream(TextWriter.Null, _records);

    private sealed class CountingWriter : TextWriter
    {
        public long Count { get; private set; }

        public override System.Text.Encoding Encoding => System.Text.Encoding.Unicode;

        public override void Write(char value) => Count++;

        public override void Write(string? value) => Count += value?.Length ?? 0;

        public override void Write(ReadOnlySpan<char> buffer) => Count += buffer.Length;

        public override void Write(char[] buffer, int index, int count) => Count += count;
    }
}
