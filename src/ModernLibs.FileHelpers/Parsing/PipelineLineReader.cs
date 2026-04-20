// MA0004: ConfigureAwait — library code; callers control context. Suppressed per project pattern.
#pragma warning disable MA0004

using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;

namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// <see cref="PipeReader"/>-backed async line reader that decodes byte segments into
/// <see cref="ReadOnlyMemory{T}"/> char lines via a stateful <see cref="Decoder"/>.
/// </summary>
/// <remarks>
/// <para>Handles LF (\n), CRLF (\r\n), and CR (\r) line endings, including CRLF split across
/// PipeReader buffer boundaries.</para>
/// <para>The last line without a trailing newline is still yielded.</para>
/// <para>
/// <strong>Lifetime contract:</strong> The <see cref="ReadOnlyMemory{T}"/> yielded by
/// <see cref="ReadLinesAsync"/> references an internal rented <c>char[]</c> buffer that is
/// returned to <see cref="ArrayPool{T}"/> only after the <c>await foreach</c> loop exits
/// (either normally, via <c>break</c>, or via disposal of the enumerator). Callers must not
/// cache the yielded <see cref="ReadOnlyMemory{T}"/> beyond the current loop iteration.
/// </para>
/// <para>
/// <strong>Encoding errors:</strong> Uses <see cref="Encoding.GetDecoder()"/> which defaults to
/// <see cref="DecoderExceptionFallback"/> for <see cref="Encoding.UTF8"/>. Malformed byte
/// sequences throw <see cref="DecoderFallbackException"/>. Callers requiring lenient behavior
/// should pass an encoding with <see cref="DecoderReplacementFallback"/> explicitly.
/// </para>
/// </remarks>
public sealed class PipelineLineReader : IAsyncDisposable
{
    // SearchValues<byte> for SIMD-accelerated newline scan — static readonly per CLAUDE.md.
    private static readonly SearchValues<byte> s_newlineBytes =
        SearchValues.Create([(byte)'\r', (byte)'\n']);

    private readonly PipeReader _pipe;
    private readonly Encoding _encoding;
    private readonly Decoder _decoder;
    private bool _pipeCompleted;

    // Char decode buffer — instance field so async iterator helpers can resize it without
    // threading ref parameters across yield points (not permitted by the C# compiler).
    // Allocated lazily in ReadLinesAsync and returned to the pool in the finally block.
    private char[] _charBuf = [];

    /// <summary>
    /// Initializes a new <see cref="PipelineLineReader"/> that reads from <paramref name="stream"/>.
    /// The stream is owned by this reader and will be closed when the reader is disposed.
    /// </summary>
    /// <param name="stream">Source byte stream. Must be readable.</param>
    /// <param name="encoding">Character encoding for decoding bytes to chars.</param>
    public PipelineLineReader(Stream stream, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(encoding);

        _pipe = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: false));
        _encoding = encoding;
        _decoder = encoding.GetDecoder();
    }

    /// <summary>
    /// Asynchronously enumerates lines from the underlying stream.
    /// Each <see cref="ReadOnlyMemory{T}"/> contains one decoded line with all line-ending
    /// characters stripped.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel iteration. Must use
    /// <c>[EnumeratorCancellation]</c> — compiler generates the combined-token pattern for
    /// callers using <c>.WithCancellation(ct)</c> on the returned <see cref="IAsyncEnumerable{T}"/>.</param>
    /// <returns>An async sequence of decoded lines.</returns>
    public async IAsyncEnumerable<ReadOnlyMemory<char>> ReadLinesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _charBuf = ArrayPool<char>.Shared.Rent(4096);
        try
        {
            while (true)
            {
                ReadResult result = await _pipe.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                try
                {
                    // Process all complete lines in the current buffer segment(s).
                    while (TryReadLine(ref buffer, out ReadOnlyMemory<char> line))
                        yield return line;

                    if (result.IsCompleted)
                    {
                        // Flush any residual bytes as the final line (no trailing newline).
                        if (!buffer.IsEmpty)
                            yield return DecodeSegment(buffer);
                        break;
                    }
                }
                finally
                {
                    // Always call AdvanceTo after ReadAsync — even if no line was extracted.
                    // consumed = buffer.Start  → nothing consumed yet (partial line)
                    // examined = buffer.End    → prevents busy-loop (next ReadAsync blocks for new data)
                    _pipe.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(_charBuf);
            _charBuf = [];
            _pipeCompleted = true;
            await _pipe.CompleteAsync();
        }
    }

    /// <summary>
    /// Attempts to extract the next complete line (terminated by \n) from <paramref name="buffer"/>.
    /// On success, advances <paramref name="buffer"/> past the terminator and sets <paramref name="line"/>.
    /// Handles CRLF by back-trimming \r before decoding.
    /// Calls <see cref="EnsureCharBuf"/> before each decode to resize <see cref="_charBuf"/> if needed.
    /// </summary>
    private bool TryReadLine(
        ref ReadOnlySequence<byte> buffer,
        out ReadOnlyMemory<char> line)
    {
        // Search for the first \r or \n — we terminate on \n; \r is handled by back-trim.
        // Fast path: single-segment buffer (most common).
        if (buffer.IsSingleSegment)
        {
            ReadOnlySpan<byte> span = buffer.FirstSpan;
            int nlIdx = span.IndexOf((byte)'\n');

            if (nlIdx < 0)
            {
                // Check for bare \r terminator (old Mac CR-only).
                int crIdx = span.IndexOf((byte)'\r');
                if (crIdx < 0)
                {
                    line = default;
                    return false;
                }

                // CR-only: extract line up to \r, advance past it.
                ReadOnlySpan<byte> lineBytes = span[..crIdx];
                EnsureCharBuf(_encoding.GetMaxCharCount(lineBytes.Length));
                int charCount = _decoder.GetChars(lineBytes, _charBuf, flush: false);
                line = new ReadOnlyMemory<char>(_charBuf, 0, charCount);
                buffer = buffer.Slice(buffer.GetPosition(crIdx + 1));
                return true;
            }

            // LF found — back-trim \r if present (CRLF).
            int lineEnd = nlIdx;
            if (lineEnd > 0 && span[lineEnd - 1] == (byte)'\r')
                lineEnd--;

            ReadOnlySpan<byte> lineSpan = span[..lineEnd];
            EnsureCharBuf(_encoding.GetMaxCharCount(lineSpan.Length));
            int chars = _decoder.GetChars(lineSpan, _charBuf, flush: false);
            line = new ReadOnlyMemory<char>(_charBuf, 0, chars);
            buffer = buffer.Slice(buffer.GetPosition(nlIdx + 1));
            return true;
        }

        // Multi-segment path: use SequenceReader<byte> for efficient scanning.
        return TryReadLineMultiSegment(ref buffer, out line);
    }

    /// <summary>
    /// Multi-segment fallback for <see cref="TryReadLine"/>. Uses <see cref="SequenceReader{T}"/>
    /// to scan across segment boundaries.
    /// </summary>
    private bool TryReadLineMultiSegment(
        ref ReadOnlySequence<byte> buffer,
        out ReadOnlyMemory<char> line)
    {
        var reader = new SequenceReader<byte>(buffer);

        // Try \n first.
        if (reader.TryReadTo(out ReadOnlySequence<byte> lineSeq, (byte)'\n'))
        {
            // Back-trim \r if the sequence ends with it (CRLF split across segments).
            ReadOnlySequence<byte> trimmed = TrimTrailingCr(lineSeq);
            int chars = DecodeSequence(trimmed);
            line = new ReadOnlyMemory<char>(_charBuf, 0, chars);
            buffer = buffer.Slice(reader.Position);
            return true;
        }

        // Try bare \r (old Mac).
        reader = new SequenceReader<byte>(buffer);
        if (reader.TryReadTo(out ReadOnlySequence<byte> crLineSeq, (byte)'\r'))
        {
            int chars = DecodeSequence(crLineSeq);
            line = new ReadOnlyMemory<char>(_charBuf, 0, chars);
            buffer = buffer.Slice(reader.Position);
            return true;
        }

        line = default;
        return false;
    }

    /// <summary>
    /// Decodes the entire <paramref name="buffer"/> into <see cref="_charBuf"/> and returns
    /// the decoded content as <see cref="ReadOnlyMemory{T}"/>. Used for the final partial line.
    /// Resizes <see cref="_charBuf"/> via <see cref="EnsureCharBuf"/> if the byte count
    /// could produce more chars than the current buffer can hold.
    /// </summary>
    private ReadOnlyMemory<char> DecodeSegment(ReadOnlySequence<byte> buffer)
    {
        int chars = DecodeSequence(buffer);
        return new ReadOnlyMemory<char>(_charBuf, 0, chars);
    }

    /// <summary>
    /// Decodes a <see cref="ReadOnlySequence{T}"/> of bytes into <see cref="_charBuf"/>.
    /// Resizes <see cref="_charBuf"/> via <see cref="EnsureCharBuf"/> when the worst-case
    /// char count exceeds the current buffer length.
    /// Uses <c>FirstSpan</c> fast path for single-segment sequences; iterates segments for multi-segment.
    /// </summary>
    private int DecodeSequence(ReadOnlySequence<byte> seq)
    {
        if (seq.IsEmpty)
            return 0;

        if (seq.IsSingleSegment)
        {
            ReadOnlySpan<byte> span = seq.FirstSpan;
            EnsureCharBuf(_encoding.GetMaxCharCount(span.Length));
            return _decoder.GetChars(span, _charBuf, flush: false);
        }

        // Multi-segment: compute worst-case char count across all segments, resize once.
        int maxChars = _encoding.GetMaxCharCount((int)seq.Length);
        EnsureCharBuf(maxChars);

        int totalChars = 0;
        Span<char> remaining = _charBuf;
        foreach (ReadOnlyMemory<byte> segment in seq)
        {
            int got = _decoder.GetChars(segment.Span, remaining, flush: false);
            totalChars += got;
            remaining = remaining[got..];
        }
        return totalChars;
    }

    /// <summary>
    /// Ensures <see cref="_charBuf"/> can hold at least <paramref name="maxCharCount"/> chars.
    /// Returns the old buffer to <see cref="ArrayPool{T}"/> and rents a larger one if needed.
    /// </summary>
    private void EnsureCharBuf(int maxCharCount)
    {
        if (maxCharCount <= _charBuf.Length)
            return;
        if (_charBuf.Length > 0)
            ArrayPool<char>.Shared.Return(_charBuf);
        _charBuf = ArrayPool<char>.Shared.Rent(maxCharCount);
    }

    /// <summary>
    /// Returns the sequence with a trailing \r byte removed if present.
    /// Used to strip the \r from a CRLF pair when \r is at the end of a segment boundary.
    /// </summary>
    private static ReadOnlySequence<byte> TrimTrailingCr(ReadOnlySequence<byte> seq)
    {
        if (seq.IsEmpty)
            return seq;

        // Peek at the last byte — must materialize a SequenceReader from the end.
        byte last = seq.Slice(seq.Length - 1).FirstSpan[0];
        if (last == (byte)'\r')
            return seq.Slice(0, seq.Length - 1);

        return seq;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!_pipeCompleted)
            await _pipe.CompleteAsync();
    }
}
