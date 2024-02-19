using System.Net;

namespace Misc;

public class MiscTestCases(Fixture f, ITestOutputHelper o) : TestClass<Fixture>(f, o)
{
    [Fact]
    public async Task ThrottledGlobalResponse()
    {
        HttpResponseMessage? response = null;

        for (var i = 0; i < 5; i++)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get
            };
            request.Headers.Add("X-Custom-Throttle-Header", "test");
            request.RequestUri =
                new(
                    "api/test-cases/global-throttle-error-response?customerId=09809&otherId=12",
                    UriKind.Relative);
            response = await Fixture.GuestClient.SendAsync(request);
        }

        var responseContent = await response!.Content.ReadAsStringAsync();
        responseContent.Should().Be("Custom Error Response");
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task NotThrottledGlobalResponse()
    {
        HttpResponseMessage response = default!;

        for (var i = 0; i < 3; i++)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get
            };
            request.Headers.Add("X-Custom-Throttle-Header", "test-2");
            request.RequestUri =
                new(
                    "api/test-cases/global-throttle-error-response?customerId=09809&otherId=12",
                    UriKind.Relative);
            response = await Fixture.GuestClient.SendAsync(request);
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DontCatchExceptions()
    {
        try
        {
            await Fixture.GuestClient.GetStringAsync("/api/test-cases/one");
        }
        catch { }

        var res = await Fixture.GuestClient.GetStringAsync("/api/test-cases/1");

        res.Should().Be("1");
    }

    [Fact]
    public async Task STJ_Infinite_Recursion()
    {
        var (rsp, _) = await Fx.GuestClient.GETAsync<TestCases.STJInfiniteRecursionTest.Endpoint, TestCases.STJInfiniteRecursionTest.Response>();
        rsp.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await rsp.Content.ReadAsStringAsync()).Should().Contain("A possible object cycle was detected.");
    }
}