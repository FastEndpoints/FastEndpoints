using NativeAotChecker.Endpoints.Events;

namespace NativeAotCheckerTests;

public class EventsTests(App app) : TestBase<App>
{
    [Fact]
    public async Task Event_Publish()
    {
        var id = Guid.NewGuid();
        var (rsp, res, err) = await app.Client.POSTAsync<EventPublishEndpoint, EventPublishRequest, Guid>(new() { Id = id });

        if (rsp.IsSuccessStatusCode)
            res.ShouldBe(id);
        else
            Assert.Fail(err);
    }

    [Fact]
    public async Task Record_Event_Handler_Is_Discovered()
    {
        var id = Guid.NewGuid();
        var (rsp, res, err) = await app.Client.POSTAsync<RecordEventHandlerEndpoint, RecordEventHandlerRequest, Guid>(new() { Id = id });

        if (rsp.IsSuccessStatusCode)
            res.ShouldBe(id);
        else
            Assert.Fail(err);
    }
}