using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FastEndpoints;

/// <summary>
/// extension methods for registering messaging services
/// </summary>
public static class MessagingExtensions
{
    /// <summary>
    /// adds the messaging services (command bus and event bus) to the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies">assemblies to scan for command handlers and event handlers. if not specified, scans all loaded assemblies.</param>
    public static IServiceCollection AddMessaging(this IServiceCollection services, params Assembly[]? assemblies)
    {
        services.TryAddSingleton<IServiceResolver, ServiceResolver>();

        var assembliesToScan = assemblies?.Length > 0 ? assemblies : AppDomain.CurrentDomain.GetAssemblies();
        var cmdHandlerRegistry = new CommandHandlerRegistry();

        foreach (var assembly in assembliesToScan)
        {
            try
            {
                foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }))
                {
                    var cmdHandlerInterface = type.GetInterfaces()
                                                  .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>));

                    if (cmdHandlerInterface is not null)
                    {
                        var tCommand = cmdHandlerInterface.GetGenericArguments()[0];
                        cmdHandlerRegistry[tCommand] = new(type);
                    }

                    var evtHandlerInterface = type.GetInterfaces()
                                                  .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

                    if (evtHandlerInterface is null)
                        continue;

                    var tEvent = evtHandlerInterface.GetGenericArguments()[0];

                    var handlers = EventBase.HandlerDict.GetOrAdd(tEvent, _ => []);
                    handlers.Add(type);

                    var eventBusType = typeof(EventBus<>).MakeGenericType(tEvent);
                    services.TryAddTransient(eventBusType);
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Ignore assemblies that cannot be loaded
            }
        }

        services.TryAddSingleton(cmdHandlerRegistry);

        return services;
    }

    /// <summary>
    /// configures the messaging service resolver. Call this after building the service provider.
    /// </summary>
    /// <param name="provider">the service provider</param>
    /// <returns>the service provider for chaining</returns>
    public static IServiceProvider UseMessaging(this IServiceProvider provider)
    {
        ServiceResolver.Instance = provider.GetRequiredService<IServiceResolver>();

        return provider;
    }
}