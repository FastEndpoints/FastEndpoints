using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FastEndpoints.Messaging.RabbitMQ;

/// <summary>
/// dependency injection extensions for the RabbitMQ messaging transport.
/// </summary>
public static class RabbitMQExtensions
{
    /// <summary>
    /// adds the RabbitMQ transport using an Aspire-provided or configuration-based named connection string.
    /// </summary>
    public static RabbitMQBuilder AddRabbitMQMessaging(this IHostApplicationBuilder builder,
                                                       string connectionName,
                                                       Action<RabbitMQOptions>? configure = null)
    {
        var connectionString = builder.Configuration[$"ConnectionStrings:{connectionName}"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Connection string [{connectionName}] was not found in ConnectionStrings configuration.");

        return builder.Services.AddRabbitMQMessaging(
            options =>
            {
                options.ConnectionString = connectionString;
                configure?.Invoke(options);
            });
    }

    /// <summary>
    /// adds the RabbitMQ publisher and hosted consumers to the service collection.
    /// </summary>
    public static RabbitMQBuilder AddRabbitMQMessaging(this IServiceCollection services, Action<RabbitMQOptions>? configure = null)
    {
        var options = new RabbitMQOptions();
        configure?.Invoke(options);

        if (!Uri.TryCreate(options.ConnectionString, UriKind.Absolute, out _))
            throw new ArgumentException("A valid absolute AMQP connection string is required.", nameof(configure));
        if (string.IsNullOrWhiteSpace(options.TopologyPrefix))
            throw new ArgumentException("RabbitMQ topology prefix cannot be empty.", nameof(configure));
        if (options.PublisherConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.PublisherConcurrency), "RabbitMQ publisher concurrency must be greater than zero.");

        var routes = new RabbitRouteRegistry();
        var consumers = new List<RabbitConsumerRegistration>();
        services.AddSingleton(options);
        services.AddSingleton(routes);
        services.AddSingleton(consumers);
        services.TryAddSingleton<RabbitConnection>();
        services.TryAddSingleton<IRabbitMQPublisher, RabbitMQPublisher>();
        services.AddHostedService<RabbitMQConsumerService>();

        return new(services, options, routes, consumers);
    }
}
