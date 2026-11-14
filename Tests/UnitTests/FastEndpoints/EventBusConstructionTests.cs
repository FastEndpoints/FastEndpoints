using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RouteMapper;
using Shouldly;
using Xunit;

namespace EventBus;

[Collection(RouteMapperCollection.Name)]
public class EventBusConstructionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Actual_Open_Generic_Bus_And_Contract_Plan_Share_Transient_Handlers(bool busFirst, bool receiver)
    {
        var calls = 0;
        var services = Services();
        services.AddTransient<IEventHandler<BaseEvent>>(_ => { calls++; return new Handler(); });
        if (receiver)
            services.RegisterTestEventReceivers();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var registry = provider.GetRequiredService<EventRegistry>();
        if (busFirst)
            provider.GetRequiredService<EventBus<BaseEvent>>();

        var plan = registry.GetDispatchPlan(typeof(DerivedEvent)).Subscriptions;
        var bus = provider.GetRequiredService<EventBus<BaseEvent>>();
        plan.ShouldHaveSingleItem();
        plan[0].Handler.ShouldBeSameAs(((IExactEventBus)bus).Subscriptions.Single().Handler);
        calls.ShouldBe(1);
        var model = new DerivedEvent();
        await plan[0].Invoke(plan[0].Handler, model, default);
        model.Calls.ShouldBe(1);

        // ActivatorUtilities must select the owner-aware overload as well, with or without the optional receiver.
        var activated = ActivatorUtilities.CreateInstance<EventBus<BaseEvent>>(provider);
        ((IExactEventBus)activated).Subscriptions.ShouldHaveSingleItem();
        calls.ShouldBe(2);
        await activated.PublishAsync(new BaseEvent());
    }

    [Fact]
    public async Task Concurrent_Plans_For_Different_Runtime_Types_Share_The_Exact_Bus()
    {
        var calls = 0;
        var services = Services();
        services.AddTransient<IEventHandler<BaseEvent>>(_ => { Interlocked.Increment(ref calls); return new Handler(); });
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<EventRegistry>();
        var plans = await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(() => registry.GetDispatchPlan(i % 2 == 0 ? typeof(DerivedEvent) : typeof(OtherEvent)))));

        calls.ShouldBe(1);
        plans.Select(p => p.Subscriptions.Single().Handler).Distinct().Count().ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_Contract_Reads_Reuse_The_Bus_Subscription_Snapshot(bool warmup)
    {
        using var isolation = new EventBusIsolationScope();
        var activations = 0;
        var services = Services();
        services.AddTransient<IEventHandler<BaseEvent>>(_ => { Interlocked.Increment(ref activations); return new Handler(); });
        using var provider = services.BuildServiceProvider();
        provider.UseMessaging(o =>
        {
            o.Events.EnableInheritance = true;
            if (warmup)
                o.Warmup();
        });
        var bus = provider.GetRequiredService<EventBus<BaseEvent>>();
        var registry = provider.GetRequiredService<EventRegistry>();
        var snapshots = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => registry.GetContractSubscriptions(typeof(BaseEvent)))));
        var owned = ((IExactEventBus)bus).Subscriptions;

        foreach (var snapshot in snapshots)
            snapshot.ShouldBeSameAs(owned);
        activations.ShouldBe(1);

        var model = new DerivedEvent();
        await provider.GetRequiredService<EventBus<DerivedEvent>>().PublishAsync(model);
        model.Calls.ShouldBe(1);
        ((IExactEventBus)bus).Subscriptions.ShouldBeSameAs(owned);
    }

    [Fact]
    public void Discovered_Activation_Uses_Owner_Root_During_Another_Hosts_Request()
    {
        var accessor = new HttpContextAccessor();
        var previous = accessor.HttpContext;
        var servicesA = Services(typeof(DependencyHandler));
        servicesA.AddSingleton(new Dependency("A"));
        var servicesB = Services(typeof(DependencyHandler));
        servicesB.AddSingleton(new Dependency("B"));
        using var a = servicesA.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var b = servicesB.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = b.CreateScope();
        try
        {
            accessor.HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            var subscription = a.GetRequiredService<EventRegistry>().GetDispatchPlan(typeof(DerivedEvent)).Subscriptions.Single();
            ((DependencyHandler)subscription.Handler).Dependency.Host.ShouldBe("A");
        }
        finally
        {
            accessor.HttpContext = previous;
        }
    }

    [Fact]
    public void Discovered_Handlers_Do_Not_Capture_Scoped_Dependencies()
    {
        var services = Services(typeof(DependencyHandler));
        services.AddScoped(_ => new Dependency("scope"));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<EventRegistry>().GetDispatchPlan(typeof(DerivedEvent)));
    }

    static IServiceCollection Services(params Type[] handlers)
    {
        var services = new ServiceCollection();
        services.AddMessaging(handlers.ToList());
        services.AddHttpContextAccessor();
        return services;
    }

    public class BaseEvent { public int Calls; }
    public sealed class DerivedEvent : BaseEvent;
    public sealed class OtherEvent : BaseEvent;
    public sealed record Dependency(string Host);
    public sealed class Handler : IEventHandler<BaseEvent>
    {
        public Task HandleAsync(BaseEvent eventModel, CancellationToken ct)
        {
            eventModel.Calls++;
            return Task.CompletedTask;
        }
    }
    public sealed class DependencyHandler(Dependency dependency) : IEventHandler<BaseEvent>
    {
        public Dependency Dependency { get; } = dependency;
        public Task HandleAsync(BaseEvent eventModel, CancellationToken ct) => Task.CompletedTask;
    }
}
