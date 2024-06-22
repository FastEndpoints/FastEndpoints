using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

public static class EventHubExceptionReceiverExtensions
{
    /// <summary>
    /// register a custom exception receiver for receiving event hub exceptions.
    /// </summary>
    /// <typeparam name="TReceiver">the implementation type of the receiver</typeparam>
    public static IServiceCollection AddEventHubExceptionReceiver<TReceiver>(this IServiceCollection services) where TReceiver : EventHubExceptionReceiver
    {
        services.AddSingleton<EventHubExceptionReceiver, TReceiver>();
        return services;
    }
}