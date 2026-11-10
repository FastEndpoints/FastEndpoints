using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

sealed class EventHubRuntime(IHostApplicationLifetime lifetime, ILogger<EventHubRuntime> logger) : IHostedService
{
    readonly Lock _lock = new();
    readonly List<EventHubBase> _hubs = [];
    readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    bool _stopping;
    int _pending;

    internal void Register(EventHubBase hub)
    {
        lock (_lock)
        {
            if (_stopping)
                throw new InvalidOperationException("The event hub is stopping.");

            _hubs.Add(hub);
        }
    }

    internal void Broadcast<TState>(TState state, Func<TState, Task> broadcast)
    {
        lock (_lock)
        {
            if (_stopping || lifetime.ApplicationStopping.IsCancellationRequested)
                throw new InvalidOperationException("The event hub is stopping and cannot accept new events.");

            _pending++;
        }

        _ = Execute(state, broadcast);
    }

    async Task Execute<TState>(TState state, Func<TState, Task> broadcast)
    {
        try
        {
            await broadcast(state);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Event hub broadcast failed.");
        }
        finally
        {
            lock (_lock)
            {
                if (--_pending == 0 && _stopping)
                    _drained.TrySetResult();
            }
        }
    }

    //ct passed in here is useless. it's a default token: https://source.dot.net/#Microsoft.AspNetCore.Hosting/Internal/WebHost.cs,124
    public Task StartAsync(CancellationToken _)
    {
        EventHubBase[] hubs;

        lock (_lock)
            hubs = _hubs.ToArray();

        return Task.WhenAll(hubs.Select(hub => hub.InitializeIfNeeded()));
    }

    public async Task StopAsync(CancellationToken ct)
    {
        lock (_lock)
        {
            _stopping = true;

            if (_pending == 0)
                _drained.TrySetResult();
        }

        // A deadline cannot abort a durable provider call. Keep its dependencies alive until it returns.
        using var registration = ct.Register(() =>
        {
            if (!_drained.Task.IsCompleted)
                logger.LogWarning("Event hub shutdown deadline exceeded. Waiting for pending broadcasts before releasing storage dependencies.");
        });

        await _drained.Task;

        foreach (var hub in _hubs)
            hub.Unregister();
    }
}
