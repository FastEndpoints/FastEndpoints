using System.Collections.Concurrent;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using RouteMapper;
using Shouldly;
using Xunit;

namespace EventBus;

[Collection(RouteMapperCollection.Name)]
public class EventBusInheritanceTests : IDisposable
{
    readonly EventBusIsolationScope _isolation = new();

    public void Dispose() => _isolation.Dispose();

    static ServiceProvider Provider(Type[] discovered, Action<IServiceCollection>? configure = null, bool full = false, bool enable = true)
    {
        var services = new ServiceCollection();
        if (full)
            services.AddFastEndpoints([typeof(StubEndpoint), ..discovered]);
        else
            services.AddMessaging(discovered.ToList());
        configure?.Invoke(services);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        provider.UseMessaging(o => o.Events.EnableInheritance = enable);
        return provider;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Runtime_Matching_Is_Identical_For_All_Publish_Entry_Contracts(bool full)
    {
        using var provider = Provider([typeof(Dual), typeof(AncestorHandler), typeof(InterfaceHandler), typeof(CatchAll), typeof(SiblingHandler)], full: full);
        var model = new Derived();
        Base asBase = model;
        IEvent asEvent = model;
        Func<Task>[] publishes =
        [
            () => model.PublishAsync(),
            () => asBase.PublishAsync(),
            () => asEvent.PublishAsync(),
            () => asEvent.PublishAsync<IEvent>(),
            () => asBase.PublishFilteredAsync(_ => true),
            () => provider.GetRequiredService<EventBus<Base>>().PublishAsync(model)
        ];
        foreach (var publish in publishes)
        {
            await publish();
            model.Calls.Order().ShouldBe(new[] { "base", "derived", "ancestor", "interface", "all" }.Order());
            model.Calls.Clear();
        }
    }

    [Fact]
    public async Task Duplicate_Discovery_Diamond_Interfaces_And_Dual_Methods_Are_Deduplicated_Per_Contract()
    {
        var types = new List<Type> { typeof(Dual), typeof(InterfaceHandler), typeof(Dual) };
        using var provider = new ServiceCollection().AddMessaging(types, types).BuildServiceProvider();
        provider.UseMessaging(o => o.Events.EnableInheritance = true);
        var model = new Derived();

        await model.PublishAsync();

        model.Calls.Order().ShouldBe(new[] { "base", "derived", "interface" }.Order());
        var registry = provider.GetRequiredService<EventRegistry>();
        var dual = registry.GetDispatchPlan(typeof(Derived)).Subscriptions.Where(s => s.HandlerType == typeof(Dual)).ToArray();
        dual.Length.ShouldBe(2);
        dual[0].Handler.ShouldBeSameAs(dual[1].Handler);
    }

    [Fact]
    public async Task No_Exact_Handler_Is_Needed_And_More_Specific_Handlers_Are_Excluded()
    {
        using var provider = Provider([typeof(Dual), typeof(InterfaceHandler), typeof(SiblingHandler)]);
        var model = new OtherDerived();
        await model.PublishAsync();
        model.Calls.ShouldBe(["base"]);
        var plainBase = new Base();
        await plainBase.PublishAsync();
        plainBase.Calls.ShouldBe(["base"]);
    }

    [Fact]
    public async Task Registered_Variant_Closed_Contracts_Are_Matched_By_Assignability()
    {
        using var provider = Provider([], s => s.AddSingleton<IEventHandler<IEnvelope<object>>, EnvelopeHandler>());
        var model = new Envelope();

        await model.PublishAsync();

        model.Calls.ShouldBe(["envelope"]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DI_Overrides_Are_Per_Contract_And_Enumerable_Overrides_Are_Indexed(bool enumerable, bool full)
    {
        using var provider = Provider([typeof(Dual)], s =>
        {
            if (enumerable)
                s.AddSingleton<IEnumerable<IEventHandler<Base>>>([new Replacement(), new Replacement()]);
            else
            {
                s.AddSingleton<IEventHandler<Base>>(new Replacement());
                s.AddTransient<IEventHandler<Base>>(_ => new Replacement());
            }
            s.AddKeyedSingleton<IEventHandler<IEvent>>("ignored", new CatchAll());
        }, full);
        var model = new Derived();

        await model.PublishAsync();

        model.Calls.Order().ShouldBe(new[] { "replacement", "replacement", "derived" }.Order());
    }

    [Fact]
    public async Task Empty_Enumerable_Override_Falls_Back_To_Discovery()
    {
        using var provider = Provider([typeof(Dual)], s => s.AddSingleton<IEnumerable<IEventHandler<Base>>>([]));
        var model = new Derived();
        await model.PublishAsync();
        model.Calls.Order().ShouldBe(new[] { "base", "derived" }.Order());
    }

    [Fact]
    public async Task Explicit_Contract_Does_Not_Implicitly_Subscribe_An_Implementations_Other_Interfaces()
    {
        using var provider = Provider([], s => s.AddSingleton<IEventHandler<Base>, Dual>());
        var model = new Derived();
        await model.PublishAsync();
        model.Calls.ShouldBe(["base"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Open_Generic_Handlers_Fan_Out_Per_Closed_Contract_With_Constraints_And_Closed_Overrides(bool constrained)
    {
        using var provider = Provider([], s =>
        {
            s.AddTransient(typeof(IEventHandler<>), constrained ? typeof(Constrained<>) : typeof(Open<>));
            s.AddSingleton<IEventHandler<Base>, Replacement>();
        });
        var model = new Derived();
        await model.PublishAsync();
        var expected = constrained
                           ? new[] { nameof(Derived), nameof(Base), "replacement" }
                           : new[] { nameof(Derived), nameof(Base), nameof(Ancestor), nameof(Object), nameof(IEvent), nameof(IContract), nameof(ILeft), nameof(IRight), "replacement" };
        model.Calls.Order().ShouldBe(expected.Order());
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Empty_And_Single_Subscription_Filters_Do_Not_Allocate(bool enable, bool subscribed, bool include)
    {
        var handler = new CountingHandler();
        using var provider = Provider([], s =>
        {
            if (subscribed)
                s.AddSingleton<IEventHandler<Base>>(handler);
        }, enable: enable);
        var bus = provider.GetRequiredService<EventBus<Base>>();
        var model = new Base();
        var filterCalls = 0;
        Type? handlerType = null;
        Func<Type, bool> filter = t =>
        {
            filterCalls++;
            handlerType = t;
            return include;
        };
        for (var i = 0; i < 100; i++)
            await bus.PublishFilteredAsync(model, filter, Mode.WaitForAll);

        filterCalls = 0;
        handler.Calls = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            await bus.PublishFilteredAsync(model, filter, Mode.WaitForAll);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
        filterCalls.ShouldBe(subscribed ? 1000 : 0);
        handlerType.ShouldBe(subscribed ? typeof(CountingHandler) : null);
        handler.Calls.ShouldBe(subscribed && include ? 1000 : 0);
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, -1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task Multi_Subscription_Filter_Preserves_Order_And_Per_Publish_Decisions(bool enable, int rejected)
    {
        var first = new OrderedHandler<int>("first");
        var middle = new OrderedHandler<string>("middle");
        var last = new OrderedHandler<double>("last");
        using var provider = Provider([], s =>
        {
            s.AddSingleton<IEventHandler<Base>>(first);
            s.AddSingleton<IEventHandler<Base>>(middle);
            s.AddSingleton<IEventHandler<Base>>(first);
            s.AddSingleton<IEventHandler<Base>>(last);
        }, enable: enable);
        var bus = provider.GetRequiredService<EventBus<Base>>();
        Type[] types = [first.GetType(), middle.GetType(), last.GetType()];
        string[] names = ["first", "middle", "first", "last"];
        int[] positions = [0, 1, 0, 2];
        var decisions = new Dictionary<Type, int>();
        var model = new Derived();
        Func<Type, bool> filter = t =>
        {
            model.Calls.ShouldBeEmpty();
            decisions[t] = decisions.GetValueOrDefault(t) + 1;
            return rejected < 0 || t != types[rejected];
        };

        for (var publish = 0; publish < 2; publish++)
        {
            await bus.PublishFilteredAsync(model, filter, Mode.WaitForAll);

            model.Calls.ShouldBe(names.Where((_, i) => positions[i] != rejected));
            decisions.Keys.ShouldBe(types);
            decisions.Values.ShouldAllBe(n => n == 1);
            model = new Derived();
            decisions.Clear();
            rejected = (rejected + 1) % types.Length;
        }
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(false, 16)]
    [InlineData(false, 256)]
    [InlineData(true, 4)]
    [InlineData(true, 16)]
    [InlineData(true, 256)]
    public async Task Multi_Subscription_Identity_Filter_Does_Not_Allocate_Beyond_Dispatch(bool enable, int subscriptions)
    {
        var handler = new CountingHandler();
        using var provider = Provider([], s =>
        {
            for (var i = 0; i < subscriptions; i++)
                s.AddSingleton<IEventHandler<Base>>(handler);
        }, enable: enable);
        var bus = provider.GetRequiredService<EventBus<Base>>();
        var model = new Derived();
        Func<Type, bool> filter = static _ => true;
        // A GC can trim shared pool buffers. Warm and measure without GC to test steady-state allocations.
        GC.TryStartNoGCRegion(16 * 1024 * 1024).ShouldBeTrue();
        try
        {
            for (var i = 0; i < 100; i++)
            {
                await bus.PublishAsync(model, Mode.WaitForAll);
                await bus.PublishFilteredAsync(model, filter, Mode.WaitForAll);
            }

            const int iterations = 1000;
            handler.Calls = 0;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < iterations; i++)
                await bus.PublishAsync(model, Mode.WaitForAll);
            var unfiltered = GC.GetAllocatedBytesForCurrentThread() - before;

            before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < iterations; i++)
                await bus.PublishFilteredAsync(model, filter, Mode.WaitForAll);
            var filtered = GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.Current.TestOutputHelper!.WriteLine($"Unfiltered: {unfiltered}; filtered: {filtered}; extra bytes/publish: {(filtered - unfiltered) / iterations}");
            // Filtering reuses cached type indexes and adds no allocations beyond task aggregation.
            filtered.ShouldBe(unfiltered);
            handler.Calls.ShouldBe(2 * iterations * subscriptions);
        }
        finally
        {
            GC.EndNoGCRegion();
        }
    }

    [Theory]
    [InlineData(false, false, 4)]
    [InlineData(false, false, 256)]
    [InlineData(true, false, 4)]
    [InlineData(true, false, 256)]
    [InlineData(true, true, 4)]
    [InlineData(true, true, 256)]
    public async Task Stateful_Filter_Decisions_Are_Reused_For_Each_Implementation(bool enable, bool direct, int subscriptions)
    {
        IEventHandler<Base>[] implementations = [new OrderedHandler<int>("first"), new OrderedHandler<string>("middle"), new OrderedHandler<double>("last")];
        var handlers = Enumerable.Range(0, subscriptions).Select(i => implementations[i % implementations.Length]).ToArray();
        using var provider = Provider([], s => s.AddSingleton<IEnumerable<IEventHandler<Base>>>(handlers), enable: enable);
        var bus = direct ? new EventBus<Base>(handlers) : provider.GetRequiredService<EventBus<Base>>();
        string[] names = ["first", "middle", "last"];

        foreach (var includeFirst in new[] { false, true })
        {
            var model = new Derived();
            var decisions = new List<Type>();
            await bus.PublishFilteredAsync(model, t =>
            {
                decisions.Add(t);
                return (decisions.Count == 1) == includeFirst;
            });

            decisions.ShouldBe(implementations.Select(h => h.GetType()));
            model.Calls.ShouldBe(Enumerable.Range(0, subscriptions)
                                          .Where(i => (i % implementations.Length == 0) == includeFirst)
                                          .Select(i => names[i % names.Length]));
        }
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(false, 256)]
    [InlineData(true, 4)]
    [InlineData(true, 256)]
    public async Task Reentrant_Filters_Keep_Their_Decisions_Separate(bool enable, int subscriptions)
    {
        IEventHandler<Base>[] implementations = [new OrderedHandler<int>("first"), new OrderedHandler<string>("second")];
        var handlers = Enumerable.Range(0, subscriptions).Select(i => implementations[i % implementations.Length]).ToArray();
        using var provider = Provider([], s => s.AddSingleton<IEnumerable<IEventHandler<Base>>>(handlers), enable: enable);
        var bus = provider.GetRequiredService<EventBus<Base>>();
        var outer = new Derived();
        var inner = new Derived();
        Task? nested = null;

        await bus.PublishFilteredAsync(outer, t =>
        {
            if (t != implementations[0].GetType())
                return false;
            nested = bus.PublishFilteredAsync(inner, type => type == implementations[1].GetType());
            return true;
        });
        await nested.ShouldNotBeNull();

        outer.Calls.ShouldBe(Enumerable.Repeat("first", subscriptions / 2));
        inner.Calls.ShouldBe(Enumerable.Repeat("second", subscriptions / 2));
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(false, 256)]
    [InlineData(true, 4)]
    [InlineData(true, 256)]
    public async Task Throwing_Filter_Delivers_Nothing_And_Next_Publish_Reevaluates(bool enable, int subscriptions)
    {
        IEventHandler<Base>[] implementations = [new OrderedHandler<int>("first"), new OrderedHandler<string>("second")];
        var handlers = Enumerable.Range(0, subscriptions).Select(i => implementations[i % implementations.Length]).ToArray();
        using var provider = Provider([], s => s.AddSingleton<IEnumerable<IEventHandler<Base>>>(handlers), enable: enable);
        var bus = provider.GetRequiredService<EventBus<Base>>();
        var model = new Derived();
        var failure = new InvalidOperationException("filter failed");

        Should.Throw<InvalidOperationException>(() =>
        {
            _ = bus.PublishFilteredAsync(model, t => t == implementations[0].GetType() ? true : throw failure);
        }).ShouldBeSameAs(failure);
        model.Calls.ShouldBeEmpty();

        await bus.PublishFilteredAsync(model, _ => false);
        model.Calls.ShouldBeEmpty();
        await bus.PublishFilteredAsync(model, t => t == implementations[1].GetType());
        model.Calls.ShouldBe(Enumerable.Repeat("second", subscriptions / 2));
    }

    sealed class OrderedHandler<T>(string name) : IEventHandler<Base>
    {
        public Task HandleAsync(Base model, CancellationToken ct)
        {
            model.Calls.Enqueue(name);
            return Task.CompletedTask;
        }
    }

    sealed class CountingHandler : IEventHandler<Base>
    {
        public int Calls;

        public Task HandleAsync(Base model, CancellationToken ct)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Filter_Sees_Actual_Implementations_Once_And_Receiver_Observes_Only_The_Entry_Contract()
    {
        using var provider = Provider([typeof(Dual), typeof(InterfaceHandler)], s => s.RegisterTestEventReceivers());
        var model = new Derived();
        var decisions = new Dictionary<Type, int>();
        await provider.GetRequiredService<EventBus<Base>>().PublishFilteredAsync(model, t =>
        {
            decisions[t] = decisions.GetValueOrDefault(t) + 1;
            return t == typeof(Dual);
        });
        decisions.Count.ShouldBe(2);
        decisions.Values.ShouldAllBe(n => n == 1);
        model.Calls.Order().ShouldBe(new[] { "base", "derived" }.Order());
        var observed = await provider.GetTestEventReceiver<Base>().WaitForMatchAsync(e => ReferenceEquals(e, model));
        observed.ShouldHaveSingleItem();
        var second = new Derived();
        await provider.GetRequiredService<EventBus<Base>>().PublishFilteredAsync(second, _ => false);
        (await provider.GetTestEventReceiver<Base>().WaitForMatchAsync(e => ReferenceEquals(e, second))).ShouldHaveSingleItem();
        second.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(Mode.WaitForAll, false)]
    [InlineData(Mode.WaitForAny, false)]
    [InlineData(Mode.WaitForNone, false)]
    [InlineData(Mode.WaitForAll, true)]
    [InlineData(Mode.WaitForAny, true)]
    [InlineData(Mode.WaitForNone, true)]
    public async Task Failed_Cold_Plan_Delivers_Nothing_And_Retries_Even_With_Rejecting_Filter(Mode mode, bool filtered)
    {
        var attempts = 0;
        var failure = new InvalidOperationException("activation failed once");
        using var provider = Provider([typeof(Dual)], s => s.AddTransient<IEventHandler<IContract>>(_ =>
        {
            if (Interlocked.Increment(ref attempts) == 1)
                throw failure;
            return new InterfaceHandler();
        }));
        var model = new Derived();
        var bus = provider.GetRequiredService<EventBus<Derived>>();
        Should.Throw<InvalidOperationException>(() =>
        {
            _ = filtered ? bus.PublishFilteredAsync(model, _ => false, mode) : bus.PublishAsync(model, mode);
        }).ShouldBeSameAs(failure);
        model.Calls.ShouldBeEmpty();

        await bus.PublishAsync(model);

        attempts.ShouldBe(2);
        model.Calls.Order().ShouldBe(new[] { "base", "derived", "interface" }.Order());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Early_Bus_Resolution_Does_Not_Freeze_Options_And_Startup_Calls_Preserve_Them(bool warmup)
    {
        var services = new ServiceCollection().AddMessaging([typeof(Dual)]);
        using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<EventBus<Base>>();
        provider.UseMessaging(o => o.Events.EnableInheritance = true);
        provider.UseMessaging();
        if (warmup)
            provider.UseMessaging(o => o.Warmup());
        var model = new Derived();
        await bus.PublishAsync(model);
        model.Calls.Order().ShouldBe(new[] { "base", "derived" }.Order());
        provider.UseMessaging(o => o.Events.EnableInheritance = true);
        Should.Throw<InvalidOperationException>(() => provider.UseMessaging(o => o.Events.EnableInheritance = false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_Host_Publish_Or_Warmup_Freezes_The_Default(bool warmup)
    {
        using var provider = Provider([], enable: false);
        if (warmup)
            provider.UseMessaging(o => o.Warmup());
        else
            await provider.GetRequiredService<EventBus<Derived>>().PublishAsync(new());
        Should.Throw<InvalidOperationException>(() => provider.UseMessaging(o => o.Events.EnableInheritance = true));
        new MessagingOptions().Events.EnableInheritance.ShouldBeFalse();
    }

    [Fact]
    public async Task Option_Setter_And_Freeze_Have_One_Atomic_Winner()
    {
        for (var i = 0; i < 100; i++)
        {
            var options = new EventOptions();
            var accepted = false;
            var setter = Task.Run(() =>
            {
                try { options.EnableInheritance = true; accepted = true; }
                catch (InvalidOperationException) { }
            });
            var freeze = Task.Run(options.Freeze);
            await Task.WhenAll(setter, freeze);
            (await freeze).ShouldBe(accepted);
            options.EnableInheritance.ShouldBe(accepted);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Providers_Built_From_One_Unchanged_Collection_Own_Options_Instances_And_Plans(bool reverse)
    {
        var services = new ServiceCollection().AddMessaging([typeof(Dual)]);
        using var a = services.BuildServiceProvider();
        using var b = services.BuildServiceProvider();
        var first = reverse ? b : a;
        var second = reverse ? a : b;
        first.UseMessaging(o => o.Events.EnableInheritance = true);
        second.UseMessaging();
        var model = new Derived();
        await first.GetRequiredService<EventBus<Base>>().PublishAsync(model);
        model.Calls.Order().ShouldBe(new[] { "base", "derived" }.Order());
        model.Calls.Clear();
        await second.GetRequiredService<EventBus<Base>>().PublishAsync(model);
        model.Calls.ShouldBe(["base"]);
        first.GetRequiredService<EventOptions>().ShouldNotBeSameAs(second.GetRequiredService<EventOptions>());
        ((IExactEventBus)first.GetRequiredService<EventBus<Base>>()).Subscriptions[0].Handler
            .ShouldNotBeSameAs(((IExactEventBus)second.GetRequiredService<EventBus<Base>>()).Subscriptions[0].Handler);
    }

    [Fact]
    public async Task Separate_Host_Catalogs_Remain_Isolated_After_The_Other_Host_Is_Disposed()
    {
        using var a = Provider([typeof(Dual)]);
        var aBus = a.GetRequiredService<EventBus<Base>>();
        using (var b = Provider([typeof(Replacement)], enable: false))
        {
            var model = new Derived();
            await b.GetRequiredService<EventBus<Base>>().PublishAsync(model);
            model.Calls.ShouldBe(["replacement"]);
        }
        var first = new Derived();
        await aBus.PublishAsync(first);
        first.Calls.Order().ShouldBe(new[] { "base", "derived" }.Order());
    }

    [Fact]
    public async Task Legacy_Local_Handler_List_Ignores_Host_Inheritance_And_Does_Not_Freeze_Options()
    {
        using var provider = Provider([typeof(Dual)]);
        var model = new Derived();
        await new EventBus<Base>([new Dual()]).PublishAsync(model);
        model.Calls.ShouldBe(["base"]);
        provider.UseMessaging(o => o.Events.EnableInheritance = false);
    }

    [Fact]
    public async Task Container_Owns_Disposal_Of_A_Shared_Transient_Handler()
    {
        var handler = new DisposableHandler();
        using (var provider = Provider([], s => s.AddTransient<IEventHandler<Base>>(_ => handler)))
        {
            await new Derived().PublishAsync();
            await new OtherDerived().PublishAsync();
            handler.DisposeCount.ShouldBe(0);
        }
        handler.DisposeCount.ShouldBe(1);
    }

    public class Ancestor : IEvent { public ConcurrentQueue<string> Calls { get; } = new(); }
    public sealed class StubEndpoint : EndpointWithoutRequest
    {
        public override void Configure() { Get("event-inheritance-unit"); AllowAnonymous(); }
        public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
    }
    public class Base : Ancestor;
    public sealed class Derived : Base, ILeft, IRight;
    public sealed class OtherDerived : Base;
    public sealed class Sibling : Base;
    public interface IContract : IEvent;
    public interface ILeft : IContract;
    public interface IRight : IContract;
    public interface IEnvelope<out T> : IEvent { T Value { get; } }
    public sealed class Envelope : Ancestor, IEnvelope<string> { public string Value => "value"; }

    public sealed class Dual : IEventHandler<Base>, IEventHandler<Derived>
    {
        Task IEventHandler<Base>.HandleAsync(Base e, CancellationToken ct) { e.Calls.Enqueue("base"); return Task.CompletedTask; }
        Task IEventHandler<Derived>.HandleAsync(Derived e, CancellationToken ct) { e.Calls.Enqueue("derived"); return Task.CompletedTask; }
    }
    public sealed class AncestorHandler : IEventHandler<Ancestor>
    {
        public Task HandleAsync(Ancestor e, CancellationToken ct) { e.Calls.Enqueue("ancestor"); return Task.CompletedTask; }
    }
    public sealed class InterfaceHandler : IEventHandler<IContract>
    {
        public Task HandleAsync(IContract e, CancellationToken ct) { ((Ancestor)e).Calls.Enqueue("interface"); return Task.CompletedTask; }
    }
    public sealed class CatchAll : IEventHandler<IEvent>
    {
        public Task HandleAsync(IEvent e, CancellationToken ct) { ((Ancestor)e).Calls.Enqueue("all"); return Task.CompletedTask; }
    }
    public sealed class SiblingHandler : IEventHandler<Sibling>
    {
        public Task HandleAsync(Sibling e, CancellationToken ct) { e.Calls.Enqueue("sibling"); return Task.CompletedTask; }
    }
    public sealed class Replacement : IEventHandler<Base>
    {
        public Task HandleAsync(Base e, CancellationToken ct) { e.Calls.Enqueue("replacement"); return Task.CompletedTask; }
    }
    public sealed class EnvelopeHandler : IEventHandler<IEnvelope<object>>
    {
        public Task HandleAsync(IEnvelope<object> e, CancellationToken ct) { ((Ancestor)e).Calls.Enqueue("envelope"); return Task.CompletedTask; }
    }
    public sealed class Open<T> : IEventHandler<T>
    {
        public Task HandleAsync(T e, CancellationToken ct) { ((Ancestor)(object)e!).Calls.Enqueue(typeof(T).Name); return Task.CompletedTask; }
    }
    public sealed class Constrained<T> : IEventHandler<T> where T : Base
    {
        public Task HandleAsync(T e, CancellationToken ct) { e.Calls.Enqueue(typeof(T).Name); return Task.CompletedTask; }
    }
    public sealed class DisposableHandler : IEventHandler<Base>, IDisposable
    {
        public int DisposeCount;
        public void Dispose() => DisposeCount++;
        public Task HandleAsync(Base e, CancellationToken ct) => Task.CompletedTask;
    }
}
