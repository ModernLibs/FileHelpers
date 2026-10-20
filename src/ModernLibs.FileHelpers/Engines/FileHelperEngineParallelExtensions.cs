// Parallel batch reading engine extension.

using System.Runtime.ExceptionServices;
using System.Text;
using ModernLibs.FileHelpers.Parsing;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Parallel batch reading extensions for <see cref="FileHelperEngine{T}"/>.
/// Chunk-based reading with configurable chunk size for multi-core parallelism.
/// </summary>
/// <remarks>
/// <para>Results are identical to <see cref="FileHelperEngine{T}.ReadString(string)"/> for the same input and
/// options, whatever the input size: header and footer are taken from the whole input, every chunk runs the
/// engine's record pipeline (filters, field-count validation, <see cref="ErrorMode"/>) with the original line
/// numbers, records and saved errors are merged in input order, and in <see cref="ErrorMode.ThrowException"/>
/// the error of the earliest failing record is thrown. <see cref="FileHelperEngine{T}.HeaderText"/>,
/// <see cref="FileHelperEngine{T}.FooterText"/>, <see cref="FileHelperEngine{T}.TotalRecords"/>,
/// <see cref="FileHelperEngine{T}.LineNumber"/> and <see cref="FileHelperEngine{T}.ErrorManager"/> are set as
/// by a sequential read.</para>
/// <para>Inputs that cannot be split safely fall back to the sequential read: multiline quoted fields,
/// <see cref="FieldInNewLineAttribute"/> records, and engines with read events (<c>BeforeReadRecord</c>,
/// <c>AfterReadRecord</c>, <c>Progress</c>, <see cref="INotifyRead{T}"/>). Inputs smaller than two chunks are
/// read sequentially as well.</para>
/// <para>The <c>progress</c> callback is invoked once per completed chunk, never concurrently, with the chunk
/// index, the number of chunks and the number of records parsed by completed chunks so far.</para>
/// </remarks>
public static class FileHelperEngineParallelExtensions
{
    /// <summary>Default chunk size: 4M characters — balances parallelism overhead with memory usage.</summary>
    private const int DefaultChunkSize = 4 * 1024 * 1024;

    /// <summary>Minimum chunk size in characters.</summary>
    private const int MinimumChunkSize = 64 * 1024;

    /// <summary>
    /// Reads all records from <paramref name="filePath"/> using parallel chunk processing.
    /// </summary>
    /// <typeparam name="T">Record type with source-generated descriptor.</typeparam>
    /// <param name="engine">The engine instance (descriptor and config are used).</param>
    /// <param name="filePath">Path to the file to read.</param>
    /// <returns>Array of parsed records.</returns>
    public static T[] ParallelReadFile<T>(this FileHelperEngine<T> engine, string filePath)
        => ParallelReadFile<T>(engine, filePath, DefaultChunkSize, null, CancellationToken.None);

    /// <summary>
    /// Reads all records from <paramref name="filePath"/> using parallel chunk processing.
    /// </summary>
    /// <param name="engine">The engine instance.</param>
    /// <param name="filePath">Path to the file.</param>
    /// <param name="chunkSize">Chunk size in characters (minimum 64K). Defaults to 4M.</param>
    /// <param name="progress">Optional progress callback invoked per completed chunk (chunkIndex, totalChunks, recordsSoFar).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static T[] ParallelReadFile<T>(
        this FileHelperEngine<T> engine,
        string filePath,
        int chunkSize,
        Action<int, int, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(filePath);

        var content = File.ReadAllText(filePath, engine.Encoding);
        return ParallelReadStringCore(engine, content, chunkSize, progress, cancellationToken);
    }

    /// <summary>
    /// Reads all records from <paramref name="content"/> using parallel chunk processing.
    /// </summary>
    public static T[] ParallelReadString<T>(this FileHelperEngine<T> engine, string content)
        => ParallelReadString<T>(engine, content, DefaultChunkSize, null, CancellationToken.None);

    /// <summary>
    /// Reads all records from <paramref name="content"/> using parallel chunk processing.
    /// </summary>
    public static T[] ParallelReadString<T>(
        this FileHelperEngine<T> engine,
        string content,
        int chunkSize,
        Action<int, int, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(content);
        return ParallelReadStringCore(engine, content, chunkSize, progress, cancellationToken);
    }

    private static T[] ParallelReadStringCore<T>(
        FileHelperEngine<T> engine,
        string content,
        int chunkSize,
        Action<int, int, int>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        chunkSize = Math.Max(chunkSize, MinimumChunkSize);

        var core = engine.Core;
        bool chunkable = !core.Multiline && core.LinesPerRecord == 1 && !engine.HasReadEvents;
        if (!chunkable || content.Length < chunkSize * 2)
            return engine.ReadString(content);

        var layout = SplitIntoChunks(content, core.Options, engine.NewLineForWrite, chunkSize);
        int totalChunks = layout.Chunks.Count;
        var results = new ChunkResult<T>[totalChunks];
        var progressGate = new Lock();
        int recordsSoFar = 0;
        var errorMode = engine.ErrorMode;

        Parallel.For(0, totalChunks, new ParallelOptions { CancellationToken = cancellationToken }, chunkIndex =>
        {
            var chunk = layout.Chunks[chunkIndex];
            var result = ParseChunk(engine, core, content.AsSpan(chunk.Start, chunk.Length), chunk.FirstLine - 1, errorMode, cancellationToken);
            results[chunkIndex] = result;

            if (progress is not null)
            {
                lock (progressGate)
                {
                    recordsSoFar += result.Records.Count;
                    progress(chunkIndex, totalChunks, recordsSoFar);
                }
            }
        });

        return MergeResults(engine, layout, results);
    }

    private static T[] MergeResults<T>(FileHelperEngine<T> engine, ChunkLayout layout, ChunkResult<T>[] results)
    {
        int total = 0;
        int lastLine = 0;
        var errors = new List<ErrorInfo>();
        foreach (var result in results)
        {
            if (result.Failure is not null)
            {
                engine.ApplyReadResult(lastLine, total, layout.HeaderText, layout.FooterText, errors);
                result.Failure.Throw();
            }

            total += result.Records.Count;
            if (result.LastLineNumber > 0)
                lastLine = result.LastLineNumber;
            errors.AddRange(result.Errors);
        }

        var merged = new T[total];
        int offset = 0;
        foreach (var result in results)
        {
            result.Records.CopyTo(merged, offset);
            offset += result.Records.Count;
        }

        engine.ApplyReadResult(lastLine, total, layout.HeaderText, layout.FooterText, errors);
        return merged;
    }

    private static ChunkResult<T> ParseChunk<T>(
        FileHelperEngine<T> engine,
        RecordReadCore<T> core,
        ReadOnlySpan<char> chunk,
        int lineOffset,
        ErrorMode errorMode,
        CancellationToken cancellationToken)
    {
        var errors = new ErrorManager { ErrorMode = errorMode };
        var records = new List<T>();
        using var session = new RecordReadSession<T>(core, engine, host: null, errors, engine.NewLineForWrite, maxRecords: 0, applyHeaderFooter: false, lineOffset);
        try
        {
            var lines = new SpanLines(chunk);
            int pushed = 0;
            while (lines.TryRead(out var line, out var ending))
            {
                if ((++pushed & 0x3FF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                if (session.Push(line, ending))
                    records.Add(session.Current);
            }

            session.Complete();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ThrowException mode: keep the failure; the earliest failing chunk is rethrown after the join.
            return new ChunkResult<T>(records, errors.Errors, session.LastLineNumber, ExceptionDispatchInfo.Capture(ex));
        }

        return new ChunkResult<T>(records, errors.Errors, session.LastLineNumber, null);
    }

    /// <summary>
    /// Splits <paramref name="content"/> into header, data chunks aligned to line boundaries (with the
    /// 1-based number of their first line) and footer.
    /// </summary>
    private static ChunkLayout SplitIntoChunks(string content, RecordOptions options, string newLine, int chunkSize)
    {
        var span = content.AsSpan();
        int totalLines = LineSplitter.CountLines(span);
        int headerLines = Math.Min(Math.Max(0, options.IgnoreFirst), totalLines);
        int footerLines = Math.Min(Math.Max(0, options.IgnoreLast), totalLines - headerLines);
        int dataLines = totalLines - headerLines - footerLines;

        int position = 0;
        string? header = headerLines > 0 ? CollectLines(span, ref position, headerLines, newLine) : null;

        var chunks = new List<Chunk>();
        int chunkStart = position;
        int chunkFirstLine = headerLines + 1;
        for (int i = 0; i < dataLines; i++)
        {
            LineSplitter.TryFindLine(span[position..], isFinal: true, out _, out int consumed, out _);
            position += consumed;
            if (position - chunkStart >= chunkSize || i == dataLines - 1)
            {
                chunks.Add(new Chunk(chunkStart, position - chunkStart, chunkFirstLine));
                chunkStart = position;
                chunkFirstLine = headerLines + i + 2;
            }
        }

        string? footer = footerLines > 0 ? CollectLines(span, ref position, footerLines, newLine) : null;
        return new ChunkLayout(chunks, header, footer);
    }

    private static string CollectLines(ReadOnlySpan<char> content, ref int position, int count, string newLine)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            LineSplitter.TryFindLine(content[position..], isFinal: true, out int length, out int consumed, out _);
            if (i > 0)
                sb.Append(newLine);
            sb.Append(content.Slice(position, length));
            position += consumed;
        }

        return sb.ToString();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
    private readonly record struct Chunk(int Start, int Length, int FirstLine);

    private sealed record ChunkLayout(List<Chunk> Chunks, string? HeaderText, string? FooterText);

    private sealed record ChunkResult<T>(List<T> Records, IReadOnlyList<ErrorInfo> Errors, int LastLineNumber, ExceptionDispatchInfo? Failure);
}
