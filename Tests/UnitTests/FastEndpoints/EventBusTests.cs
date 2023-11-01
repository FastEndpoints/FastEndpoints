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

        var evnt = Factory.CreateEvent(
            new[]
            {
                fakeHandler
            });
        await evnt.PublishAsync();
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

        await new EventBus<NewItemAddedToStock>(handlers).PublishAsync(event1, Mode.WaitForNone);
        await new EventBus<NewItemAddedToStock>(handlers).PublishAsync(event2, Mode.WaitForAny);

        await Task.Delay(100);

        event2.ID.Should().Be(0);
        event2.Name.Should().Be("pass");

        event1.ID.Should().Be(0);
        event1.Name.Should().Be("pass");
    }

    [Fact]
    public async Task HandlerLogicThrowsException()
    {
        var logger = A.Fake<ILogger<NotifyCustomers>>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => new EventBus<NewItemAddedToStock>(
                new[]
                {
                    new NotifyCustomers(logger)
                }).PublishAsync(new()));
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

        await new NewItemAddedToStock { Name = "xyz" }.PublishAsync();

        fakeHandler.Name.Should().Be("xyz");
    }
}

file class FakeEventHandler : IEventHandler<NewItemAddedToStock>
{
    public string? Name { get; set; }

    public Task HandleAsync(NewItemAddedToStock eventModel, CancellationToken ct)
    {
        Name = eventModel.Name;

        return Task.CompletedTask;
    }
}