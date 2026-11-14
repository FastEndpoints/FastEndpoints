using FastEndpoints;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RouteMapper;
using Shouldly;
using Xunit;

namespace EventBus;

[Collection(RouteMapperCollection.Name)]
public class EventBusExecutionTests : IDisposable
{
    readonly IServiceResolver? _previousResolver = ServiceResolver.InstanceNotSet ? null : ServiceResolver.Instance;
    public void Dispose() => ServiceResolver.Instance = _previousResolver!;

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
    public async Task Inherited_Modes_Deliver_Each_Subscription_And_The_Original_Token(Mode mode, int count)
    {
        var handlers = Enumerable.Range(0, count).Select(_ => new SignalHandler()).ToArray();
        var services = new ServiceCollection().AddMessaging(new List<Type>());
        services.AddSingleton<IEnumerable<IEventHandler<Base>>>(handlers);
        using var provider = services.BuildServiceProvider();
        provider.UseMessaging(o => o.Events.EnableInheritance = true);
        var model = new Derived();
        var ct = TestContext.Current.CancellationToken;

        await provider.GetRequiredService<EventBus<Derived>>().PublishAsync(model, mode, ct);
        await EventBusDeliveryAssertions.VerifyAsync(handlers, h => h.Signal.Task, h => h.Model, h => h.Token, model, ct);
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
    public async Task Fault_Semantics_Match_For_Exact_And_Inherited_Dispatch(Mode mode, int failureKind)
    {
        foreach (var inheritance in new[] { false, true })
        foreach (var count in new[] { 1, 2 })
        {
            var services = new ServiceCollection().AddMessaging(new List<Type>());
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            var failure = new InvalidOperationException("original");
            var handler = A.Fake<IEventHandler<Base>>();
            var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var invocation = A.CallTo(() => handler.HandleAsync(A<Base>._, A<CancellationToken>._)).Invokes(() => invoked.TrySetResult());
            if (failureKind == 0)
                invocation.Throws(failure);
            else
                invocation.Returns(failureKind == 1 ? Task.FromException(failure) : Task.FromCanceled(canceled.Token));
            var later = new SignalHandler();
            services.AddSingleton<IEnumerable<IEventHandler<Base>>>(count == 1 ? [handler] : [handler, later]);
            using var provider = services.BuildServiceProvider();
            provider.UseMessaging(o => o.Events.EnableInheritance = inheritance);
            var bus = provider.GetRequiredService<EventBus<Base>>();

            if (mode == Mode.WaitForAll)
            {
                if (failureKind == 0)
                    Should.Throw<InvalidOperationException>(() => { _ = bus.PublishAsync(new Derived(), mode); }).ShouldBeSameAs(failure);
                else if (failureKind == 1)
                    (await Should.ThrowAsync<InvalidOperationException>(() => bus.PublishAsync(new Derived(), mode))).ShouldBeSameAs(failure);
                else
                    await Should.ThrowAsync<TaskCanceledException>(() => bus.PublishAsync(new Derived(), mode));
            }
            else
            {
                await bus.PublishAsync(new Derived(), mode);
                await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }

            if (count == 2)
            {
                if (mode == Mode.WaitForAll && failureKind == 0)
                    later.Signal.Task.IsCompleted.ShouldBeFalse();
                else
                    await later.Signal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
        }
    }

    [Theory]
    [InlineData(Mode.WaitForAll)]
    [InlineData(Mode.WaitForAny)]
    [InlineData(Mode.WaitForNone)]
    public async Task Already_Canceled_Token_Retains_Existing_Scheduling_Semantics(Mode mode)
    {
        foreach (var inheritance in new[] { false, true })
        foreach (var count in new[] { 1, 2 })
        {
            var handlers = Enumerable.Range(0, count).Select(_ => new SignalHandler()).ToArray();
            var services = new ServiceCollection().AddMessaging(new List<Type>())
                                                  .AddSingleton<IEnumerable<IEventHandler<Base>>>(handlers);
            using var provider = services.BuildServiceProvider();
            provider.UseMessaging(o => o.Events.EnableInheritance = inheritance);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await provider.GetRequiredService<EventBus<Base>>().PublishAsync(new Derived(), mode, cancellation.Token);

            foreach (var handler in handlers)
            {
                handler.Signal.Task.IsCompleted.ShouldBe(mode == Mode.WaitForAll);
                if (mode == Mode.WaitForAll)
                    handler.Token.ShouldBe(cancellation.Token);
            }
        }
    }

    [Fact]
    public async Task Jit_Value_Types_And_Unmarked_Dtos_Remain_Supported()
    {
        var calls = 0;
        var handler = A.Fake<IEventHandler<object>>();
        A.CallTo(() => handler.HandleAsync(A<object>._, A<CancellationToken>._)).Invokes(() => calls++).Returns(Task.CompletedTask);
        using var provider = new ServiceCollection().AddMessaging(new List<Type>()).AddSingleton(handler).BuildServiceProvider();
        provider.UseMessaging(o => o.Events.EnableInheritance = true);

        await provider.GetRequiredService<EventBus<ValueEvent>>().PublishAsync(new(42));
        await provider.GetRequiredService<EventBus<Derived>>().PublishAsync(new());
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task Custom_Resolver_And_Registered_Concrete_Discovered_Handlers_Are_Reused()
    {
        var handler = new SignalHandler();
        var custom = A.Fake<IServiceResolver>();
        A.CallTo(() => custom.CreateSingleton(typeof(SignalHandler))).Returns(handler);
        var services = new ServiceCollection().AddMessaging([typeof(SignalHandler)]).AddSingleton(custom);
        using var provider = services.BuildServiceProvider();
        provider.UseMessaging(o => o.Events.EnableInheritance = true);
        await provider.GetRequiredService<EventBus<Derived>>().PublishAsync(new());
        await provider.GetRequiredService<EventBus<Base>>().PublishAsync(new Derived());
        A.CallTo(() => custom.CreateSingleton(typeof(SignalHandler))).MustHaveHappenedOnceExactly();

        using var builtin = new ServiceCollection().AddMessaging([typeof(SignalHandler)]).AddSingleton(handler).BuildServiceProvider();
        builtin.UseMessaging(o => o.Events.EnableInheritance = true);
        builtin.GetRequiredService<EventRegistry>().GetDispatchPlan(typeof(Derived)).Subscriptions.Single().Handler.ShouldBeSameAs(handler);
    }

    [Fact]
    public async Task Factory_Enumerable_Overrides_Are_Used_By_Inherited_Publishing()
    {
        var previousContext = new HttpContextAccessor().HttpContext;
        ServiceResolver.Instance = null!;
        try
        {
            var handler = new FactoryHandler();
            var model = Factory.CreateEvent<FactoryBase>([handler]);
            var provider = ServiceResolver.Instance.Resolve<IServiceProvider>();
            provider.UseMessaging(o => o.Events.EnableInheritance = true);
            await provider.GetRequiredService<EventBus<FactoryDerived>>().PublishAsync(new());
            handler.Calls.ShouldBe(1);
            model.ShouldNotBeNull();
        }
        finally
        {
            new HttpContextAccessor().HttpContext = previousContext;
        }
    }

    public class Base;
    public sealed class Derived : Base;
    public readonly record struct ValueEvent(int Id);
    public class FactoryBase : IEvent;
    public sealed class FactoryDerived : FactoryBase;
    public sealed class FactoryHandler : IEventHandler<FactoryBase>
    {
        public int Calls;
        public Task HandleAsync(FactoryBase model, CancellationToken ct) { Calls++; return Task.CompletedTask; }
    }
    public sealed class SignalHandler : IEventHandler<Base>
    {
        public TaskCompletionSource Signal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Base? Model;
        public CancellationToken Token;
        public Task HandleAsync(Base model, CancellationToken ct)
        {
            Model = model;
            Token = ct;
            Signal.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
