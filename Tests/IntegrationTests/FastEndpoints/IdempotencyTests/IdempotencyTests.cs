using System.Net;
using TestCases.Idempotency;

namespace IdempotencyTests;

public class IdempotencyTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task Header_Not_Present()
    {
        var url = $"{Endpoint.BaseRoute}/123";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        var res = await App.Client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Multiple_Headers()
    {
        var url = $"{Endpoint.BaseRoute}/123";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Idempotency-Key", ["1", "2"]);
        var res = await App.Client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}