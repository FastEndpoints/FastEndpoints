using System.Text.Json;
using RabbitMQ.Client;

namespace FastEndpoints.Messaging.RabbitMQ;

sealed class RabbitMQPublisher : IRabbitMQPublisher, IAsyncDisposable
{
    readonly AsyncResourcePool<IChannel> _channels;
    readonly RabbitConnection _connection;
    readonly RabbitMQOptions _options;
    readonly RabbitRouteRegistry _routes;

    public RabbitMQPublisher(RabbitConnection connection, RabbitRouteRegistry routes, RabbitMQOptions options)
    {
        _connection = connection;
        _routes = routes;
        _options = options;
        _channels = new(options.PublisherConcurrency, CreateChannelAsync, static channel => channel.IsOpen);
    }

    public Task SendAsync<TCommand>(TCommand command, CancellationToken ct = default) where TCommand : class, ICommand
        => PublishCore(command, ct);

    public Task PublishAsync<TEvent>(TEvent eventModel, CancellationToken ct = default) where TEvent : class, IEvent
        => PublishCore(eventModel, ct);

    async Task PublishCore<TMessage>(TMessage message, CancellationToken ct) where TMessage : class
    {
        var route = _routes.Get(typeof(TMessage));
        var body = JsonSerializer.SerializeToUtf8Bytes(message, _options.SerializerOptions);

        await using var lease = await _channels.RentAsync(ct);
        var channel = lease.Resource;
        await channel.ExchangeDeclareAsync(route.Exchange, route.ExchangeType, route.Durable, autoDelete: false, route.ExchangeArguments, cancellationToken: ct);

        if (route.Queue is not null)
        {
            await channel.QueueDeclareAsync(route.Queue, route.Durable, route.Exclusive, route.AutoDelete, route.QueueArguments, cancellationToken: ct);
            await channel.QueueBindAsync(route.Queue, route.Exchange, route.RoutingKey, cancellationToken: ct);
        }

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Type = typeof(TMessage).AssemblyQualifiedName,
            MessageId = Guid.NewGuid().ToString("N")
        };
        await channel.BasicPublishAsync(route.Exchange, route.RoutingKey, mandatory: true, properties, body, ct);
    }

    async ValueTask<IChannel> CreateChannelAsync(CancellationToken ct)
    {
        var conn = await _connection.GetAsync(ct);

        return await conn.CreateChannelAsync(new(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
    }

    public ValueTask DisposeAsync()
        => _channels.DisposeAsync();
}
