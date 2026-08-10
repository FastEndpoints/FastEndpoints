using System.Collections.Concurrent;
using FakeItEasy;
using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TestCases.EventHandlingTest;
using Xunit;

namespace EventBus;

public class EventBusTests
{
    [Fact]
    public async Task AbilityToFakeAnEventHandler()
    {
        var fakeHandler = A.Fake<IEventHandler<NewItemAddedToStock>>();

        A.CallTo(() => fakeHandler.HandleAsync(A<NewItemAddedToStock>.Ignored, A<CancellationToken>.Ignored))
         .Returns(Task.CompletedTask)
         .Once();

        var evnt = Factory.CreateEvent([fakeHandler]);
        await evnt.PublishAsync(cancellation: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EventHandlersExecuteSuccessfully()
    {
        var logger = A.Fake<ILogger<NotifyCustomers>>();

        var event1 = new NewItemAddedToStock { ID = 1, Name = "one", Quantity = 10 };
        var event2 = new NewItemAddedToStock { ID = 2, Name = "two", Quantity = 20 };

        var handlers = new IEventHandler<NewItemAddedToStock>[]
        {
            new NotifyCustomers(logger),
            new UpdateInventoryLevel()
        };

        await new EventBus<NewItemAddedToStock>(handlers).PublishAsync(event1, Mode.WaitForNone, TestContext.Current.CancellationToken);
        await new EventBus<NewItemAddedToStock>(handlers).PublishAsync(event2, Mode.WaitForAny, TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        event2.ID.ShouldBe(0);
        event2.Name.ShouldBe("pass");

        event1.ID.ShouldBe(0);
        event1.Name.ShouldBe("pass");
    }

    [Fact]
    public async Task HandlerLogicThrowsException()
    {
        var logger = A.Fake<ILogger<NotifyCustomers>>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => new EventBus<NewItemAddedToStock>([new NotifyCustomers(logger)]).PublishAsync(
                new(),
                cancellation: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitForAllInvokesEveryHandler()
    {
        var evnt = new TrackedEvent();

        await new EventBus<TrackedEvent>([new HandlerA(), new HandlerB()])
            .PublishAsync(evnt, Mode.WaitForAll, TestContext.Current.CancellationToken);

        evnt.Visited.OrderBy(x => x).ShouldBe(["A", "B"]);
    }

    [Fact]
    public async Task PublishFilteredOnlyInvokesMatchingHandlers()
    {
        var evnt = new TrackedEvent();

        await new EventBus<TrackedEvent>([new HandlerA(), new HandlerB()])
            .PublishFilteredAsync(evnt, t => t == typeof(HandlerA), Mode.WaitForAll, TestContext.Current.CancellationToken);

        evnt.Visited.ShouldBe(["A"]);
    }

    [Fact]
    public async Task PublishFilteredWithNoMatchingHandlersIsNoOp()
    {
        var evnt = new TrackedEvent();

        await new EventBus<TrackedEvent>([new HandlerA(), new HandlerB()])
            .PublishFilteredAsync(evnt, _ => false, Mode.WaitForAll, TestContext.Current.CancellationToken);

        evnt.Visited.ShouldBeEmpty();
    }

    [Fact]
    public async Task WaitForNoneDoesNotSurfaceHandlerExceptions()
    {
        var evnt = new TrackedEvent();

        //the throwing handler comes first, so it must neither reach the publisher nor prevent the second handler from running
        await new EventBus<TrackedEvent>([new ThrowingHandler(), new HandlerB()])
            .PublishAsync(evnt, Mode.WaitForNone, TestContext.Current.CancellationToken);

        await evnt.Signal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        evnt.Visited.ShouldBe(["B"]);
    }

    [Fact]
    public async Task WaitForAnySingleHandlerDoesNotSurfaceSyncExceptions()
    {
        //same isolation as WaitForNone: Task.Run keeps a sync throw off the publisher thread
        var task = new EventBus<TrackedEvent>([new ThrowingHandler()])
            .PublishAsync(new TrackedEvent(), Mode.WaitForAny, TestContext.Current.CancellationToken);

        //WhenAny/offload returns a non-faulted task to the publisher; exceptions are not capturable
        await task;
        task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task RegisterFakeEventHandlerAndPublish()
    {
        var fakeHandler = new FakeEventHandler();

        Factory.RegisterTestServices(
            s =>
            {
                s.AddSingleton<IEventHandler<NewItemAddedToStock>>(fakeHandler);
            });

        await new NewItemAddedToStock { Name = "xyz" }.PublishAsync(cancellation: TestContext.Current.CancellationToken);

        fakeHandler.Name.ShouldBe("xyz");
    }
}

file class TrackedEvent
{
    public ConcurrentQueue<string> Visited { get; } = new();
    public TaskCompletionSource Signal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

file class HandlerA : IEventHandler<TrackedEvent>
{
    public Task HandleAsync(TrackedEvent eventModel, CancellationToken ct)
    {
        eventModel.Visited.Enqueue("A");

        return Task.CompletedTask;
    }
}

file class HandlerB : IEventHandler<TrackedEvent>
{
    public Task HandleAsync(TrackedEvent eventModel, CancellationToken ct)
    {
        eventModel.Visited.Enqueue("B");
        eventModel.Signal.TrySetResult();

        return Task.CompletedTask;
    }
}

file class ThrowingHandler : IEventHandler<TrackedEvent>
{
    public Task HandleAsync(TrackedEvent eventModel, CancellationToken ct)
        => throw new InvalidOperationException("this must never reach the publisher");
}

file class FakeEventHandler : IEventHandler<NewItemAddedToStock>
{
    public string? Name { get; private set; }

    public Task HandleAsync(NewItemAddedToStock eventModel, CancellationToken ct)
    {
        Name = eventModel.Name;

        return Task.CompletedTask;
    }
}