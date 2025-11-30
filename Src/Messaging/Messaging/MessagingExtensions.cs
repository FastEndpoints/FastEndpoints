using System.Reflection;
using FastEndpoints.Messaging;
using Microsoft.Extensions.DependencyInjection;

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
    /// <param name="assemblies">assemblies to scan for command handlers and event handlers, in addition to all loaded assemblies.</param>
    public static IServiceCollection AddMessaging(this IServiceCollection services, params Assembly[]? assemblies)
    {
        services.AddSingleton<IServiceResolver, ServiceResolver>();
        services.AddSingleton<CommandHandlerRegistry>(
            _ =>
            {
                var cmdHandlerRegistry = new CommandHandlerRegistry();
                var discoveredTypes = AssemblyScanner.ScanForTypes(
                    new()
                    {
                        Assemblies = assemblies,
                        InterfaceTypes =
                        [
                            Types.ICommandHandler,
                            Types.IEventHandler
                        ]
                    });

                foreach (var t in discoveredTypes)
                {
                    foreach (var tInterface in t.GetInterfaces())
                    {
                        var tGeneric = tInterface.IsGenericType
                                           ? tInterface.GetGenericTypeDefinition()
                                           : null;

                        if (tGeneric is null)
                            continue;

                        RegisterHandler(tGeneric, tInterface, t, cmdHandlerRegistry);
                    }
                }

                return cmdHandlerRegistry;
            });

        return services;
    }

    internal static void RegisterHandler(Type tGeneric, Type tInterface, Type t, CommandHandlerRegistry cmdHandlerRegistry)
    {
        if (tGeneric == Types.IEventHandlerOf1) //IsAssignableTo() is no good here if the user inherits the interface.
        {
            var tEvent = tInterface.GetGenericArguments()[0];

            if (EventBase.HandlerDict.TryGetValue(tEvent, out var handlers))
                handlers.Add(t);
            else
                EventBase.HandlerDict[tEvent] = [t];

            return;
        }

        if (tGeneric == Types.ICommandHandlerOf1 || tGeneric == Types.ICommandHandlerOf2) // IsAssignableTo() is no good here either
        {
            cmdHandlerRegistry.TryAdd(
                key: tInterface.GetGenericArguments()[0],
                value: new(t));
        }
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