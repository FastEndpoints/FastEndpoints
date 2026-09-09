using FastEndpoints;
using FastEndpoints.Messaging.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Unit.Messaging.RabbitMQ;

public class RabbitMQRegistrationTests
{
    [Fact]
    public async Task Registers_Publisher_And_Typed_Handlers()
    {
        var hostBuilder = Host.CreateApplicationBuilder();
        var services = hostBuilder.Services;
        var builder = services.AddRabbitMQMessaging(o => o.TopologyPrefix = "orders")
                              .AddCommand<CreateOrder, CreateOrderHandler>()
                              .AddEvent<OrderCreated, OrderCreatedHandler>();

        var host = hostBuilder.Build();
        await using var hostDisposer = (IAsyncDisposable)host;
        var provider = host.Services;

        builder.Services.ShouldBeSameAs(services);
        provider.GetRequiredService<IRabbitMQPublisher>().ShouldNotBeNull();
        provider.GetRequiredService<ICommandHandler<CreateOrder>>().ShouldBeOfType<CreateOrderHandler>();
        provider.GetServices<IEventHandler<OrderCreated>>().Single().ShouldBeOfType<OrderCreatedHandler>();
        provider.GetServices<IHostedService>().Count().ShouldBe(1);
    }

    [Fact]
    public void Rejects_Invalid_Consumer_Limits()
    {
        var services = new ServiceCollection();
        var builder = services.AddRabbitMQMessaging();

        Should.Throw<ArgumentOutOfRangeException>(
            () => builder.AddCommand<CreateOrder, CreateOrderHandler>(o => o.Concurrency = 0));
        Should.Throw<ArgumentOutOfRangeException>(
            () => builder.AddEvent<OrderCreated, OrderCreatedHandler>(o => o.PrefetchCount = 0));
        Should.Throw<InvalidOperationException>(
            () => builder.AddCommand<CreateOrder, CreateOrderHandler>(
                o =>
                {
                    o.QueueType = RabbitQueueType.Quorum;
                    o.Exclusive = true;
                }));
    }

    [Fact]
    public void Rejects_Invalid_Publisher_Concurrency()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddRabbitMQMessaging(o => o.PublisherConcurrency = 0));

        exception.ParamName.ShouldBe(nameof(RabbitMQOptions.PublisherConcurrency));
    }

    [Fact]
    public void Publisher_Concurrency_Defaults_To_One()
        => new RabbitMQOptions().PublisherConcurrency.ShouldBe(1);

    [Fact]
    public void Host_Builder_Uses_Aspire_Connection_String_Convention()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:messaging"] = "amqp://guest:guest@localhost:5672/";

        var rabbit = builder.AddRabbitMQMessaging("messaging");

        rabbit.Services.ShouldBeSameAs(builder.Services);
        builder.Services.ShouldContain(d => d.ServiceType == typeof(IRabbitMQPublisher));
    }

    [Fact]
    public async Task Publisher_Rejects_Unregistered_Message_Type_Without_Connecting()
    {
        var services = new ServiceCollection();
        services.AddRabbitMQMessaging();
        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IRabbitMQPublisher>();

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => publisher.SendAsync(new CreateOrder("42")));

        exception.Message.ShouldContain(typeof(CreateOrder).FullName!);
    }

    [Fact]
    public async Task Registers_Publisher_Only_Routes_Without_Handlers()
    {
        var services = new ServiceCollection();
        services.AddRabbitMQMessaging()
                .AddCommandPublisher<CreateOrder>()
                .AddEventPublisher<OrderCreated>();
        await using var provider = services.BuildServiceProvider();
        var routes = provider.GetRequiredService<RabbitRouteRegistry>();

        routes.Get(typeof(CreateOrder)).Queue.ShouldNotBeNull();
        routes.Get(typeof(OrderCreated)).Queue.ShouldBeNull();
        provider.GetService<ICommandHandler<CreateOrder>>().ShouldBeNull();
        provider.GetServices<IEventHandler<OrderCreated>>().ShouldBeEmpty();
    }

    [Fact]
    public void Rejects_Duplicate_And_Conflicting_Registrations()
    {
        var builder = new ServiceCollection().AddRabbitMQMessaging();
        builder.AddCommand<CreateOrder, CreateOrderHandler>();
        builder.AddEvent<OrderCreated, OrderCreatedHandler>();

        Should.Throw<InvalidOperationException>(() => builder.AddCommand<CreateOrder, CreateOrderHandler>());
        Should.Throw<InvalidOperationException>(() => builder.AddEvent<OrderCreated, OrderCreatedHandler>());
        Should.Throw<InvalidOperationException>(
            () => builder.AddCommandPublisher<CreateOrder>(o => o.Exchange = "different.commands"));
    }

    [Theory]
    [InlineData(RabbitQueueType.Classic, "classic")]
    [InlineData(RabbitQueueType.Quorum, "quorum")]
    [InlineData(RabbitQueueType.Stream, "stream")]
    public async Task Explicitly_Declares_The_Selected_Queue_Type(RabbitQueueType queueType, string expected)
    {
        var services = new ServiceCollection();
        services.AddRabbitMQMessaging()
                .AddCommandPublisher<CreateOrder>(
                    o =>
                    {
                        o.QueueType = queueType;
                        o.QueueArguments["x-queue-type"] = "ignored";
                    });
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<RabbitRouteRegistry>()
                .Get(typeof(CreateOrder))
                .QueueArguments["x-queue-type"]
                .ShouldBe(expected);
    }
}

sealed record CreateOrder(string Id) : ICommand;
sealed record OrderCreated(string Id) : IEvent;

sealed class CreateOrderHandler : ICommandHandler<CreateOrder>
{
    public Task ExecuteAsync(CreateOrder command, CancellationToken ct) => Task.CompletedTask;
}

sealed class OrderCreatedHandler : IEventHandler<OrderCreated>
{
    public Task HandleAsync(OrderCreated eventModel, CancellationToken ct) => Task.CompletedTask;
}
