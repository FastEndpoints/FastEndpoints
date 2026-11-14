using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobQueues;

[Collection(Messaging.MessagingWarmupCollection.Name)]
public class JobQueueWarmupTests : IDisposable
{
    public void Dispose()
    {
        var testingProvider = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
        ServiceResolver.Instance = new ServiceResolver(
            provider: testingProvider,
            ctxAccessor: testingProvider.GetRequiredService<IHttpContextAccessor>(),
            isUnitTestMode: true);
    }

    [Fact]
    public void Warmup_SetsWarmupRequested()
    {
        var opts = new JobQueueOptions();

        opts.WarmupRequested.ShouldBeFalse();

        opts.Warmup();

        opts.WarmupRequested.ShouldBeTrue();
    }

    [Fact]
    public void UseJobQueues_DoesNotWarmupByDefault()
    {
        var provider = new RecordingServiceProvider(typeof(WarmupEventHandlerA));

        provider.UseJobQueues();

        provider.RequestedTypes.ShouldBeEmpty();
    }

    [Fact]
    public void UseJobQueues_Warmup_ResolvesEventBusForEachEventWithHandlers()
    {
        var provider = new RecordingServiceProvider(typeof(WarmupEventHandlerA), typeof(WarmupEventHandlerB));

        provider.UseJobQueues(o => o.Warmup());

        provider.RequestedTypes.ShouldContain(typeof(EventBus<WarmupEventA>));
        provider.RequestedTypes.ShouldContain(typeof(EventBus<WarmupEventB>));
        provider.RequestedTypes.Count.ShouldBe(2);
    }
}

file sealed class RecordingServiceProvider : IServiceProvider
{
    readonly CommandHandlerRegistry _commandHandlerRegistry = new();
    readonly IServiceProvider _rootProvider = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
    readonly IServiceResolver _serviceResolver;
    readonly EventOptions _options = new();
    readonly EventRegistry _events;

    public RecordingServiceProvider(params Type[] types)
    {
        var catalog = MessagingExtensions.RegisterHandlers(types, _commandHandlerRegistry);
        _events = new(this, _options, catalog, new(new ServiceCollection()));
        foreach (var tEvent in catalog.Contracts)
            _commandHandlerRegistry[tEvent] = new CommandHandlerDefinition(catalog.GetHandlers(tEvent)[0]);

        _serviceResolver = new ServiceResolver(
            provider: _rootProvider,
            ctxAccessor: _rootProvider.GetRequiredService<IHttpContextAccessor>(),
            isUnitTestMode: true);
    }

    public List<Type> RequestedTypes { get; } = [];

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(EventRegistry))
            return _events;
        if (serviceType == typeof(EventOptions))
            return _options;
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
