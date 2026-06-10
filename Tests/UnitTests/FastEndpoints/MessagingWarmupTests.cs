using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Messaging;

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
    public void WarmUp_SetsWarmupRequested()
    {
        var opts = new MessagingOptions();

        opts.WarmupRequested.ShouldBeFalse();

        opts.WarmUp();

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
    public void UseMessaging_WarmUp_ResolvesEventBusForEachEventWithHandlers()
    {
        EventBase.HandlerDict[typeof(WarmupEventA)] = [typeof(WarmupEventHandlerA)];
        EventBase.HandlerDict[typeof(WarmupEventB)] = [typeof(WarmupEventHandlerB)];
        var provider = new RecordingServiceProvider();

        provider.UseMessaging(o => o.WarmUp());

        provider.RequestedTypes.ShouldContain(typeof(EventBus<WarmupEventA>));
        provider.RequestedTypes.ShouldContain(typeof(EventBus<WarmupEventB>));
        provider.RequestedTypes.Count.ShouldBe(2);
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
