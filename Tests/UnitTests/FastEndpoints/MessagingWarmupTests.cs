using FastEndpoints;
using Xunit;

namespace Messaging;

public class MessagingWarmupTests : IDisposable
{
    public void Dispose()
        => EventBase.HandlerDict.Clear();

    [Fact]
    public void WarmupMessaging_ResolvesEventBusForEachEventWithHandlers()
    {
        EventBase.HandlerDict[typeof(WarmupEventA)] = [typeof(WarmupEventHandlerA)];
        EventBase.HandlerDict[typeof(WarmupEventB)] = [typeof(WarmupEventHandlerB)];
        var provider = new RecordingServiceProvider();

        MessagingExtensions.WarmupMessaging(provider);

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
    public List<Type> RequestedTypes { get; } = [];

    public object? GetService(Type serviceType)
    {
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
