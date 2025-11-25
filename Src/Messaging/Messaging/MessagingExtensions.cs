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
    /// adds the messaging services (command bus, event bus) to the service collection.
    /// </summary>
    /// <param name="services">the service collection to add services to</param>
    /// <param name="assemblies">assemblies to scan for command handlers and event handlers. if not specified, scans all loaded assemblies.</param>
    /// <returns>the service collection for chaining</returns>
    public static IServiceCollection AddMessaging(this IServiceCollection services, params Assembly[]? assemblies)
    {
        var scanAssemblies = assemblies?.Length > 0 ? assemblies : AppDomain.CurrentDomain.GetAssemblies();

        // Register command handler registry
        services.TryAddSingleton<CommandHandlerRegistry>();

        // Register messaging service resolver
        services.TryAddSingleton<IMessagingServiceResolver, MessagingServiceResolver>();

        // Discover and register command handlers
        var commandHandlerRegistry = new CommandHandlerRegistry();

        foreach (var assembly in scanAssemblies)
        {
            try
            {
                foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }))
                {
                    // Register command handlers
                    var cmdHandlerInterface = type.GetInterfaces()
                                                   .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>));

                    if (cmdHandlerInterface is not null)
                    {
                        var tCommand = cmdHandlerInterface.GetGenericArguments()[0];
                        commandHandlerRegistry[tCommand] = new CommandHandlerDefinition(type);
                    }

                    // Register event handlers
                    var evtHandlerInterface = type.GetInterfaces()
                                                   .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

                    if (evtHandlerInterface is not null)
                    {
                        var tEvent = evtHandlerInterface.GetGenericArguments()[0];

                        var handlers = EventBase.HandlerDict.GetOrAdd(tEvent, _ => []);
                        handlers.Add(type);

                        // Register EventBus<TEvent>
                        var eventBusType = typeof(EventBus<>).MakeGenericType(tEvent);
                        services.TryAddTransient(eventBusType);
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Ignore assemblies that cannot be loaded
            }
        }

        services.AddSingleton(commandHandlerRegistry);

        return services;
    }

    /// <summary>
    /// configures the messaging service resolver. Call this after building the service provider.
    /// </summary>
    /// <param name="provider">the service provider</param>
    /// <returns>the service provider for chaining</returns>
    public static IServiceProvider UseMessaging(this IServiceProvider provider)
    {
        MsgCfg.ServiceResolver = provider.GetRequiredService<IMessagingServiceResolver>();

        return provider;
    }
}
