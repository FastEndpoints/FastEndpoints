namespace FastEndpoints.Messaging.RabbitMQ;

sealed class AsyncResourcePool<TResource> : IAsyncDisposable where TResource : IAsyncDisposable
{
    readonly Stack<TResource> _available = new();
    readonly Func<CancellationToken, ValueTask<TResource>> _factory;
    readonly Func<TResource, bool> _isUsable;
    readonly SemaphoreSlim _slots;
    readonly CancellationTokenSource _shutdown = new();
    readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly object _sync = new();
    bool _disposing;
    int _leased;
    Task? _disposeTask;

    public AsyncResourcePool(int capacity,
                             Func<CancellationToken, ValueTask<TResource>> factory,
                             Func<TResource, bool>? isUsable = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _factory = factory;
        _isUsable = isUsable ?? (_ => true);
        _slots = new(capacity, capacity);
    }

    public async ValueTask<Lease> RentAsync(CancellationToken ct = default)
    {
        CancellationTokenSource? linkedCts = null;
        try
        {
            var waitToken = _shutdown.Token;
            if (ct.CanBeCanceled)
            {
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, waitToken);
                waitToken = linkedCts.Token;
            }
            await _slots.WaitAsync(waitToken);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(AsyncResourcePool<TResource>));
        }
        finally
        {
            linkedCts?.Dispose();
        }

        TResource? resource = default;
        List<TResource>? unusable = null;
        lock (_sync)
        {
            if (_disposing)
            {
                _slots.Release();
                throw new ObjectDisposedException(nameof(AsyncResourcePool<TResource>));
            }

            _leased++;
            while (_available.TryPop(out var candidate))
            {
                if (_isUsable(candidate))
                {
                    resource = candidate;
                    break;
                }
                (unusable ??= []).Add(candidate);
            }
        }

        if (unusable is not null)
        {
            try
            {
                foreach (var item in unusable)
                    await item.DisposeAsync();
            }
            catch
            {
                ReleaseSlot();
                throw;
            }
        }

        if (resource is not null)
            return new(this, resource);

        try
        {
            resource = await _factory(ct);
        }
        catch
        {
            ReleaseSlot();
            throw;
        }

        lock (_sync)
        {
            if (!_disposing)
                return new(this, resource);
        }

        try
        {
            await resource.DisposeAsync();
        }
        finally
        {
            ReleaseSlot();
        }
        throw new ObjectDisposedException(nameof(AsyncResourcePool<TResource>));
    }

    void ReleaseSlot()
    {
        var releaseSemaphore = false;
        lock (_sync)
        {
            _leased--;
            if (_disposing)
            {
                if (_leased == 0)
                    _drained.TrySetResult();
            }
            else
                releaseSemaphore = true;
        }
        if (releaseSemaphore)
            _slots.Release();
    }

    async ValueTask ReturnAsync(TResource resource)
    {
        var dispose = true;
        lock (_sync)
        {
            if (!_disposing && _isUsable(resource))
            {
                _available.Push(resource);
                _leased--;
                dispose = false;
            }
        }

        if (!dispose)
        {
            _slots.Release();
            return;
        }

        try
        {
            await resource.DisposeAsync();
        }
        finally
        {
            ReleaseSlot();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            if (_disposeTask is not null)
                return new(_disposeTask);

            _disposing = true;
            _shutdown.Cancel();
            var available = _available.ToArray();
            _available.Clear();
            if (_leased == 0)
                _drained.TrySetResult();
            _disposeTask = DisposeCoreAsync(available);

            return new(_disposeTask);
        }
    }

    async Task DisposeCoreAsync(TResource[] available)
    {
        Exception? exception = null;
        foreach (var resource in available)
        {
            try
            {
                await resource.DisposeAsync();
            }
            catch (Exception ex)
            {
                exception = exception is null ? ex : new AggregateException(exception, ex);
            }
        }

        try
        {
            await _drained.Task;
        }
        catch (Exception ex)
        {
            exception = exception is null ? ex : new AggregateException(exception, ex);
        }

        if (exception is not null)
            throw exception;
    }

    public sealed class Lease : IAsyncDisposable
    {
        AsyncResourcePool<TResource>? _owner;

        internal Lease(AsyncResourcePool<TResource> owner, TResource resource)
        {
            _owner = owner;
            Resource = resource;
        }

        public TResource Resource { get; }

        public ValueTask DisposeAsync()
        {
            var owner = Interlocked.Exchange(ref _owner, null);

            return owner is null ? ValueTask.CompletedTask : owner.ReturnAsync(Resource);
        }
    }
}
