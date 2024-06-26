using System.Net;
using TestCases.RateLimitTests;

namespace Misc;

public class MiscTestCases(Sut App) : TestBase<Sut>
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
            response = await App.GuestClient.SendAsync(request);
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
            response = await App.GuestClient.SendAsync(request);
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BypassThrottlingLimit()
    {
        var client = App.CreateClient(
            new()
            {
                ThrottleBypassHeaderName = "X-Custom-Throttle-Header"
            });

        for (var i = 0; i < 5; i++)
        {
            var (rsp, _) = await client.GETAsync<GlobalErrorResponseTest, Response>();
            rsp.IsSuccessStatusCode.Should().BeTrue();
        }
    }

    [Fact]
    public async Task DontCatchExceptions()
    {
        try
        {
            await App.GuestClient.GetStringAsync("/api/test-cases/one");
        }
        catch { }

        var res = await App.GuestClient.GetStringAsync("/api/test-cases/1");

        res.Should().Be("1");
    }

    [Fact]
    public async Task STJ_Infinite_Recursion()
    {
        var (rsp, _) = await App.GuestClient.GETAsync<TestCases.STJInfiniteRecursionTest.Endpoint, TestCases.STJInfiniteRecursionTest.Response>();
        rsp.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await rsp.Content.ReadAsStringAsync()).Should().Contain("A possible object cycle was detected.");
    }
}