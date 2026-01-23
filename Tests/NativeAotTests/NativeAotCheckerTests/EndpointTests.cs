using NativeAotChecker.Greeting;

namespace NativeAotCheckerTests;

public class EndpointTests(App app)
{
    [Fact]
    public async Task User_Greeting_Endpoint_Response()
    {
        var (rsp, res) = await app.Client.POSTAsync<GreetingEndpoint, GreetingRequest, GreetingResponse>(
                             new()
                             {
                                 FirstName = "Jane",
                                 LastName = "Doe"
                             });

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.Message.ShouldBe("Hello Jane Doe!");
    }
}