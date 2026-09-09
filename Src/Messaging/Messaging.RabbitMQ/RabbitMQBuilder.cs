using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FastEndpoints.Messaging.RabbitMQ;

/// <summary>
/// registers typed RabbitMQ command and event consumers.
/// </summary>
public sealed class RabbitMQBuilder
{
    readonly RabbitMQOptions _transport;
    readonly RabbitRouteRegistry _routes;
    readonly List<RabbitConsumerRegistration> _consumers;

    internal RabbitMQBuilder(IServiceCollection services, RabbitMQOptions transport, RabbitRouteRegistry routes, List<RabbitConsumerRegistration> consumers)
    {
        Services = services;
        _transport = transport;
        _routes = routes;
        _consumers = consumers;
    }

    /// <summary>
    /// the service collection being configured.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// registers the topology required to publish a command without registering a local consumer.
    /// </summary>
    public RabbitMQBuilder AddCommandPublisher<TCommand>(Action<RabbitPublishOptions>? configure = null) where TCommand : class, ICommand
    {
        var options = PublishOptions(configure);
        _routes.Add(typeof(TCommand), RabbitTopology.Command<TCommand>(_transport, options));

        return this;
    }

    /// <summary>
    /// registers the topology required to publish an event without registering a local consumer.
    /// </summary>
    public RabbitMQBuilder AddEventPublisher<TEvent>(Action<RabbitPublishOptions>? configure = null) where TEvent : class, IEvent
    {
        var options = PublishOptions(configure);
        _routes.Add(typeof(TEvent), RabbitTopology.Event<TEvent>(_transport, options));

        return this;
    }

    /// <summary>
    /// registers a command queue and its handler. competing application instances consume from the same queue.
    /// </summary>
    public RabbitMQBuilder AddCommand<TCommand, THandler>(Action<RabbitConsumerOptions>? configure = null)
        where TCommand : class, ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        if (_consumers.Any(registration => registration.MessageType == typeof(TCommand)))
            throw new InvalidOperationException($"A RabbitMQ command consumer for [{typeof(TCommand).FullName}] has already been registered.");

        var options = ConsumerOptions(configure);
        var route = RabbitTopology.Command<TCommand>(_transport, options);
        _routes.Add(typeof(TCommand), route);
        _consumers.Add(RabbitConsumerRegistration.Command<TCommand, THandler>(route, options));
        Services.TryAddTransient<THandler>();
        Services.TryAddTransient<ICommandHandler<TCommand>, THandler>();

        return this;
    }

    /// <summary>
    /// registers an event subscription and its handler. each handler gets its own queue by convention.
    /// </summary>
    public RabbitMQBuilder AddEvent<TEvent, THandler>(Action<RabbitConsumerOptions>? configure = null)
        where TEvent : class, IEvent
        where THandler : class, IEventHandler<TEvent>
    {
        if (_consumers.Any(registration => registration.MessageType == typeof(TEvent) && registration.HandlerType == typeof(THandler)))
            throw new InvalidOperationException($"RabbitMQ event consumer [{typeof(THandler).FullName}] for [{typeof(TEvent).FullName}] has already been registered.");

        var options = ConsumerOptions(configure);
        var route = RabbitTopology.Event<TEvent, THandler>(_transport, options);
        _routes.Add(typeof(TEvent), route with { Queue = null, AutoDelete = false });
        _consumers.Add(RabbitConsumerRegistration.Event<TEvent, THandler>(route, options));
        Services.TryAddTransient<THandler>();
        Services.TryAddEnumerable(ServiceDescriptor.Transient<IEventHandler<TEvent>, THandler>());

        return this;
    }

    static RabbitConsumerOptions ConsumerOptions(Action<RabbitConsumerOptions>? configure)
    {
        var options = new RabbitConsumerOptions();
        configure?.Invoke(options);

        if (options.Concurrency == 0)
            throw new ArgumentOutOfRangeException(nameof(options.Concurrency), "RabbitMQ consumer concurrency must be greater than zero.");
        if (options.PrefetchCount == 0)
            throw new ArgumentOutOfRangeException(nameof(options.PrefetchCount), "RabbitMQ prefetch count must be greater than zero.");
        Validate(options);

        return options;
    }

    static RabbitPublishOptions PublishOptions(Action<RabbitPublishOptions>? configure)
    {
        var options = new RabbitPublishOptions();
        configure?.Invoke(options);
        Validate(options);

        return options;
    }

    static void Validate(RabbitPublishOptions options)
    {
        if (options.QueueType is RabbitQueueType.Quorum or RabbitQueueType.Stream && (!options.Durable || options.AutoDelete || options.Exclusive))
            throw new InvalidOperationException("RabbitMQ quorum and stream queues must be durable, non-exclusive, and cannot be auto-delete.");
        if (options.Queue is not null && string.IsNullOrWhiteSpace(options.Queue))
            throw new ArgumentException("RabbitMQ queue name cannot be empty.", nameof(options.Queue));
        if (options.Exchange is not null && string.IsNullOrWhiteSpace(options.Exchange))
            throw new ArgumentException("RabbitMQ exchange name cannot be empty.", nameof(options.Exchange));
    }
}
