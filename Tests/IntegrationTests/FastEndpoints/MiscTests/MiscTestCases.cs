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
            response = await App.GuestClient.SendAsync(request, Cancellation);
        }

        var responseContent = await response!.Content.ReadAsStringAsync(Cancellation);
        responseContent.ShouldBe("Custom Error Response");
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
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
            response = await App.GuestClient.SendAsync(request, Cancellation);
        }

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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
            rsp.IsSuccessStatusCode.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task DontCatchExceptions()
    {
        try
        {
            await App.GuestClient.GetStringAsync("/api/test-cases/one", Cancellation);
        }
        catch { }

        var res = await App.GuestClient.GetStringAsync("/api/test-cases/1", Cancellation);

        res.ShouldBe("1");
    }

    [Fact]
    public async Task STJ_Infinite_Recursion()
    {
        var (rsp, _) = await App.GuestClient.GETAsync<TestCases.STJInfiniteRecursionTest.Endpoint, TestCases.STJInfiniteRecursionTest.Response>();
        rsp.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await rsp.Content.ReadAsStringAsync(Cancellation)).ShouldContain("A possible object cycle was detected.");
    }
}