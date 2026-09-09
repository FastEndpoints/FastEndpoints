using System.Collections.Concurrent;
using FastEndpoints;
using FastEndpoints.Messaging.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Shouldly;
using Xunit;

namespace Integration.Messaging.RabbitMQ;

public class RabbitMQTransportTests
{
    [Fact]
    [Trait("ExcludeInCiCd", "Yes")]
    public async Task Delivers_Typed_Command_And_Event()
    {
        var connectionString = ConnectionString();
        var id = Guid.NewGuid().ToString("N");
        var probe = new DeliveryProbe();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(probe);
        builder.Services.AddRabbitMQMessaging(
                   o =>
                   {
                       o.ConnectionString = connectionString;
                       o.TopologyPrefix = $"fe-tests-{id}";
                   })
               .AddCommand<TestCommand, TestCommandHandler>()
               .AddEvent<TestEvent, TestEventHandler>()
               .AddEvent<TestEvent, SecondTestEventHandler>();

        var host = builder.Build();
        await using var hostDisposer = (IAsyncDisposable)host;
        await host.StartAsync();
        var publisher = host.Services.GetRequiredService<IRabbitMQPublisher>();
        await publisher.SendAsync(new TestCommand(id));
        await publisher.PublishAsync(new TestEvent(id));

        (await probe.Command.Task.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(id);
        (await probe.Event.Task.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(id);
        (await probe.SecondEvent.Task.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(id);
        await host.StopAsync();
    }

    [Fact]
    [Trait("ExcludeInCiCd", "Yes")]
    public async Task Publishes_Concurrently_With_A_Bounded_Channel_Pool()
    {
        var connectionString = ConnectionString();
        var id = Guid.NewGuid().ToString("N");
        const int messageCount = 32;
        var probe = new ConcurrentDeliveryProbe(messageCount);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(probe);
        builder.Services.AddRabbitMQMessaging(
                   o =>
                   {
                       o.ConnectionString = connectionString;
                       o.TopologyPrefix = $"fe-tests-concurrent-{id}";
                       o.PublisherConcurrency = 4;
                   })
               .AddCommand<ConcurrentCommand, ConcurrentCommandHandler>(o => o.Concurrency = 4);

        var host = builder.Build();
        await using var hostDisposer = (IAsyncDisposable)host;
        await host.StartAsync();
        var publisher = host.Services.GetRequiredService<IRabbitMQPublisher>();

        await Task.WhenAll(Enumerable.Range(0, messageCount).Select(number => publisher.SendAsync(new ConcurrentCommand(number))));
        await probe.AllDelivered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        probe.Delivered.Count.ShouldBe(messageCount);
        await host.StopAsync();
    }

    [Fact]
    [Trait("ExcludeInCiCd", "Yes")]
    public async Task Nacks_Failed_Command_Without_Requeue_By_Default()
    {
        var connectionString = ConnectionString();
        var id = Guid.NewGuid().ToString("N");
        var queue = $"fe-tests-failure-{id}";
        var probe = new DeliveryProbe();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(probe);
        builder.Services.AddRabbitMQMessaging(o => o.ConnectionString = connectionString)
               .AddCommand<FailingCommand, FailingCommandHandler>(o => o.Queue = queue);

        var host = builder.Build();
        await using var hostDisposer = (IAsyncDisposable)host;
        await host.StartAsync();
        await host.Services.GetRequiredService<IRabbitMQPublisher>().SendAsync(new FailingCommand(id));
        (await probe.Command.Task.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(id);

        var factory = new ConnectionFactory { Uri = new(connectionString) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await WaitForEmptyQueue(channel, queue);
        await host.StopAsync();
    }

    [Theory]
    [InlineData(RabbitQueueType.Classic)]
    [InlineData(RabbitQueueType.Quorum)]
    [InlineData(RabbitQueueType.Stream)]
    [Trait("ExcludeInCiCd", "Yes")]
    public async Task Supports_All_RabbitMQ_Queue_Types(RabbitQueueType queueType)
    {
        var connectionString = ConnectionString();
        var id = Guid.NewGuid().ToString("N");
        var probe = new DeliveryProbe();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(probe);
        builder.Services.AddRabbitMQMessaging(
                   o =>
                   {
                       o.ConnectionString = connectionString;
                       o.TopologyPrefix = $"fe-tests-types-{id}";
                   })
               .AddCommand<TestCommand, TestCommandHandler>(
                   o =>
                   {
                       o.QueueType = queueType;
                       if (queueType == RabbitQueueType.Stream)
                           o.ConsumerArguments["x-stream-offset"] = "next";
                   });

        var host = builder.Build();
        await using var hostDisposer = (IAsyncDisposable)host;
        await host.StartAsync();
        await host.Services.GetRequiredService<IRabbitMQPublisher>().SendAsync(new TestCommand(id));
        (await probe.Command.Task.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(id);
        await host.StopAsync();
    }

    static string ConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("RABBITMQ_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(value))
            Assert.Skip("Set RABBITMQ_CONNECTION_STRING to run RabbitMQ broker integration tests.");

        return value;
    }

    static async Task WaitForEmptyQueue(IChannel channel, string queue)
    {
        for (var i = 0; i < 50; i++)
        {
            if ((await channel.QueueDeclarePassiveAsync(queue)).MessageCount == 0)
                return;
            await Task.Delay(100);
        }

        throw new TimeoutException($"Queue [{queue}] was not emptied by NACK.");
    }
}

sealed class DeliveryProbe
{
    public TaskCompletionSource<string> Command { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<string> Event { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<string> SecondEvent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

sealed class ConcurrentDeliveryProbe(int expectedCount)
{
    int _delivered;

    public ConcurrentDictionary<int, byte> Delivered { get; } = new();
    public TaskCompletionSource AllDelivered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Add(int number)
    {
        Delivered.TryAdd(number, 0);
        if (Interlocked.Increment(ref _delivered) == expectedCount)
            AllDelivered.TrySetResult();
    }
}

sealed record TestCommand(string Id) : ICommand;
sealed record TestEvent(string Id) : IEvent;
sealed record FailingCommand(string Id) : ICommand;
sealed record ConcurrentCommand(int Number) : ICommand;

sealed class TestCommandHandler(DeliveryProbe probe) : ICommandHandler<TestCommand>
{
    public Task ExecuteAsync(TestCommand command, CancellationToken ct)
    {
        probe.Command.TrySetResult(command.Id);
        return Task.CompletedTask;
    }
}

sealed class TestEventHandler(DeliveryProbe probe) : IEventHandler<TestEvent>
{
    public Task HandleAsync(TestEvent eventModel, CancellationToken ct)
    {
        probe.Event.TrySetResult(eventModel.Id);
        return Task.CompletedTask;
    }
}

sealed class SecondTestEventHandler(DeliveryProbe probe) : IEventHandler<TestEvent>
{
    public Task HandleAsync(TestEvent eventModel, CancellationToken ct)
    {
        probe.SecondEvent.TrySetResult(eventModel.Id);
        return Task.CompletedTask;
    }
}

sealed class FailingCommandHandler(DeliveryProbe probe) : ICommandHandler<FailingCommand>
{
    public Task ExecuteAsync(FailingCommand command, CancellationToken ct)
    {
        probe.Command.TrySetResult(command.Id);
        throw new InvalidOperationException("Expected test failure.");
    }
}

sealed class ConcurrentCommandHandler(ConcurrentDeliveryProbe probe) : ICommandHandler<ConcurrentCommand>
{
    public Task ExecuteAsync(ConcurrentCommand command, CancellationToken ct)
    {
        probe.Add(command.Number);

        return Task.CompletedTask;
    }
}
