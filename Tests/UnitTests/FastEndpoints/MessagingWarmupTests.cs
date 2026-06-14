using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Messaging;

[CollectionDefinition(MessagingWarmupCollection.Name, DisableParallelization = true)]
public class MessagingWarmupCollection
{
    public const string Name = nameof(MessagingWarmupCollection);
}

[Collection(MessagingWarmupCollection.Name)]
public class MessagingWarmupTests : IDisposable
{
    public void Dispose()
    {
        EventBase.HandlerDict.Clear();
        var testingProvider = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
        ServiceResolver.Instance = new ServiceResolver(
            provider: testingProvider,
            ctxAccessor: testingProvider.GetRequiredService<IHttpContextAccessor>(),
            isUnitTestMode: true);
    }

    [Fact]
    public void Warmup_SetsWarmupRequested()
    {
        var opts = new MessagingOptions();

        opts.WarmupRequested.ShouldBeFalse();

        opts.Warmup();

        opts.WarmupRequested.ShouldBeTrue();
    }

    [Fact]
    public void UseMessaging_DoesNotWarmupByDefault()
    {
        EventBase.HandlerDict[typeof(WarmupEventA)] = [typeof(WarmupEventHandlerA)];
        var provider = new RecordingServiceProvider();

        provider.UseMessaging();

        provider.RequestedTypes.ShouldBeEmpty();
    }

    [Fact]
    public void UseMessaging_Warmup_ResolvesEventBusForEachEventWithHandlers()
    {
        EventBase.HandlerDict[typeof(WarmupEventA)] = [typeof(WarmupEventHandlerA)];
        EventBase.HandlerDict[typeof(WarmupEventB)] = [typeof(WarmupEventHandlerB)];
        var provider = new RecordingServiceProvider();

        provider.UseMessaging(o => o.Warmup());

        provider.RequestedTypes.ShouldContain(typeof(EventBus<WarmupEventA>));
        provider.RequestedTypes.ShouldContain(typeof(EventBus<WarmupEventB>));
        provider.RequestedTypes.Count.ShouldBe(2);
    }

    [Fact]
    public void UseMessaging_Warmup_ResolvesActualEventBusInstancesFromServiceProvider()
    {
        var services = new ServiceCollection();
        var resolvedBuses = new List<object>();
        services.AddHttpContextAccessor();
        services.AddMessaging([typeof(WarmupEventHandlerA), typeof(WarmupEventHandlerB)]);
        services.AddSingleton(
            sp =>
            {
                var bus = ActivatorUtilities.CreateInstance<EventBus<WarmupEventA>>(sp);
                resolvedBuses.Add(bus);
                return bus;
            });
        services.AddSingleton(
            sp =>
            {
                var bus = ActivatorUtilities.CreateInstance<EventBus<WarmupEventB>>(sp);
                resolvedBuses.Add(bus);
                return bus;
            });
        using var provider = services.BuildServiceProvider();

        provider.UseMessaging(o => o.Warmup());

        resolvedBuses.ShouldContain(provider.GetRequiredService<EventBus<WarmupEventA>>());
        resolvedBuses.ShouldContain(provider.GetRequiredService<EventBus<WarmupEventB>>());
        resolvedBuses.Count.ShouldBe(2);
    }

    [Fact]
    public void WarmupMessaging_DoesNothingWhenNoEventHandlersAreRegistered()
    {
        var provider = new RecordingServiceProvider();

        MessagingExtensions.WarmupMessaging(provider);

        provider.RequestedTypes.ShouldBeEmpty();
    }

    [Fact]
    public void WarmupMessaging_DoesNotThrowWhenEventBusServiceIsMissing()
    {
        EventBase.HandlerDict[typeof(WarmupEventA)] = [typeof(WarmupEventHandlerA)];
        var provider = new RecordingServiceProvider();

        Should.NotThrow(() => MessagingExtensions.WarmupMessaging(provider));

        provider.RequestedTypes.ShouldHaveSingleItem().ShouldBe(typeof(EventBus<WarmupEventA>));
    }
}

file sealed class RecordingServiceProvider : IServiceProvider
{
    readonly CommandHandlerRegistry _commandHandlerRegistry = new();
    readonly IServiceProvider _rootProvider = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
    readonly IServiceResolver _serviceResolver;

    public RecordingServiceProvider()
        => _serviceResolver = new ServiceResolver(
            provider: _rootProvider,
            ctxAccessor: _rootProvider.GetRequiredService<IHttpContextAccessor>(),
            isUnitTestMode: true);

    public List<Type> RequestedTypes { get; } = [];

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceResolver))
            return _serviceResolver;

        if (serviceType == typeof(CommandHandlerRegistry))
            return _commandHandlerRegistry;

        RequestedTypes.Add(serviceType);
        return null;
    }
}

file sealed class WarmupEventA : IEvent;

file sealed class WarmupEventB : IEvent;

file sealed class WarmupEventHandlerA : IEventHandler<WarmupEventA>
{
    public Task HandleAsync(WarmupEventA eventModel, CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class WarmupEventHandlerB : IEventHandler<WarmupEventB>
{
    public Task HandleAsync(WarmupEventB eventModel, CancellationToken ct)
        => Task.CompletedTask;
}
