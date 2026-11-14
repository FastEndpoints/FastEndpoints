using System.Collections.Concurrent;
using FakeItEasy;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using RouteMapper;
using Shouldly;
using Xunit;

namespace EventBus;

[Collection(RouteMapperCollection.Name)]
public class EventBusCompatibilityTests : IDisposable
{
    readonly EventBusIsolationScope _isolation = new();

    public void Dispose() => _isolation.Dispose();

    [Fact]
    public async Task Default_Generic_Publishing_Uses_Declared_Contract_But_IEvent_Overload_Uses_Runtime_Type()
    {
        using var provider = new ServiceCollection().AddMessaging([typeof(BaseHandler), typeof(DerivedHandler), typeof(CatchAllHandler)]).BuildServiceProvider();
        provider.UseMessaging();
        var model = new DerivedEvent();

        await model.PublishAsync();
        model.Calls.ShouldBe(["derived"]);
        model.Calls.Clear();

        BaseEvent asBase = model;
        await asBase.PublishAsync();
        model.Calls.ShouldBe(["base"]);
        model.Calls.Clear();

        IEvent asInterface = model;
        await asInterface.PublishAsync();
        model.Calls.ShouldBe(["derived"]);
        model.Calls.Clear();

        await asInterface.PublishAsync<IEvent>();
        model.Calls.ShouldBe(["all"]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Explicit_Enumerable_Overrides_Individual_Registrations_And_Only_Nonempty_Overrides_Discovery(bool empty, bool registerFirst)
    {
        var services = new ServiceCollection();
        if (!registerFirst)
            services.AddMessaging([typeof(BaseHandler)]);
        services.AddSingleton<IEventHandler<BaseEvent>>(new NamedHandler("individual"));
        services.AddSingleton<IEnumerable<IEventHandler<BaseEvent>>>(empty ? [] : [new NamedHandler("enumerable")]);
        if (registerFirst)
            services.AddMessaging([typeof(BaseHandler)]);
        using var provider = services.BuildServiceProvider();
        provider.UseMessaging();
        var model = new BaseEvent();

        await model.PublishAsync();

        model.Calls.ShouldBe([empty ? "base" : "enumerable"]);
    }

    [Fact]
    public async Task Distinct_DI_Entries_Preserve_Order_And_Replace_Discovery()
    {
        var services = new ServiceCollection().AddMessaging([typeof(BaseHandler)]);
        services.AddTransient<IEventHandler<BaseEvent>>(_ => new NamedHandler("first"));
        services.AddSingleton<IEventHandler<BaseEvent>>(new NamedHandler("second"));
        using var provider = services.BuildServiceProvider();
        provider.UseMessaging();
        var model = new BaseEvent();

        await model.PublishAsync();

        model.Calls.ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task Legacy_Constructors_With_No_Handlers_Use_Discovery()
    {
        using var provider = new ServiceCollection().AddMessaging([typeof(BaseHandler)]).BuildServiceProvider();
        provider.UseMessaging();
        EventBus<BaseEvent>[] buses = [new(), new(null), new(null, null), new([])];

        foreach (var bus in buses)
        {
            var model = new BaseEvent();
            await bus.PublishAsync(model);
            model.Calls.ShouldBe(["base"]);
        }
    }

    [Fact]
    public async Task Legacy_Empty_Bus_Does_Not_Require_A_Resolver()
    {
        ServiceResolver.Instance = null!;
        EventBus<UnregisteredDto>[] buses = [new(), new(null), new(null, null), new([])];
        foreach (var bus in buses)
            await bus.PublishAsync(new());
    }

    [Fact]
    public async Task Legacy_Nonempty_Handler_List_Is_Local_And_Binds_Only_The_Declared_Interface()
    {
        using var provider = new ServiceCollection().AddMessaging([typeof(BaseHandler), typeof(DerivedHandler)]).BuildServiceProvider();
        provider.UseMessaging();
        var model = new DerivedEvent();

        await new EventBus<BaseEvent>([new DualHandler()]).PublishAsync(model);

        model.Calls.ShouldBe(["dual-base"]);
    }

    [Fact]
    public async Task Direct_Bus_Snapshots_The_Supplied_Handler_Array()
    {
        IEventHandler<BaseEvent>[] handlers = [new NamedHandler("first"), new NamedHandler("second")];
        var bus = new EventBus<BaseEvent>(handlers);
        handlers[1] = new BaseHandler();
        var model = new BaseEvent();
        List<Type> inspected = [];

        await bus.PublishFilteredAsync(model, type =>
        {
            inspected.Add(type);
            return type == typeof(NamedHandler);
        });

        inspected.ShouldBe([typeof(NamedHandler)]);
        model.Calls.ShouldBe(["first", "second"]);
        model.Calls.Clear();
        await bus.PublishAsync(model);
        model.Calls.ShouldBe(["first", "second"]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Provider_Bus_Snapshots_Handler_Arrays_Before_Any_Publish(bool inheritance, bool warmPlan)
    {
        IEventHandler<BaseEvent>[] handlers = [new NamedHandler("first"), new NamedHandler("second")];
        using var provider = new ServiceCollection().AddMessaging(new List<Type>())
                                                    .AddSingleton<IEnumerable<IEventHandler<BaseEvent>>>(handlers)
                                                    .BuildServiceProvider();
        provider.UseMessaging(o => o.Events.EnableInheritance = inheritance);
        var bus = provider.GetRequiredService<EventBus<BaseEvent>>();
        if (warmPlan)
            await bus.PublishAsync(new DerivedEvent());
        handlers[1] = new BaseHandler();
        var model = new DerivedEvent();
        List<Type> inspected = [];

        await bus.PublishFilteredAsync(model, type =>
        {
            inspected.Add(type);
            return type == typeof(NamedHandler);
        });

        inspected.ShouldBe([typeof(NamedHandler)]);
        model.Calls.ShouldBe(["first", "second"]);
        model.Calls.Clear();
        await bus.PublishAsync(model);
        model.Calls.ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task Factory_CreateEvent_Uses_Its_Enumerable_Override()
    {
        // Factory installs its own request-services resolver when none has been configured.
        ServiceResolver.Instance = null!;
        var model = Factory.CreateEvent<BaseEvent>([new NamedHandler("factory")]);

        await model.PublishAsync();

        model.Calls.ShouldBe(["factory"]);
    }

    [Theory]
    [InlineData(Mode.WaitForAll, 0)]
    [InlineData(Mode.WaitForAll, 1)]
    [InlineData(Mode.WaitForAll, 2)]
    [InlineData(Mode.WaitForAny, 0)]
    [InlineData(Mode.WaitForAny, 1)]
    [InlineData(Mode.WaitForAny, 2)]
    [InlineData(Mode.WaitForNone, 0)]
    [InlineData(Mode.WaitForNone, 1)]
    [InlineData(Mode.WaitForNone, 2)]
    public async Task Modes_Deliver_All_Selected_Handlers_And_Preserve_Token(Mode mode, int count)
    {
        var handlers = Enumerable.Range(0, count).Select(_ => new SignalingHandler()).ToArray();
        var model = new UnregisteredDto();
        var ct = TestContext.Current.CancellationToken;

        await new EventBus<UnregisteredDto>(handlers).PublishAsync(model, mode, ct);
        await EventBusDeliveryAssertions.VerifyAsync(handlers, h => h.Completed.Task, h => h.Model, h => h.Token, model, ct);
    }

    [Fact]
    public async Task Receiver_Observes_Once_Before_Filtering_Even_When_Nothing_Is_Selected()
    {
        var receiver = new Receiver<BaseEvent>();
        var model = new BaseEvent();
        var bus = new EventBus<BaseEvent>([new BaseHandler()], receiver);

        await bus.PublishFilteredAsync(model, t =>
        {
            receiver.Events.ShouldBe([model]);
            t.ShouldBe(typeof(BaseHandler));
            return false;
        });

        receiver.Events.ShouldBe([model]);
        model.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void WaitForAll_Synchronous_Throws_Are_Unwrapped_And_Stop_Later_Handlers()
    {
        var failure = new InvalidOperationException("handler");
        var model = new BaseEvent();
        var handler = A.Fake<IEventHandler<BaseEvent>>();
        A.CallTo(() => handler.HandleAsync(model, A<CancellationToken>._)).Throws(failure);
        var bus = new EventBus<BaseEvent>([handler, new BaseHandler()]);

        Should.Throw<InvalidOperationException>(() => bus.PublishAsync(model)).ShouldBeSameAs(failure);
        model.Calls.ShouldBeEmpty();
    }

    public class BaseEvent : IEvent
    {
        public ConcurrentQueue<string> Calls { get; } = new();
    }

    public sealed class DerivedEvent : BaseEvent;
    public sealed class UnregisteredDto;

    public sealed class BaseHandler : IEventHandler<BaseEvent>
    {
        public Task HandleAsync(BaseEvent eventModel, CancellationToken ct)
        {
            eventModel.Calls.Enqueue("base");
            return Task.CompletedTask;
        }
    }

    public sealed class DerivedHandler : IEventHandler<DerivedEvent>
    {
        public Task HandleAsync(DerivedEvent eventModel, CancellationToken ct)
        {
            eventModel.Calls.Enqueue("derived");
            return Task.CompletedTask;
        }
    }

    public sealed class CatchAllHandler : IEventHandler<IEvent>
    {
        public Task HandleAsync(IEvent eventModel, CancellationToken ct)
        {
            ((BaseEvent)eventModel).Calls.Enqueue("all");
            return Task.CompletedTask;
        }
    }

    public sealed class NamedHandler(string name) : IEventHandler<BaseEvent>
    {
        public Task HandleAsync(BaseEvent eventModel, CancellationToken ct)
        {
            eventModel.Calls.Enqueue(name);
            return Task.CompletedTask;
        }
    }

    public sealed class DualHandler : IEventHandler<BaseEvent>, IEventHandler<DerivedEvent>
    {
        Task IEventHandler<BaseEvent>.HandleAsync(BaseEvent eventModel, CancellationToken ct)
        {
            eventModel.Calls.Enqueue("dual-base");
            return Task.CompletedTask;
        }

        Task IEventHandler<DerivedEvent>.HandleAsync(DerivedEvent eventModel, CancellationToken ct)
        {
            eventModel.Calls.Enqueue("dual-derived");
            return Task.CompletedTask;
        }
    }

    sealed class SignalingHandler : IEventHandler<UnregisteredDto>
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; private set; }
        public UnregisteredDto? Model { get; private set; }

        public Task HandleAsync(UnregisteredDto eventModel, CancellationToken ct)
        {
            Model = eventModel;
            Token = ct;
            Completed.TrySetResult();
            return Task.CompletedTask;
        }
    }

    sealed class Receiver<T> : IEventReceiver<T> where T : notnull
    {
        public List<T> Events { get; } = [];
        void IEventReceiver<T>.AddEvent(T evnt) => Events.Add(evnt);
        public Task<IEnumerable<T>> WaitForMatchAsync(Func<T, bool> match, int timeoutSeconds = 2, CancellationToken ct = default)
            => Task.FromResult(Events.Where(match));
    }
}
