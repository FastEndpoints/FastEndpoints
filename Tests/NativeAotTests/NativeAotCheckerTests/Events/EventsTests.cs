using NativeAotChecker.Endpoints.Events;

namespace NativeAotCheckerTests;

public class EventsTests(App app)
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
}
