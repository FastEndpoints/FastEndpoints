using System.Collections.Concurrent;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QueueTesting;
using Xunit;
using static QueueTesting.QueueTestSupport;

namespace EventQueue;

public partial class EventQueueTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task broadcast_shutdown_wakes_wait_and_persists_final_recipients(bool ack)
        => ack ? ShutdownWaitingBroadcast<ShutdownAckStorage>() : ShutdownWaitingBroadcast<ShutdownStorage>();

    static async Task ShutdownWaitingBroadcast<TStorage>() where TStorage : class, IEventHubStorageProvider<TestEventRecord>
    {
        var state = new ShutdownState();
        using var host = CreateBroadcastHost<WaitingShutdownEvent, TStorage>(state);
        await host.StartAsync();

        // Broadcast runs synchronously up to the no-subscriber delay.
        new WaitingShutdownEvent().Broadcast();
        state.Attempts.ShouldBe(0);
        EventHub<WaitingShutdownEvent, TestEventRecord, TStorage>.Configure(HubMode.EventPublisher, ["final-recipient"]);

        var stop = host.StopAsync();
        try
        {
            await state.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            stop.IsCompleted.ShouldBeFalse();
            host.Services.GetRequiredService<ShutdownDependency>().Disposed.ShouldBeFalse();
        }
        finally
        {
            state.Release.TrySetResult();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
        }

        state.Rows.ShouldHaveSingleItem().SubscriberID.ShouldBe("final-recipient");
        state.TokenCanBeCanceled.ShouldBeFalse();
    }

    [Fact]
    public async Task broadcast_shutdown_with_no_recipients_finishes_without_storage()
    {
        var state = new ShutdownState();
        using var host = CreateBroadcastHost<NoRecipientShutdownEvent, ShutdownStorage>(state);
        await host.StartAsync();
        new NoRecipientShutdownEvent().Broadcast();

        await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));

        state.Attempts.ShouldBe(0);
        Should.Throw<InvalidOperationException>(() => new NoRecipientShutdownEvent().Broadcast());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task broadcast_shutdown_keeps_dependencies_alive_past_deadline(bool ack)
        => ack ? ShutdownActiveWrite<ShutdownAckStorage>() : ShutdownActiveWrite<ShutdownStorage>();

    static async Task ShutdownActiveWrite<TStorage>() where TStorage : class, IEventHubStorageProvider<TestEventRecord>
    {
        var state = new ShutdownState();
        using var host = CreateBroadcastHost<ActiveShutdownEvent, TStorage>(state, "active");
        await host.StartAsync();
        var dependency = host.Services.GetRequiredService<ShutdownDependency>();
        new ActiveShutdownEvent().Broadcast();
        await state.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var deadline = new CancellationTokenSource();
        var stop = host.StopAsync(deadline.Token);
        deadline.Cancel();
        try
        {
            stop.IsCompleted.ShouldBeFalse();
            dependency.Disposed.ShouldBeFalse();
            state.Messages.ShouldContain(message => message.Contains("shutdown deadline exceeded"));
            Should.Throw<InvalidOperationException>(() => new ActiveShutdownEvent().Broadcast());
        }
        finally
        {
            state.Release.TrySetResult();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
        }

        state.Rows.ShouldHaveSingleItem();
        state.TokenCanBeCanceled.ShouldBeFalse();
        host.Dispose();
        dependency.Disposed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task broadcast_shutdown_reports_failed_write_without_retrying(bool waitingForRetry)
    {
        var state = new ShutdownState { Fail = true };
        using var host = CreateBroadcastHost<FailedShutdownEvent, ShutdownStorage>(state, "failed");
        await host.StartAsync();

        if (waitingForRetry)
            state.Release.SetResult();

        new FailedShutdownEvent().Broadcast();
        await state.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = host.StopAsync();
        state.Release.TrySetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(2));

        state.Attempts.ShouldBe(1);
        state.Rows.ShouldBeEmpty();
        state.Messages.ShouldContain(message => message.Contains("write failed"));
        state.Messages.ShouldNotContain(message => message.Contains("Retrying"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task broadcast_callback_failure_is_logged_and_drained(int failureMode)
    {
        var logger = new TestLogger<EventHubRuntime>();
        var runtime = new EventHubRuntime(new TestHostLifetime(CancellationToken.None), logger);
        var failure = new IOException("broadcast failed");
        var failed = NewSignal();
        var release = NewSignal();
        runtime.Broadcast(release.Task, static task => task);

        runtime.Broadcast((failureMode, failure, failed.Task), static state => state.failureMode switch
        {
            0 => throw state.failure,
            1 => Task.FromException(state.failure),
            _ => state.Task
        });

        var stop = runtime.StopAsync(CancellationToken.None);
        try
        {
            stop.IsCompleted.ShouldBeFalse();
            if (failureMode == 2)
                failed.SetException(failure);
            await WaitUntil(() => logger.Entries.Length == 1);
            // A failed broadcast must not decrement twice and release the other broadcast.
            stop.IsCompleted.ShouldBeFalse();
            var entry = logger.Entries.ShouldHaveSingleItem();
            entry.Level.ShouldBe(LogLevel.Error);
            entry.Exception.ShouldBeSameAs(failure);
            entry.Message.ShouldBe("Event hub broadcast failed.");
        }
        finally
        {
            release.TrySetResult();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
        }

        var invoked = false;
        Should.Throw<InvalidOperationException>(() => runtime.Broadcast(0, _ =>
        {
            invoked = true;
            return Task.CompletedTask;
        }));
        invoked.ShouldBeFalse();
    }

    [Fact]
    public async Task broadcast_admission_racing_shutdown_is_drained_or_rejected()
    {
        var runtime = new EventHubRuntime(new TestHostLifetime(CancellationToken.None), NullLogger<EventHubRuntime>.Instance);
        var release = NewSignal();
        var start = NewSignal();
        var admitted = 0;
        var rejected = 0;
        var completed = 0;
        // Ensure the drain has work even if shutdown wins every racing admission.
        runtime.Broadcast(release.Task, Work);
        var publishers = Enumerable.Range(0, 100).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            try { runtime.Broadcast(release.Task, Work); }
            catch (InvalidOperationException) { Interlocked.Increment(ref rejected); }
        })).ToArray();
        var stop = Task.Run(async () =>
        {
            await start.Task;
            await runtime.StopAsync(CancellationToken.None);
        });
        start.SetResult();
        try
        {
            await Task.WhenAll(publishers).WaitAsync(TimeSpan.FromSeconds(5));
            stop.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            release.TrySetResult();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
        }

        (admitted + rejected).ShouldBe(101);
        completed.ShouldBe(admitted);
        Should.Throw<InvalidOperationException>(() => runtime.Broadcast(release.Task, Work));

        async Task Work(Task completion)
        {
            Interlocked.Increment(ref admitted);
            await completion;
            Interlocked.Increment(ref completed);
        }
    }

    [Fact]
    public async Task broadcast_hosts_do_not_drain_each_other_or_reuse_disposed_storage()
    {
        var first = new ShutdownState();
        using var oldHost = CreateBroadcastHost<RecreatedShutdownEvent, ShutdownStorage>(first, "recreated");
        await oldHost.StartAsync();
        new RecreatedShutdownEvent().Broadcast();
        await first.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var second = new ShutdownState();
        using var newHost = CreateBroadcastHost<RecreatedShutdownEvent, ShutdownStorage>(second, "recreated");
        await newHost.StartAsync();
        new RecreatedShutdownEvent().Broadcast();
        await second.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            // Broker ingress must stay on its owning host even when the global publisher points to the new host.
            var oldHub = oldHost.Services.GetRequiredService<EventHub<RecreatedShutdownEvent, TestEventRecord, ShutdownStorage>>();
            await oldHub.OnEventReceived(oldHub, new(), CreateServerCallContext(CancellationToken.None));
            first.Release.SetResult();
            await oldHost.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
            oldHost.Dispose();
            // Old-host cleanup must not unregister the replacement hub or wait for its write.
            new RecreatedShutdownEvent().Broadcast();
        }
        finally
        {
            first.Release.TrySetResult();
            second.Release.TrySetResult();
            await newHost.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        first.Rows.Count.ShouldBe(2);
        second.Rows.Count.ShouldBe(2);
        second.Messages.ShouldNotContain(message => message.Contains("disposed", StringComparison.OrdinalIgnoreCase));
    }

    static IHost CreateBroadcastHost<TEvent, TStorage>(ShutdownState state, params string[] recipients)
        where TEvent : class, IEvent
        where TStorage : class, IEventHubStorageProvider<TestEventRecord>
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new ShutdownLoggerProvider(state));
        builder.Services.AddSingleton(state);
        builder.Services.AddSingleton<ShutdownDependency>();
        builder.Services.AddHandlerServer();
        var host = builder.Build();
        EventHub<TEvent, TestEventRecord, TStorage>.Configure(HubMode.EventPublisher, recipients);
        host.Services.GetRequiredService<EventHub<TEvent, TestEventRecord, TStorage>>();

        return host;
    }

    sealed class WaitingShutdownEvent : IEvent;
    sealed class NoRecipientShutdownEvent : IEvent;
    sealed class ActiveShutdownEvent : IEvent;
    sealed class FailedShutdownEvent : IEvent;
    sealed class RecreatedShutdownEvent : IEvent;

    sealed class ShutdownState
    {
        public TaskCompletionSource Entered { get; } = NewSignal();
        public TaskCompletionSource Release { get; } = NewSignal();
        public ConcurrentBag<TestEventRecord> Rows { get; } = [];
        public ConcurrentBag<string> Messages { get; } = [];
        public int Attempts;
        public bool TokenCanBeCanceled;
        public bool Fail;
    }

    sealed class ShutdownDependency : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Check() => ObjectDisposedException.ThrowIf(Disposed, this);
        public void Dispose() => Disposed = true;
    }

    class ShutdownStorage(ShutdownState state, ShutdownDependency dependency) : IEventHubStorageProvider<TestEventRecord>
    {
        public ValueTask<IEnumerable<string>> RestoreSubscriberIDsForEventTypeAsync(SubscriberIDRestorationParams<TestEventRecord> parameters)
        {
            dependency.Check();
            return new(Array.Empty<string>());
        }

        public async ValueTask StoreEventsAsync(IEnumerable<TestEventRecord> records, CancellationToken ct)
        {
            dependency.Check();
            Interlocked.Increment(ref state.Attempts);
            state.TokenCanBeCanceled = ct.CanBeCanceled;
            state.Entered.TrySetResult();
            await state.Release.Task;
            dependency.Check();
            if (state.Fail)
                throw new IOException("write failed");
            foreach (var record in records)
                state.Rows.Add(record);
        }

        public ValueTask<IEnumerable<TestEventRecord>> GetNextBatchAsync(PendingRecordSearchParams<TestEventRecord> parameters)
            => new(Array.Empty<TestEventRecord>());
        public ValueTask MarkEventAsCompleteAsync(TestEventRecord record, CancellationToken ct) => default;
        public ValueTask PurgeStaleRecordsAsync(StaleRecordSearchParams<TestEventRecord> parameters) => default;
    }

    sealed class ShutdownAckStorage(ShutdownState state, ShutdownDependency dependency)
        : ShutdownStorage(state, dependency), IEventHubDeliveryAck<TestEventRecord>;

    sealed class ShutdownLoggerProvider(ShutdownState state) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ShutdownLogger(state);
        public void Dispose() { }
    }

    sealed class ShutdownLogger(ShutdownState state) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState value) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState value, Exception? exception, Func<TState, Exception?, string> formatter)
            => state.Messages.Add(formatter(value, exception));
    }
}
