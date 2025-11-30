using System.Reflection;
using FastEndpoints.Messaging;
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
    /// <param name="assemblies">assemblies to scan for command handlers and event handlers, in addition to all loaded assemblies.</param>
    public static IServiceCollection AddMessaging(this IServiceCollection services, params Assembly[]? assemblies)
    {
        services.TryAddSingleton<IServiceResolver, ServiceResolver>();

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

        var cmdHandlerRegistry = new CommandHandlerRegistry();

        foreach (var t in discoveredTypes)
        {
            var tInterfaces = t.GetInterfaces();

            foreach (var tInterface in tInterfaces)
            {
                var tGeneric = tInterface.IsGenericType
                                   ? tInterface.GetGenericTypeDefinition()
                                   : null;

                if (tGeneric is null)
                    continue;

                if (tGeneric == Types.IEventHandlerOf1) // IsAssignableTo() is no good here
                {
                    var tEvent = tInterface.GetGenericArguments()[0];

                    if (EventBase.HandlerDict.TryGetValue(tEvent, out var handlers))
                        handlers.Add(t);
                    else
                        EventBase.HandlerDict[tEvent] = [t];

                    continue;
                }

                if (tGeneric == Types.ICommandHandlerOf1 || tGeneric == Types.ICommandHandlerOf2) // IsAssignableTo() is no good here
                {
                    cmdHandlerRegistry.TryAdd(
                        key: tInterface.GetGenericArguments()[0],
                        value: new(t));

                    // ReSharper disable once RedundantJumpStatement
                    continue;
                }
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