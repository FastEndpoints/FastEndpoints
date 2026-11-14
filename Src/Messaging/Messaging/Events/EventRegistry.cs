using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

// Discovery builders are copied before this immutable metadata is shared with providers.
sealed class EventCatalog
{
    readonly FrozenDictionary<Type, ImmutableArray<Type>> _handlers;

    internal static EventCatalog Empty { get; } = new([]);
    internal ImmutableArray<Type> Contracts => _handlers.Keys;

    internal EventCatalog(Dictionary<Type, HashSet<Type>> handlers)
    {
        _handlers = handlers.ToFrozenDictionary(p => p.Key, p => p.Value.ToImmutableArray());
    }

    internal ImmutableArray<Type> GetHandlers(Type contract)
        => _handlers.TryGetValue(contract, out var handlers) ? handlers : [];
}

sealed class EventRegistrationIndex
{
    internal ImmutableArray<Type> Contracts { get; }
    internal bool HasOpenHandlers { get; }

    internal EventRegistrationIndex(IServiceCollection services)
    {
        // Handler registrations must not change after the collection's first provider is built.
        // Keep metadata only. In particular, do not invoke factories or inspect their return types.
        HashSet<Type> contracts = [];

        foreach (var descriptor in services)
        {
            if (descriptor.IsKeyedService)
                continue;

            var type = descriptor.ServiceType;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                type = type.GetGenericArguments()[0];

            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IEventHandler<>))
                continue;

            if (type.IsGenericTypeDefinition)
                HasOpenHandlers = true;
            else if (!type.ContainsGenericParameters)
                contracts.Add(type.GetGenericArguments()[0]);
        }
        Contracts = contracts.ToImmutableArray();
    }
}

sealed class EventRegistry
{
    readonly IServiceProvider _provider;
    readonly EventCatalog _catalog;
    readonly EventRegistrationIndex _registrations;

    readonly Type[] _contracts;

    // Exact buses own their subscriptions. Only discovered instances and combined runtime plans need registry caches.
    readonly ConcurrentDictionary<Type, Lazy<object>> _instancesByHandlerType = new();
    readonly ConcurrentDictionary<Type, Lazy<EventDispatchPlan>> _dispatchPlansByRuntimeType = new();
    readonly Func<Type, object> _createInstance;
    readonly Func<Type, EventDispatchPlan> _buildPlan;

    internal EventOptions Options { get; }

    internal EventRegistry(IServiceProvider provider, EventOptions options, EventCatalog catalog, EventRegistrationIndex registrations)
    {
        _provider = provider;
        Options = options;
        _catalog = catalog;
        _registrations = registrations;
        _contracts = catalog.Contracts.Union(registrations.Contracts).ToArray();
        _createInstance = CreateInstance;
        _buildPlan = BuildPlan;
    }

    internal IEventHandler<T>[] ResolveDiscoveredHandlers<T>() where T : notnull
    {
        var types = _catalog.GetHandlers(typeof(T));

        if (types.IsEmpty)
            return [];

        var handlers = new IEventHandler<T>[types.Length];
        for (var i = 0; i < types.Length; i++)
            handlers[i] = (IEventHandler<T>)GetOrCreate(_instancesByHandlerType, types[i], _createInstance);

        return handlers;
    }

    object CreateInstance(Type type)
    {
        var resolver = _provider.GetRequiredService<IServiceResolver>();

        return resolver is ServiceResolver builtin
                   ? builtin.CreateSingletonFromRoot(type)
                   : resolver.CreateSingleton(type);
    }

    internal EventDispatchPlan GetDispatchPlan(Type runtimeType)
        => GetOrCreate(_dispatchPlansByRuntimeType, runtimeType, _buildPlan);

    internal EventSubscription[] GetContractSubscriptions(Type eventType)
        => ResolveBus(eventType)?.Subscriptions ?? [];

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification =
            "Reference-type exact buses and their typed invokers are exercised by the full and standalone Native AOT checks. Value-type generic DI remains unsupported under Native AOT.")]
    IExactEventBus? ResolveBus(Type eventType)
        => _provider.GetService(typeof(EventBus<>).MakeGenericType(eventType)) as IExactEventBus;

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070",
        Justification = "IEvent preserves runtime event interfaces for open-generic DI candidates; generated discovery preserves subscribed handler interfaces.")]
    EventDispatchPlan BuildPlan(Type runtimeType)
    {
        var candidates = _contracts.Where(t => t.IsAssignableFrom(runtimeType));

        if (_registrations.HasOpenHandlers)
        {
            var uniqueCandidates = candidates.ToHashSet();
            for (var type = runtimeType; type is not null; type = type.BaseType)
                uniqueCandidates.Add(type);
            uniqueCandidates.UnionWith(runtimeType.GetInterfaces());
            candidates = uniqueCandidates;
        }

        List<EventSubscription> subscriptions = [];
        foreach (var candidate in candidates)
            subscriptions.AddRange(GetContractSubscriptions(candidate));

        return new(subscriptions);
    }

    internal void Warmup()
    {
        var inheritance = Options.Freeze();
        IEnumerable<Type> contracts = inheritance ? _contracts : _catalog.Contracts;

        foreach (var contract in contracts)
        {
            var bus = ResolveBus(contract);
            if (inheritance)
                _ = bus?.Subscriptions;
        }

        if (inheritance)
        {
            foreach (var contract in contracts)
            {
                if (!contract.IsAbstract && !contract.IsInterface)
                    GetDispatchPlan(contract);
            }
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2091", Justification = "Every Lazy is constructed with an explicit factory, never Lazy's default-constructor activation path.")]
    static T GetOrCreate<T>(ConcurrentDictionary<Type, Lazy<T>> cache, Type key, Func<Type, T> factory)
    {
        var lazy = cache.GetOrAdd(key, static (type, create) => new(() => create(type)), factory);

        try
        {
            return lazy.Value;
        }
        catch
        {
            // A failing activation may be retried. Never remove a newer replacement from another caller.
            cache.TryRemove(new(key, lazy));

            throw;
        }
    }
}

interface IExactEventBus
{
    EventSubscription[] Subscriptions { get; }
}

readonly record struct EventSubscription(Type HandlerType, object Handler, Func<object, object, CancellationToken, Task> Invoke);