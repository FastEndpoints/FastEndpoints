using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TestCases.EventInheritance;

namespace Messaging;

public class EventBusInheritanceSut : AppFixture<Web.Program>
{
    protected override void ConfigureApp(IWebHostBuilder app)
        => app.UseSetting("Testing:EventInheritance", "true");

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RegisterTestEventHandler<BaseEvent, ReplacementHandler>();
        services.RegisterTestEventReceivers();
    }
}

public class EventBusInheritanceTests(EventBusInheritanceSut app) : TestBase<EventBusInheritanceSut>
{
    [Fact]
    public async Task Endpoint_Base_Publish_Uses_Runtime_Type_And_Replaces_Only_The_Base_Subscription()
    {
        var id = Guid.NewGuid();
        var (response, result) = await app.Client.POSTAsync<Endpoint, Request, Response>(new() { Id = id });

        response.IsSuccessStatusCode.ShouldBeTrue();
        result.BaseCalls.ShouldBe(0);
        result.DerivedCalls.ShouldBe(1);
        result.ReplacementCalls.ShouldBe(1);
        var observed = await app.Services.GetTestEventReceiver<BaseEvent>().WaitForMatchAsync(e => e.Id == id, ct: Cancellation);
        observed.ShouldHaveSingleItem().ShouldBeOfType<DerivedEvent>();
    }
}

[DontRegister]
public sealed class ReplacementHandler : IEventHandler<BaseEvent>
{
    public Task HandleAsync(BaseEvent model, CancellationToken ct)
    {
        model.Result.ReplacementCalls++;
        return Task.CompletedTask;
    }
}
