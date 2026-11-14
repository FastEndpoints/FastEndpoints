using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FastEndpoints.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FastEndpoints;

/// <summary>
/// extension methods for registering messaging services
/// </summary>
/// <remarks>
/// finish event-handler registrations before building the first provider from a service collection.
/// use separate collections when providers need different handler registrations.
/// </remarks>
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

    static IServiceCollection AddMessagingCore(IServiceCollection services, Func<IEnumerable<Type>> getTypes)
    {
        services.TryAddSingleton(_ => new MessagingDiscovery(getTypes()));
        services.TryAddSingleton(p => p.GetRequiredService<MessagingDiscovery>().Commands);
        return services.AddEventServices(p => p.GetRequiredService<MessagingDiscovery>().Events);
    }

    internal static IServiceCollection AddEventServices(this IServiceCollection services, Func<IServiceProvider, EventCatalog> catalog)
    {
        var registrations = new Lazy<EventRegistrationIndex>(() => new(services));
        services.TryAddSingleton<IServiceResolver, ServiceResolver>();
        services.TryAddSingleton<EventOptions>();
        services.TryAddSingleton(typeof(EventBus<>));
        services.TryAddSingleton(p => new EventRegistry(p, p.GetRequiredService<EventOptions>(), catalog(p), registrations.Value));
        return services;
    }

    const string ErrMsg = "Make sure to call one of the following in startup: [.AddFastEndpoints()/.AddMessaging()/.AddJobQueues()]";

    /// <summary>
    /// configures the messaging functionality.
    /// configure event settings here before <c>UseFastEndpoints</c>, <c>UseJobQueues</c>, publishing, or messaging warmup.
    /// </summary>
    /// <param name="provider">the service provider</param>
    /// <param name="options">optionally configure messaging startup behavior</param>
    /// <returns>the service provider for chaining</returns>
    public static IServiceProvider UseMessaging(this IServiceProvider provider, Action<MessagingOptions>? options = null)
    {
        ServiceResolver.Instance = provider.GetService<IServiceResolver>() ?? throw new InvalidOperationException(ErrMsg);

        if (provider.GetService<CommandHandlerRegistry>() is null) //this causes either the above factory method to run or resolve an existing instance
            throw new InvalidOperationException(ErrMsg);

        var opts = new MessagingOptions(provider.GetRequiredService<EventOptions>());
        options?.Invoke(opts);

        if (opts.WarmupRequested)
            WarmupMessaging(provider);

        return provider;
    }

    internal static void WarmupMessaging(IServiceProvider provider)
        => provider.GetService<EventRegistry>()?.Warmup();

    /// <summary>
    /// registers command and stream-command handlers and returns an immutable catalog of discovered event handlers.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2075")]
    internal static EventCatalog RegisterHandlers(IEnumerable<Type> types, CommandHandlerRegistry cmdHandlerRegistry)
    {
        Dictionary<Type, HashSet<Type>> events = [];
        foreach (var t in types)
        {
            foreach (var tInterface in t.GetInterfaces())
            {
                var tGeneric = tInterface.IsGenericType
                                   ? tInterface.GetGenericTypeDefinition()
                                   : null;

                if (tGeneric is null)
                    continue;

                RegisterHandler(tGeneric, tInterface, t, cmdHandlerRegistry, events);
            }
        }
        return new(events);
    }

    static void RegisterHandler(Type tGeneric, Type tInterface, Type t, CommandHandlerRegistry cmdHandlerRegistry, Dictionary<Type, HashSet<Type>> events)
    {
        if (tGeneric == Types.IEventHandlerOf1) //IsAssignableTo() is no good here if the user inherits the interface.
        {
            var tEvent = tInterface.GetGenericArguments()[0];

            if (!events.TryGetValue(tEvent, out var handlers))
                events.Add(tEvent, handlers = []);
            handlers.Add(t);

            return;
        }

        if (tGeneric == Types.ICommandHandlerOf1 || tGeneric == Types.ICommandHandlerOf2 || tGeneric == Types.IStreamCommandHandlerOf2) // IsAssignableTo() is no good here either
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
    /// <summary>
    /// creates detached default settings. <c>UseMessaging</c> supplies the owning provider's event settings instead.
    /// </summary>
    public MessagingOptions() : this(new()) { }

    internal MessagingOptions(EventOptions events) => Events = events;

    /// <summary>
    /// settings for this provider's in-process event bus. configure before the first publish or warmup.
    /// </summary>
    public EventOptions Events { get; }

    internal bool WarmupRequested { get; private set; }

    /// <summary>
    /// pre-initialize event bus instances during startup.
    /// messaging warmup is lazy by default and only runs when this method is called from <c>UseMessaging(...)</c>.
    /// </summary>
    public void Warmup()
        => WarmupRequested = true;
}

sealed class MessagingDiscovery
{
    internal CommandHandlerRegistry Commands { get; } = new();
    internal EventCatalog Events { get; }

    internal MessagingDiscovery(IEnumerable<Type> types)
        => Events = MessagingExtensions.RegisterHandlers(types, Commands);
}
