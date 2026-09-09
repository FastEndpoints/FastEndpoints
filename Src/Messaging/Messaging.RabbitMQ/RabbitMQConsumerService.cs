using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FastEndpoints.Messaging.RabbitMQ;

sealed class RabbitMQConsumerService(
    RabbitConnection connection,
    List<RabbitConsumerRegistration> registrations,
    RabbitMQOptions options,
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime) : IHostedService
{
    readonly List<IChannel> _channels = [];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var registration in registrations)
            await StartConsumerAsync(registration, cancellationToken);
    }

    async Task StartConsumerAsync(RabbitConsumerRegistration registration, CancellationToken ct)
    {
        var conn = await connection.GetAsync(ct);
        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            consumerDispatchConcurrency: registration.Concurrency);
        var channel = await conn.CreateChannelAsync(channelOptions, ct);
        _channels.Add(channel);

        var route = registration.Route;
        await channel.ExchangeDeclareAsync(route.Exchange, route.ExchangeType, route.Durable, autoDelete: false, route.ExchangeArguments, cancellationToken: ct);
        await channel.QueueDeclareAsync(route.Queue!, route.Durable, route.Exclusive, route.AutoDelete, route.QueueArguments, cancellationToken: ct);
        await channel.QueueBindAsync(route.Queue!, route.Exchange, route.RoutingKey, cancellationToken: ct);
        await channel.BasicQosAsync(prefetchSize: 0, registration.PrefetchCount, global: false, ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var acknowledgementLock = new SemaphoreSlim(1, 1);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await registration.Dispatch(scope.ServiceProvider, args.Body, options.SerializerOptions, lifetime.ApplicationStopping);
                await AcknowledgeAsync(async () => await channel.BasicAckAsync(args.DeliveryTag, multiple: false, lifetime.ApplicationStopping));
            }
            catch (OperationCanceledException) when (lifetime.ApplicationStopping.IsCancellationRequested)
            {
                await AcknowledgeAsync(() => SafeNackAsync(channel, args.DeliveryTag, requeue: true));
            }
            catch
            {
                await AcknowledgeAsync(() => SafeNackAsync(channel, args.DeliveryTag, registration.RequeueOnFailure));
            }

            async Task AcknowledgeAsync(Func<Task> operation)
            {
                await acknowledgementLock.WaitAsync();
                try
                {
                    await operation();
                }
                finally
                {
                    acknowledgementLock.Release();
                }
            }
        };
        await channel.BasicConsumeAsync(route.Queue!, autoAck: false, consumerTag: string.Empty, registration.ConsumerArguments, consumer, ct);
    }

    static async Task SafeNackAsync(IChannel channel, ulong deliveryTag, bool requeue)
    {
        if (channel.IsOpen)
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var channel in _channels)
            await channel.DisposeAsync();
        _channels.Clear();
    }
}
