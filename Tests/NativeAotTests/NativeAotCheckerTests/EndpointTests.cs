using NativeAotChecker.Greeting;

namespace NativeAotCheckerTests;

public class EndpointTests(App app)
{
    [Fact]
    public async Task Json_Serialization_With_Http_Post()
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