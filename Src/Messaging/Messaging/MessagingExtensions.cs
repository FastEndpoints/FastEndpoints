using System.Diagnostics.CodeAnalysis;
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
    private const string AotWarning = "Reflection-based messaging discovery is not trim compatible. Use AddMessaging(DiscoveredTypes.All) with the source generator.";

    /// <param name="services"></param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// adds the messaging services (command bus and event bus) to the service collection using reflection-based type discovery.
        /// <para>TIP: You don't have to call this method if you already have <c>.AddFastEndpoints()</c> in your pipeline.</para>
        /// </summary>
        [RequiresUnreferencedCode(AotWarning), RequiresDynamicCode(AotWarning)]
        public IServiceCollection AddMessaging()
            => services.AddMessaging((Assembly[]?)null);

        /// <summary>
        /// adds the messaging services (command bus and event bus) to the service collection using reflection-based type discovery.
        /// <para>TIP: You don't have to call this method if you already have <c>.AddFastEndpoints()</c> in your pipeline.</para>
        /// </summary>
        /// <param name="assemblies">assemblies to scan for command handlers and event handlers, in addition to all loaded assemblies.</param>
        [RequiresUnreferencedCode(AotWarning), RequiresDynamicCode(AotWarning)]
        public IServiceCollection AddMessaging(params Assembly[]? assemblies)
            => AddMessagingCore(
                services,
                () => AssemblyScanner.ScanForTypes(
                    new()
                    {
                        Assemblies = assemblies,
                        InterfaceTypes =
                        [
                            Types.ICommandHandler,
                            Types.IEventHandler
                        ]
                    }));

        /// <summary>
        /// adds the messaging services (command bus and event bus) to the service collection using source-generated discovered types.
        /// pass one <see cref="List{Type}" /> per referenced assembly, e.g.:
        /// <c>AddMessaging(Lib1.DiscoveredTypes.All, Lib2.DiscoveredTypes.All)</c>
        /// <para>TIP: You don't have to call this method if you already have <c>.AddFastEndpoints()</c> in your pipeline.</para>
        /// </summary>
        /// <param name="discoveredTypes">one or more lists of source-generated discovered types, one per referenced assembly</param>
        public IServiceCollection AddMessaging(params List<Type>[] discoveredTypes)
            => AddMessagingCore(services, () => discoveredTypes.SelectMany(t => t));
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075")]
    static IServiceCollection AddMessagingCore(IServiceCollection services, Func<IEnumerable<Type>> getTypes)
    {
        services.TryAddSingleton<IServiceResolver, ServiceResolver>();
        services.TryAddSingleton(typeof(EventBus<>));
        services.TryAddSingleton<CommandHandlerRegistry>(
            _ =>
            {
                var cmdHandlerRegistry = new CommandHandlerRegistry();

                foreach (var t in getTypes())
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
    /// <param name="options">optionally configure messaging startup behavior</param>
    /// <returns>the service provider for chaining</returns>
    public static IServiceProvider UseMessaging(this IServiceProvider provider, Action<MessagingOptions>? options = null)
    {
        ServiceResolver.Instance = provider.GetService<IServiceResolver>() ?? throw new InvalidOperationException(ErrMsg);

        if (provider.GetService<CommandHandlerRegistry>() is null) //this causes either the above factory method to run or resolve an existing instance
            throw new InvalidOperationException(ErrMsg);

        var opts = new MessagingOptions();
        options?.Invoke(opts);

        if (opts.WarmupRequested)
            WarmupMessaging(provider);

        return provider;
    }

    [UnconditionalSuppressMessage("AOT", "IL3050")]
    internal static void WarmupMessaging(IServiceProvider provider)
    {
        foreach (var tEvent in EventBase.HandlerDict.Keys)
        {
            var eventBusType = typeof(EventBus<>).MakeGenericType(tEvent);
            provider.GetService(eventBusType);
        }
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

        if (tGeneric == Types.IStreamCommandHandlerOf2)
        {
            cmdHandlerRegistry.TryAdd(
                tInterface.GetGenericArguments()[0],
                new(t));
        }
    }
}

/// <summary>
/// messaging startup options
/// </summary>
public sealed class MessagingOptions
{
    internal bool WarmupRequested { get; private set; }

    /// <summary>
    /// pre-initialize event bus instances during startup.
    /// messaging warmup is lazy by default and only runs when this method is called from <c>UseMessaging(...)</c>.
    /// </summary>
    public void Warmup()
        => WarmupRequested = true;
}