using TestCases.EventHandlingTest;
using Xunit;

namespace FastEndpoints.UnitTests;

public class EventBusTests
{
    [Fact]
    public async Task EventHandlersExecuteSuccessfully()
    {
        var event1 = new NewItemAddedToStock { ID = 1, Name = "one", Quantity = 10 };
        var event2 = new NewItemAddedToStock { ID = 2, Name = "two", Quantity = 20 };

        var handlers = new IEventHandler<NewItemAddedToStock>[]
        {
            new NotifyCustomers(),
            new UpdateInventoryLevel()
        };

        await new Event<NewItemAddedToStock>(handlers).PublishAsync(event1, Mode.WaitForNone);
        await new Event<NewItemAddedToStock>(handlers).PublishAsync(event2, Mode.WaitForAny);

        event1.ID.Should().Be(0);
        event2.ID.Should().Be(0);

        event1.Name.Should().Be("pass");
        event2.Name.Should().Be("pass");
    }

    [Fact]
    public async Task HandlerLogicThrowsException()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async ()
            => await new Event<NewItemAddedToStock>(new[] { new NotifyCustomers() })
                .PublishAsync(new NewItemAddedToStock()));
    }
}
