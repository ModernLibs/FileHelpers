// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004

using System.Text;
using ModernLibs.FileHelpers;
using ModernLibs.FileHelpers.Tests.Errors;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// E1: parallel reads produce exactly what the sequential read produces — header/footer from the whole
/// input, ErrorMode honoured with correct line numbers, records in order — independent of input size.
/// </summary>
[Category("ReadCore")]
public sealed class ReadCoreParallelTests
{
    private const int ChunkSize = 64 * 1024;

    /// <summary>~250 KB framed input (4+ chunks) with empty, comment and bad lines spread over it.</summary>
    private static string BuildFramed(int records, Func<int, string?>? special = null)
    {
        var sb = new StringBuilder("HEADER LINE\r\n");
        for (int i = 1; i <= records; i++)
        {
            string? line = special?.Invoke(i);
            sb.Append(line ?? $"{i},name-{i}").Append("\r\n");
        }

        sb.Append("FOOTER 1\r\nFOOTER 2\r\n");
        return sb.ToString();
    }

    private static string Special(int i) => (i % 5000) switch
    {
        1 => "",
        2 => "# comment",
        3 => "not-a-number,x",
        _ => null!,
    };

    [Test]
    [DisplayName("E1: parallel SaveAndContinue matches ReadString: records, header/footer, errors and line numbers")]
    public async Task Parallel_SaveAndContinue_MatchesSequential()
    {
        // Arrange: large framed input; two engines in SaveAndContinue
        string input = BuildFramed(20_000, Special);
        var sequential = new FileHelperEngine<RcFramed> { ErrorMode = ErrorMode.SaveAndContinue };
        var parallel = new FileHelperEngine<RcFramed> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: sequential and parallel reads
        var expected = sequential.ReadString(input);
        var actual = parallel.ParallelReadString(input, ChunkSize);

        // Assert: identical outcome
        await Assert.That(input.Length).IsGreaterThan(ChunkSize * 3);
        await Assert.That(actual.Length).IsEqualTo(expected.Length);
        await Assert.That(actual.Select(r => r.Id).SequenceEqual(expected.Select(r => r.Id))).IsTrue();
        await Assert.That(parallel.HeaderText).IsEqualTo(sequential.HeaderText);
        await Assert.That(parallel.FooterText).IsEqualTo("FOOTER 1\r\nFOOTER 2");
        await Assert.That(parallel.TotalRecords).IsEqualTo(sequential.TotalRecords);
        await Assert.That(parallel.LineNumber).IsEqualTo(sequential.LineNumber);
        await Assert.That(string.Join(",", parallel.ErrorManager.Errors.Select(e => e.LineNumber)))
            .IsEqualTo(string.Join(",", sequential.ErrorManager.Errors.Select(e => e.LineNumber)));
        await Assert.That(parallel.ErrorManager.ErrorCount).IsEqualTo(4);
    }

    [Test]
    [DisplayName("E1: parallel ThrowException throws the earliest failing record with its line number")]
    public async Task Parallel_Throw_ThrowsEarliestError()
    {
        // Arrange: bad records at 1-based data rows 9000 and 15000 (file lines 9001 and 15001)
        string input = BuildFramed(20_000, i => i is 9000 or 15000 ? "bad,row" : null);
        var engine = new FileHelperEngine<RcFramed>();

        // Act: parallel read
        var ex = Capture.Exception(() => engine.ParallelReadString(input, ChunkSize));

        // Assert: the first error in input order
        await Assert.That(ex).IsAssignableTo<FileHelpersException>();
        await Assert.That(((FileHelpersException)ex!).LineNumber).IsEqualTo(9001);
    }

    [Test]
    [DisplayName("E1: behaviour does not change with input size (empty lines are errors without IgnoreEmptyLines)")]
    public async Task Parallel_SmallAndLargeInputs_SameSemantics()
    {
        // Arrange: record without IgnoreEmptyLines; an empty line in a small and in a large input
        static string Build(int n) => string.Concat(Enumerable.Range(1, n).Select(i => i == 2 ? "\n" : $"{i},n\n"));
        var small = new FileHelperEngine<RcRecord> { ErrorMode = ErrorMode.SaveAndContinue };
        var large = new FileHelperEngine<RcRecord> { ErrorMode = ErrorMode.SaveAndContinue };

        // Act: parallel read of both sizes
        var smallRecords = small.ParallelReadString(Build(10), ChunkSize);
        var largeRecords = large.ParallelReadString(Build(40_000), ChunkSize);

        // Assert: the empty line is reported as an error in both cases
        await Assert.That(smallRecords.Length).IsEqualTo(9);
        await Assert.That(largeRecords.Length).IsEqualTo(39_999);
        await Assert.That(small.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
        await Assert.That(large.ErrorManager.Errors[0].LineNumber).IsEqualTo(2);
    }

    [Test]
    [DisplayName("E1: multiline records fall back to the sequential pipeline and match ReadString")]
    public async Task Parallel_Multiline_FallsBackToSequential()
    {
        // Arrange: large multiline input
        var sb = new StringBuilder();
        for (int i = 0; i < 8000; i++)
            sb.Append(i).Append(",\"line a\r\nline b ").Append(i).Append("\",").Append(i).Append("\r\n");
        string input = sb.ToString();
        var engine = new FileHelperEngine<RcMultiline>();

        // Act: parallel read
        var records = engine.ParallelReadString(input, ChunkSize);

        // Assert: every quoted field kept its embedded line break
        await Assert.That(records.Length).IsEqualTo(8000);
        await Assert.That(records[7999].Notes).IsEqualTo("line a\r\nline b 7999");
    }

    [Test]
    [DisplayName("E1: progress is reported once per chunk, serialized, ending at the total record count")]
    public async Task Parallel_Progress_IsSerialized()
    {
        // Arrange: large input and a progress recorder that detects concurrent calls
        string input = BuildFramed(20_000);
        var engine = new FileHelperEngine<RcFramed>();
        int active = 0;
        bool overlapped = false;
        var calls = new List<(int Chunk, int Total, int Records)>();

        // Act: parallel read with progress
        var records = engine.ParallelReadString(input, ChunkSize, (chunk, total, soFar) =>
        {
            if (Interlocked.Increment(ref active) > 1)
                overlapped = true;
            calls.Add((chunk, total, soFar));
            Thread.Sleep(1);
            Interlocked.Decrement(ref active);
        });

        // Assert: one call per chunk, cumulative count ends at the total
        await Assert.That(overlapped).IsFalse();
        await Assert.That(calls.Count).IsEqualTo(calls[0].Total);
        await Assert.That(calls.Select(c => c.Chunk).Distinct().Count()).IsEqualTo(calls.Count);
        await Assert.That(calls[^1].Records).IsEqualTo(records.Length);
    }
}
