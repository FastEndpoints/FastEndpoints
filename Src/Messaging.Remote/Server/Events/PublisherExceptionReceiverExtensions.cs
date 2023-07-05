using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

public static class PublisherExceptionReceiverExtensions
{
    /// <summary>
    /// register a custom exeception receiver for receiving event publisher exceptions.
    /// </summary>
    /// <typeparam name="TReceiver">the implementation type of the receiver</typeparam>
    public static IServiceCollection AddPublisherExceptionReceiver<TReceiver>(this IServiceCollection services) where TReceiver : PublisherExceptionReceiver
    {
        services.AddSingleton<PublisherExceptionReceiver, TReceiver>();
        return services;
    }
}