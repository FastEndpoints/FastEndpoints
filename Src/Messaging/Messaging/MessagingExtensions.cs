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
    /// <para>TIP: You don't have to call this method if you already have <c>.AddFastEndpoints()</c> in your pipeline.</para>
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies">assemblies to scan for command handlers and event handlers, in addition to all loaded assemblies.</param>
    public static IServiceCollection AddMessaging(this IServiceCollection services, params Assembly[]? assemblies)
    {
        services.TryAddSingleton<IServiceResolver, ServiceResolver>();
        services.TryAddSingleton(typeof(EventBus<>));
        services.TryAddSingleton<CommandHandlerRegistry>(
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

    const string ErrMsg = "Make sure to call one of the following in startup: [.AddFastEndpoints()/.AddMessaging()/.AddJobQueues()]";

    /// <summary>
    /// configures the messaging functionality.
    /// <para>TIP: You don't have to call this method if you already have <c>.UseFastEndpoints()</c> in your pipeline.</para>
    /// </summary>
    /// <param name="provider">the service provider</param>
    /// <returns>the service provider for chaining</returns>
    public static IServiceProvider UseMessaging(this IServiceProvider provider)
    {
        ServiceResolver.Instance = provider.GetService<IServiceResolver>() ?? throw new InvalidOperationException(ErrMsg);

        return provider.GetService<CommandHandlerRegistry>() is null //this causes either the above factory method to run or resolve an existing instance
                   ? throw new InvalidOperationException(ErrMsg)
                   : provider;
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
                tInterface.GetGenericArguments()[0],
                new(t));
        }
    }
}