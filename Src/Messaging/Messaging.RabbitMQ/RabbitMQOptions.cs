using System.Text.Json;

namespace FastEndpoints.Messaging.RabbitMQ;

/// <summary>
/// connection and serialization options for the RabbitMQ messaging transport.
/// </summary>
public sealed class RabbitMQOptions
{
    /// <summary>
    /// RabbitMQ AMQP connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "amqp://guest:guest@localhost:5672/";

    /// <summary>
    /// name shown for the connection in RabbitMQ management tools.
    /// </summary>
    public string ClientProvidedName { get; set; } = "FastEndpoints.Messaging.RabbitMQ";

    /// <summary>
    /// prefix used by convention-based exchange and queue names.
    /// </summary>
    public string TopologyPrefix { get; set; } = "fe";

    /// <summary>
    /// maximum number of publications that can wait for publisher confirmations concurrently.
    /// each concurrent publication exclusively leases one long-lived RabbitMQ channel.
    /// </summary>
    public int PublisherConcurrency { get; set; } = 1;

    /// <summary>
    /// JSON options used to serialize and deserialize messages.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web);
}

/// <summary>
/// topology options for a RabbitMQ publisher route.
/// </summary>
public class RabbitPublishOptions
{
    /// <summary>
    /// RabbitMQ queue implementation used for this route.
    /// </summary>
    public RabbitQueueType QueueType { get; set; } = RabbitQueueType.Classic;

    /// <summary>
    /// exchange name. a convention-based name is used when not specified.
    /// </summary>
    public string? Exchange { get; set; }

    /// <summary>
    /// queue name. a convention-based name is used when not specified.
    /// </summary>
    public string? Queue { get; set; }

    /// <summary>
    /// routing key. a convention-based key is used when not specified.
    /// </summary>
    public string? RoutingKey { get; set; }

    /// <summary>
    /// whether the exchange and queue survive broker restarts.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// whether the queue is deleted when its last consumer disconnects.
    /// </summary>
    public bool AutoDelete { get; set; }

    /// <summary>
    /// whether the queue can only be used by this connection and is deleted when the connection closes.
    /// supported by classic queues only.
    /// </summary>
    public bool Exclusive { get; set; }

    /// <summary>
    /// optional arguments passed when declaring the exchange.
    /// </summary>
    public IDictionary<string, object?> ExchangeArguments { get; } = new Dictionary<string, object?>();

    /// <summary>
    /// optional arguments passed when declaring the queue. transport-owned arguments such as <c>x-queue-type</c> take precedence.
    /// </summary>
    public IDictionary<string, object?> QueueArguments { get; } = new Dictionary<string, object?>();
}

/// <summary>
/// topology and delivery options for a RabbitMQ consumer.
/// </summary>
public sealed class RabbitConsumerOptions : RabbitPublishOptions
{
    /// <summary>
    /// maximum number of unacknowledged deliveries on the consumer channel.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 16;

    /// <summary>
    /// maximum number of deliveries dispatched concurrently for this consumer.
    /// </summary>
    public ushort Concurrency { get; set; } = 1;

    /// <summary>
    /// whether a message is requeued when deserialization or handler execution fails.
    /// </summary>
    public bool RequeueOnFailure { get; set; }

    /// <summary>
    /// optional arguments passed when starting the consumer, for example <c>x-stream-offset</c> for stream queues.
    /// </summary>
    public IDictionary<string, object?> ConsumerArguments { get; } = new Dictionary<string, object?>();
}

/// <summary>
/// queue implementations supported by RabbitMQ through AMQP 0-9-1 declarations.
/// </summary>
public enum RabbitQueueType
{
    /// <summary>classic RabbitMQ queue.</summary>
    Classic,

    /// <summary>replicated Raft-based quorum queue.</summary>
    Quorum,

    /// <summary>append-only RabbitMQ stream consumed through the AMQP 0-9-1 compatibility path.</summary>
    Stream
}
