using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.Messaging.RabbitMQ;

sealed class RabbitConsumerRegistration
{
    public required Type MessageType { get; init; }
    public required Type HandlerType { get; init; }
    public required RabbitRoute Route { get; init; }
    public required ushort PrefetchCount { get; init; }
    public required ushort Concurrency { get; init; }
    public required bool RequeueOnFailure { get; init; }
    public required IDictionary<string, object?> ConsumerArguments { get; init; }
    public required Func<IServiceProvider, ReadOnlyMemory<byte>, JsonSerializerOptions, CancellationToken, Task> Dispatch { get; init; }

    public static RabbitConsumerRegistration Command<TCommand, THandler>(RabbitRoute route, RabbitConsumerOptions options)
        where TCommand : class, ICommand
        where THandler : class, ICommandHandler<TCommand>
        => new()
        {
            MessageType = typeof(TCommand),
            HandlerType = typeof(THandler),
            Route = route,
            PrefetchCount = options.PrefetchCount,
            Concurrency = options.Concurrency,
            RequeueOnFailure = options.RequeueOnFailure,
            ConsumerArguments = options.ConsumerArguments,
            Dispatch = async (provider, body, serializerOptions, ct) =>
            {
                var message = JsonSerializer.Deserialize<TCommand>(body.Span, serializerOptions)
                              ?? throw new JsonException($"Unable to deserialize RabbitMQ command [{typeof(TCommand).FullName}].");
                await provider.GetRequiredService<THandler>().ExecuteAsync(message, ct);
            }
        };

    public static RabbitConsumerRegistration Event<TEvent, THandler>(RabbitRoute route, RabbitConsumerOptions options)
        where TEvent : class, IEvent
        where THandler : class, IEventHandler<TEvent>
        => new()
        {
            MessageType = typeof(TEvent),
            HandlerType = typeof(THandler),
            Route = route,
            PrefetchCount = options.PrefetchCount,
            Concurrency = options.Concurrency,
            RequeueOnFailure = options.RequeueOnFailure,
            ConsumerArguments = options.ConsumerArguments,
            Dispatch = async (provider, body, serializerOptions, ct) =>
            {
                var message = JsonSerializer.Deserialize<TEvent>(body.Span, serializerOptions)
                              ?? throw new JsonException($"Unable to deserialize RabbitMQ event [{typeof(TEvent).FullName}].");
                await provider.GetRequiredService<THandler>().HandleAsync(message, ct);
            }
        };
}
