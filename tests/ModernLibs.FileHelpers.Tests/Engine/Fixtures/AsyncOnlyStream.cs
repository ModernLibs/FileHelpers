namespace ModernLibs.FileHelpers.Tests.Engine.Fixtures;

/// <summary>
/// Stream that allows only asynchronous I/O: every synchronous <c>Read</c>, <c>Write</c>, <c>Flush</c>,
/// <c>ReadByte</c> and <c>WriteByte</c> counts the attempt in <see cref="SyncCalls"/> and throws
/// <see cref="NotSupportedException"/>; the asynchronous members forward to the inner stream. Not seekable.
/// Disposing it sets <see cref="Disposed"/> and leaves the inner stream open, so a test can still inspect it.
/// </summary>
internal sealed class AsyncOnlyStream(Stream inner) : Stream
{
    /// <summary><see langword="true"/> once the stream was disposed.</summary>
    public bool Disposed { get; private set; }

    /// <summary>Number of synchronous I/O calls attempted (each one threw).</summary>
    public int SyncCalls { get; private set; }

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw SyncIo();

    public override int Read(Span<byte> buffer) => throw SyncIo();

    public override int ReadByte() => throw SyncIo();

    public override void Write(byte[] buffer, int offset, int count) => throw SyncIo();

    public override void Write(ReadOnlySpan<byte> buffer) => throw SyncIo();

    public override void WriteByte(byte value) => throw SyncIo();

    public override void Flush() => throw SyncIo();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        inner.ReadAsync(buffer, cancellationToken);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        inner.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        inner.WriteAsync(buffer, cancellationToken);

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }

    private NotSupportedException SyncIo()
    {
        SyncCalls++;
        return new NotSupportedException("sync I/O");
    }
}
