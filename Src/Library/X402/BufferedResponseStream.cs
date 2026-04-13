namespace FastEndpoints;

sealed class BufferedResponseStream(Stream inner) : Stream
{
    readonly MemoryStream _buffer = new();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _buffer.Length;

    public override long Position
    {
        get => _buffer.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override Task FlushAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    public override void SetLength(long value)
        => _buffer.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
        => _buffer.Write(buffer, offset, count);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _buffer.WriteAsync(buffer, cancellationToken);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _buffer.WriteAsync(buffer, offset, count, cancellationToken);

    internal async Task CopyToInnerAsync(CancellationToken ct)
    {
        _buffer.Position = 0;
        await _buffer.CopyToAsync(inner, ct);
        await inner.FlushAsync(ct);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _buffer.Dispose();

        base.Dispose(disposing);
    }
}