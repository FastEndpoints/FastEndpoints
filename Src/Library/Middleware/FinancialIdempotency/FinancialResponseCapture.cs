using System.IO.Pipelines;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;

namespace FastEndpoints;

sealed class FinancialResponseCapture : IHttpResponseFeature, IHttpResponseBodyFeature, IAsyncDisposable
{
    readonly IHttpResponseFeature _original;
    readonly Stack<(Func<object, Task> Callback, object State)> _starting = new();
    readonly FileBufferingWriteStream _buffer;
    readonly StreamResponseBodyFeature _body;
    bool _startingNow;
    bool _completed;
    internal bool Failed { get; private set; }
    int _statusCode;
    string? _reasonPhrase;
    IHeaderDictionary _headers = new HeaderDictionary();

    internal FinancialResponseCapture(IHttpResponseFeature original, long limit)
    {
        _original = original;
        _statusCode = original.StatusCode;
        ReasonPhrase = original.ReasonPhrase;
        Headers = new HeaderDictionary();
        foreach (var header in original.Headers)
            Headers[header.Key] = header.Value;
        _buffer = new(memoryThreshold: (int)Math.Min(32768, limit), bufferLimit: limit);
        _body = new(new CaptureStream(this));
    }

    public int StatusCode
    {
        get => _statusCode;
        set
        {
            EnsureNotStarted();
            _statusCode = value;
        }
    }

    public string? ReasonPhrase
    {
        get => _reasonPhrase;
        set
        {
            EnsureNotStarted();
            _reasonPhrase = value;
        }
    }

    public IHeaderDictionary Headers
    {
        get => _headers;
        set
        {
            EnsureNotStarted();
            _headers = value;
        }
    }

    void EnsureNotStarted()
    {
        if (HasStarted)
            throw new InvalidOperationException("The response has already started.");
    }

    public bool HasStarted { get; private set; }

    public Stream Body
    {
        get => Stream;
        set => throw new NotSupportedException("Replacing the captured response stream is unsupported.");
    }

    public Stream Stream => _body.Stream;
    public PipeWriter Writer => _body.Writer;

    public void OnStarting(Func<object, Task> callback, object state)
    {
        EnsureNotStarted();
        _starting.Push((callback, state));
    }

    public void OnCompleted(Func<object, Task> callback, object state)
        => _original.OnCompleted(callback, state);

    public void DisableBuffering()
    {
        Failed = true;

        throw new NotSupportedException("Financial idempotency requires bounded response buffering.");
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (HasStarted || _startingNow)
            return;

        _startingNow = true;

        try
        {
            while (_starting.TryPop(out var callback))
                await callback.Callback(callback.State);
            HasStarted = true;
            if (Headers is HeaderDictionary headers)
                headers.IsReadOnly = true;
        }
        catch
        {
            Failed = true;

            throw;
        }
        finally
        {
            _startingNow = false;
        }
    }

    public async Task CompleteAsync()
    {
        if (_completed)
            return;

        await StartAsync();
        await _body.CompleteAsync();
        _completed = true;
    }

    public async Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default)
    {
        await using var file = File.OpenRead(path);

        if (offset < 0 || offset > file.Length || count < 0 || count > file.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(offset));

        file.Position = offset;
        var remaining = count ?? file.Length - offset;
        var bytes = new byte[65536];

        while (remaining > 0)
        {
            var read = await file.ReadAsync(bytes.AsMemory(0, (int)Math.Min(bytes.Length, remaining)), cancellationToken);

            if (read == 0)
                throw new EndOfStreamException();

            await Stream.WriteAsync(bytes.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }
    }

    internal async Task<byte[]> SnapshotAsync()
    {
        await CompleteAsync();

        if (Failed)
            throw new IOException("Financial response capture failed.");

        var bytes = new byte[checked((int)_buffer.Length)];
        using var target = new MemoryStream(bytes, writable: true);
        await _buffer.DrainBufferAsync(target);

        return bytes;
    }

    public ValueTask DisposeAsync()
        => _buffer.DisposeAsync();

    sealed class CaptureStream(FinancialResponseCapture owner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => owner._buffer.Length;

        public override long Position
        {
            get => Length;
            set => throw new NotSupportedException();
        }

        public override void Flush()
            => owner.StartAsync().GetAwaiter().GetResult();

        public override Task FlushAsync(CancellationToken cancellationToken)
            => owner.StartAsync(cancellationToken);

        public override void Write(byte[] buffer, int offset, int count)
        {
            Flush();

            if (owner._completed)
                throw new InvalidOperationException("The response is complete.");

            try { owner._buffer.Write(buffer, offset, count); }
            catch
            {
                owner.Failed = true;

                throw;
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await owner.StartAsync(cancellationToken);

            if (owner._completed)
                throw new InvalidOperationException("The response is complete.");

            try { await owner._buffer.WriteAsync(buffer, cancellationToken); }
            catch
            {
                owner.Failed = true;

                throw;
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();
    }
}