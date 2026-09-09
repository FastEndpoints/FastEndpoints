using RabbitMQ.Client;

namespace FastEndpoints.Messaging.RabbitMQ;

sealed record RabbitRoute(
    string Exchange,
    string ExchangeType,
    string RoutingKey,
    string? Queue,
    bool Durable,
    bool AutoDelete,
    bool Exclusive,
    IDictionary<string, object?> ExchangeArguments,
    IDictionary<string, object?> QueueArguments,
    IDictionary<string, object?> ConsumerArguments);

sealed class RabbitRouteRegistry
{
    readonly Dictionary<Type, RabbitRoute> _routes = new();

    public void Add(Type messageType, RabbitRoute route)
    {
        if (_routes.TryGetValue(messageType, out var current) &&
            !PublisherTopologyMatches(current, route))
            throw new InvalidOperationException($"RabbitMQ publisher topology for [{messageType.FullName}] has already been configured differently.");

        _routes[messageType] = route;
    }

    public RabbitRoute Get(Type messageType)
        => _routes.TryGetValue(messageType, out var route)
               ? route
               : throw new InvalidOperationException($"No RabbitMQ route has been registered for [{messageType.FullName}].");

    static bool PublisherTopologyMatches(RabbitRoute current, RabbitRoute candidate)
        => current.Exchange == candidate.Exchange &&
           current.ExchangeType == candidate.ExchangeType &&
           current.RoutingKey == candidate.RoutingKey &&
           current.Queue == candidate.Queue &&
           current.Durable == candidate.Durable &&
           current.AutoDelete == candidate.AutoDelete &&
           current.Exclusive == candidate.Exclusive &&
           ArgumentsMatch(current.ExchangeArguments, candidate.ExchangeArguments) &&
           ArgumentsMatch(current.QueueArguments, candidate.QueueArguments);

    static bool ArgumentsMatch(IDictionary<string, object?> current, IDictionary<string, object?> candidate)
        => current.Count == candidate.Count &&
           current.All(pair => candidate.TryGetValue(pair.Key, out var value) && Equals(pair.Value, value));
}

static class RabbitTopology
{
    public static RabbitRoute Command<TCommand>(RabbitMQOptions transport, RabbitPublishOptions routeOptions)
    {
        var key = Name(typeof(TCommand));

        return new(
            routeOptions.Exchange ?? $"{transport.TopologyPrefix}.commands",
            ExchangeType.Direct,
            routeOptions.RoutingKey ?? key,
            routeOptions.Queue ?? $"{transport.TopologyPrefix}.command.{key}",
            routeOptions.Durable,
            routeOptions.AutoDelete,
            routeOptions.Exclusive,
            routeOptions.ExchangeArguments,
            QueueArguments(routeOptions),
            ConsumerArguments(routeOptions));
    }

    public static RabbitRoute Event<TEvent, THandler>(RabbitMQOptions transport, RabbitConsumerOptions consumer)
    {
        var eventName = Name(typeof(TEvent));

        return new(
            consumer.Exchange ?? $"{transport.TopologyPrefix}.event.{eventName}",
            ExchangeType.Fanout,
            consumer.RoutingKey ?? string.Empty,
            consumer.Queue ?? $"{transport.TopologyPrefix}.event.{eventName}.{Name(typeof(THandler))}",
            consumer.Durable,
            consumer.AutoDelete,
            consumer.Exclusive,
            consumer.ExchangeArguments,
            QueueArguments(consumer),
            consumer.ConsumerArguments);
    }

    public static RabbitRoute Event<TEvent>(RabbitMQOptions transport, RabbitPublishOptions routeOptions)
    {
        var eventName = Name(typeof(TEvent));

        return new(
            routeOptions.Exchange ?? $"{transport.TopologyPrefix}.event.{eventName}",
            ExchangeType.Fanout,
            routeOptions.RoutingKey ?? string.Empty,
            null,
            routeOptions.Durable,
            false,
            false,
            routeOptions.ExchangeArguments,
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>());
    }

    static IDictionary<string, object?> QueueArguments(RabbitPublishOptions routeOptions)
    {
        var arguments = new Dictionary<string, object?>(routeOptions.QueueArguments)
        {
            ["x-queue-type"] = routeOptions.QueueType.ToString().ToLowerInvariant()
        };

        return arguments;
    }

    static IDictionary<string, object?> ConsumerArguments(RabbitPublishOptions routeOptions)
        => routeOptions is RabbitConsumerOptions consumer
               ? consumer.ConsumerArguments
               : new Dictionary<string, object?>();

    static string Name(Type type)
        => (type.FullName ?? type.Name).Replace('+', '.').ToLowerInvariant();
}
