using System.Collections.Concurrent;
using FakeItEasy;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace QueueTesting;

static class QueueTestSupport
{
    public static ServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(A.Fake<IHostApplicationLifetime>());
        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    public static ServerCallContext CreateServerCallContext(CancellationToken cancellationToken)
    {
        var context = A.Fake<ServerCallContext>();
        A.CallTo(context).WithReturnType<CancellationToken>().Returns(cancellationToken);

        return context;
    }

    public static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var timeoutAt = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < timeoutAt)
        {
            if (condition())
                return;

            await Task.Delay(50);
        }

        condition().ShouldBeTrue();
    }

    public static async Task WaitForCompletion(Task task, int timeoutMs = 3000)
    {
        await Task.WhenAny(task, Task.Delay(timeoutMs));
        task.IsCompleted.ShouldBeTrue();
        await task;
    }

    public static TaskCompletionSource NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static TaskCompletionSource<T> NewSignal<T>()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested)
        {
            if (condition())
                return true;

            try
            {
                await Task.Delay(25, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return condition();
    }
}

sealed record TestLogEntry(LogLevel Level, string Message, Exception? Exception);

sealed class TestLogger<T> : ILogger<T>
{
    readonly ConcurrentQueue<TestLogEntry> _entries = new();

    public TestLogEntry[] Entries => _entries.ToArray();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
        => NoopScope.Instance;

    public bool IsEnabled(LogLevel logLevel)
        => true;

    public void Log<TState>(LogLevel logLevel,
                            EventId eventId,
                            TState state,
                            Exception? exception,
                            Func<TState, Exception?, string> formatter)
        => _entries.Enqueue(new(logLevel, formatter(state, exception), exception));

    sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();

        public void Dispose() { }
    }
}

sealed class TestHostLifetime(CancellationToken appStoppingToken) : IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted => CancellationToken.None;
    public CancellationToken ApplicationStopping => appStoppingToken;
    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication() { }
}

class TestServerStreamWriter<T> : IServerStreamWriter<T>
{
    public WriteOptions? WriteOptions { get; set; }
    public List<T> Responses { get; } = new();

    public Task WriteAsync(T message)
    {
        Responses.Add(message);

        return Task.CompletedTask;
    }

    public Task WriteAsync(T message, CancellationToken ct)
        => WriteAsync(message);
}
