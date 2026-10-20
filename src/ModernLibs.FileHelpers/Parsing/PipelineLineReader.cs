using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;

namespace ModernLibs.FileHelpers.Parsing;

/// <summary>
/// <see cref="PipeReader"/>-backed async line reader. Bytes are decoded to characters first (with a
/// stateful <see cref="Decoder"/>) and the characters are then split into lines, so multi-byte encodings
/// (UTF-8, UTF-16, UTF-32) and terminators split across buffer boundaries are handled correctly.
/// </summary>
/// <remarks>
/// <para>Line splitting is identical to <see cref="SpanLineReader"/>: LF, CRLF and bare CR end a line;
/// a trailing segment without a terminator is still yielded when non-empty.</para>
/// <para>A byte order mark (UTF-8, UTF-16 LE/BE, UTF-32 LE/BE) is detected and stripped like
/// <see cref="StreamReader"/> with <c>detectEncodingFromByteOrderMarks: true</c>: the detected encoding wins
/// over the configured one unless both have the same code page (then the configured instance and its
/// fallback behaviour are kept).</para>
/// <para>
/// <strong>Lifetime contract:</strong> the <see cref="ReadOnlyMemory{T}"/> yielded by
/// <see cref="ReadLinesAsync"/> references an internal pooled buffer and is only valid until the next
/// iteration. Callers must not cache it.
/// </para>
/// <para>
/// <strong>Encoding errors:</strong> the decoder is flushed at the end of input, so a truncated trailing
/// sequence is decoded with the encoding's fallback: <see cref="Encoding.UTF8"/> yields U+FFFD, an encoding
/// configured with <see cref="DecoderExceptionFallback"/> throws <see cref="DecoderFallbackException"/>.
/// </para>
/// </remarks>
internal sealed class PipelineLineReader : IAsyncDisposable
{
    private const int MinimumFill = 4096;
    private const int DecodeHeadroom = 16;

    private readonly PipeReader _pipe;
    private readonly Encoding _encoding;
    private readonly CharLineBuffer _lines = new(2 * MinimumFill);
    private Decoder? _decoder;
    private bool _pipeCompleted;

    /// <summary>
    /// Initializes a new <see cref="PipelineLineReader"/> that reads from <paramref name="stream"/>.
    /// The stream is NOT owned by this reader: disposing the reader leaves it open (the caller disposes it).
    /// </summary>
    /// <param name="stream">Source byte stream. Must be readable.</param>
    /// <param name="encoding">Character encoding used when the stream has no byte order mark.</param>
    public PipelineLineReader(Stream stream, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(encoding);

        _pipe = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
        _encoding = encoding;
    }

    /// <summary>Line read by the last successful <see cref="ReadLineAsync"/>.</summary>
    internal ReadOnlyMemory<char> CurrentLine { get; private set; }

    /// <summary>Terminator of <see cref="CurrentLine"/>.</summary>
    internal LineEnding CurrentEnding { get; private set; }

    /// <summary>
    /// Asynchronously enumerates lines from the underlying stream. Each <see cref="ReadOnlyMemory{T}"/>
    /// contains one decoded line with the line terminator stripped.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel iteration.</param>
    /// <returns>An async sequence of decoded lines.</returns>
    public async IAsyncEnumerable<ReadOnlyMemory<char>> ReadLinesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            while (await ReadLineAsync(cancellationToken).ConfigureAwait(false))
                yield return CurrentLine;
        }
        finally
        {
            await CompletePipeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await CompletePipeAsync().ConfigureAwait(false);
        _lines.Dispose();
    }

    /// <summary>
    /// Reads the next line into <see cref="CurrentLine"/>/<see cref="CurrentEnding"/>;
    /// <see langword="false"/> at end of input.
    /// </summary>
    internal ValueTask<bool> ReadLineAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TryTakeCurrent())
            return new ValueTask<bool>(true);
        return _lines.EndOfInput ? new ValueTask<bool>(false) : ReadLineSlowAsync(cancellationToken);
    }

    private async ValueTask<bool> ReadLineSlowAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await FillAsync(cancellationToken).ConfigureAwait(false);
            if (TryTakeCurrent())
                return true;
            if (_lines.EndOfInput)
                return false;
        }
    }

    private bool TryTakeCurrent()
    {
        if (!_lines.TryTakeLine(out int start, out int length, out var ending))
            return false;

        CurrentLine = new ReadOnlyMemory<char>(_lines.Array, start, length);
        CurrentEnding = ending;
        return true;
    }

    /// <summary>Decodes the next available bytes into the line buffer (at least one char or end of input).</summary>
    private async ValueTask FillAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            ReadResult result = await _pipe.ReadAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlySequence<byte> buffer = result.Buffer;

            if (_decoder is null)
            {
                if (buffer.Length < 4 && !result.IsCompleted)
                {
                    // Need up to four bytes to recognise a byte order mark.
                    _pipe.AdvanceTo(buffer.Start, buffer.End);
                    continue;
                }

                buffer = buffer.Slice(SelectDecoder(buffer));
            }

            int produced = Decode(ref buffer, out bool allConsumed);
            _lines.Commit(produced);
            bool finished = false;
            if (allConsumed && result.IsCompleted)
            {
                // Flush the decoder so a truncated trailing sequence goes through the fallback
                // (U+FFFD or DecoderFallbackException) instead of being dropped.
                var tail = _lines.GetWritableSpan(DecodeHeadroom);
                _decoder!.Convert(ReadOnlySpan<byte>.Empty, tail, flush: true, out _, out int flushed, out _);
                _lines.Commit(flushed);
                produced += flushed;
                finished = true;
            }

            _pipe.AdvanceTo(buffer.Start, allConsumed ? buffer.End : buffer.Start);

            if (finished)
            {
                _lines.MarkEndOfInput();
                return;
            }

            if (produced > 0)
                return;
        }
    }

    /// <summary>Decodes as much of <paramref name="buffer"/> as fits; advances it past the consumed bytes.</summary>
    private int Decode(ref ReadOnlySequence<byte> buffer, out bool allConsumed)
    {
        var destination = _lines.GetWritableSpan(MinimumFill);
        int produced = 0;
        while (!buffer.IsEmpty && destination.Length - produced >= DecodeHeadroom)
        {
            ReadOnlySpan<byte> segment = buffer.FirstSpan;
            _decoder!.Convert(segment, destination[produced..], flush: false, out int bytesUsed, out int charsUsed, out _);
            produced += charsUsed;
            buffer = buffer.Slice(bytesUsed);
            if (bytesUsed < segment.Length)
                break; // output full
        }

        allConsumed = buffer.IsEmpty;
        return produced;
    }

    /// <summary>Chooses the decoder from a byte order mark; returns the number of BOM bytes to skip.</summary>
    private int SelectDecoder(ReadOnlySequence<byte> buffer)
    {
        Span<byte> head = stackalloc byte[4];
        int available = (int)Math.Min(4, buffer.Length);
        buffer.Slice(0, available).CopyTo(head);
        head = head[..available];

        Encoding? detected = null;
        int bomLength = 0;
        if (head is [0xEF, 0xBB, 0xBF, ..])
        {
            detected = Encoding.UTF8;
            bomLength = 3;
        }
        else if (head is [0xFF, 0xFE, 0x00, 0x00])
        {
            detected = Encoding.UTF32;
            bomLength = 4;
        }
        else if (head is [0x00, 0x00, 0xFE, 0xFF])
        {
            detected = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
            bomLength = 4;
        }
        else if (head is [0xFF, 0xFE, ..])
        {
            detected = Encoding.Unicode;
            bomLength = 2;
        }
        else if (head is [0xFE, 0xFF, ..])
        {
            detected = Encoding.BigEndianUnicode;
            bomLength = 2;
        }

        var encoding = detected is null || detected.CodePage == _encoding.CodePage ? _encoding : detected;
        _decoder = encoding.GetDecoder();
        return bomLength;
    }

    private async ValueTask CompletePipeAsync()
    {
        if (_pipeCompleted)
            return;
        _pipeCompleted = true;
        await _pipe.CompleteAsync().ConfigureAwait(false);
    }
}
