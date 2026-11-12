using System.Buffers;
using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Target of <c>WriteStreamAsync</c>: each block of whole records goes to the caller's <see cref="TextWriter"/> with
/// one <see cref="TextWriter.WriteAsync(ReadOnlyMemory{char}, CancellationToken)"/> call, or is encoded by one
/// <see cref="Encoder"/> and goes to the caller's <see cref="Stream"/> with one
/// <see cref="Stream.WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/> call. A cancellation the engine observes
/// therefore falls between blocks, never inside a record. Neither the writer nor the stream is disposed.
/// </summary>
/// <remarks>
/// The stream output is the bytes a <see cref="StreamWriter"/> writes: the encoding's preamble first (also for an
/// empty output), unless the stream is seekable and not at position 0.
/// </remarks>
internal sealed class AsyncBlockWriter : IDisposable
{
    private readonly TextWriter? _writer;
    private readonly Stream? _stream;
    private readonly Encoding? _encoding;
    private readonly Encoder? _encoder;
    private byte[]? _bytes;
    private bool _preambleWritten;

    /// <summary>Blocks go to <paramref name="writer"/> unchanged.</summary>
    public AsyncBlockWriter(TextWriter writer)
    {
        _writer = writer;
    }

    /// <summary>Blocks are encoded with <paramref name="encoding"/> and go to <paramref name="stream"/>.</summary>
    public AsyncBlockWriter(Stream stream, Encoding encoding)
    {
        _stream = stream;
        _encoding = encoding;
        _encoder = encoding.GetEncoder();

        // As StreamWriter: no preamble when writing on in a seekable stream that already holds data.
        _preambleWritten = stream.CanSeek && stream.Position > 0;
    }

    /// <summary>
    /// Writes <paramref name="block"/>; <paramref name="final"/> marks the last block of the write (it flushes the
    /// encoder, and writes the preamble even when nothing else is written). For a stream the token is checked first,
    /// so nothing, not even the preamble, is written after a cancellation.
    /// </summary>
    public async Task WriteAsync(ReadOnlyMemory<char> block, bool final, CancellationToken cancellationToken)
    {
        if (_writer is not null)
        {
            if (!block.IsEmpty)
                await _writer.WriteAsync(block, cancellationToken).ConfigureAwait(false);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        int count = Encode(block.Span, final);
        if (count > 0)
            await _stream!.WriteAsync(_bytes.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Flushes the writer or the stream asynchronously.</summary>
    public Task FlushAsync(CancellationToken cancellationToken) =>
        _writer is not null ? _writer.FlushAsync(cancellationToken) : _stream!.FlushAsync(cancellationToken);

    public void Dispose()
    {
        if (_bytes is not null)
            ArrayPool<byte>.Shared.Return(_bytes);
        _bytes = null;
    }

    /// <summary>Encodes the preamble (once) and <paramref name="chars"/> into <see cref="_bytes"/>; returns the byte count.</summary>
    private int Encode(ReadOnlySpan<char> chars, bool final)
    {
        if (chars.IsEmpty && !final)
            return 0;

        ReadOnlySpan<byte> preamble = _preambleWritten ? default : _encoding!.Preamble;
        int maxBytes = preamble.Length + _encoding!.GetMaxByteCount(chars.Length);
        if (_bytes is null || _bytes.Length < maxBytes)
        {
            if (_bytes is not null)
                ArrayPool<byte>.Shared.Return(_bytes);
            _bytes = ArrayPool<byte>.Shared.Rent(maxBytes);
        }

        preamble.CopyTo(_bytes);
        int count = preamble.Length + _encoder!.GetBytes(chars, _bytes.AsSpan(preamble.Length), flush: final);
        _preambleWritten = true;
        return count;
    }
}
