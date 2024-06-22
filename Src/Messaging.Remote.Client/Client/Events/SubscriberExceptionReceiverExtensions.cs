using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

public static class SubscriberExceptionReceiverExtensions
{
    /// <summary>
    /// register a custom exception receiver for receiving event subscriber exceptions.
    /// </summary>
    /// <typeparam name="TReceiver">the implementation type of the receiver</typeparam>
    public static IServiceCollection AddSubscriberExceptionReceiver<TReceiver>(this IServiceCollection services) where TReceiver : SubscriberExceptionReceiver
    {
        services.AddSingleton<SubscriberExceptionReceiver, TReceiver>();
        return services;
    }
}