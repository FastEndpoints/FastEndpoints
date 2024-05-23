using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    [Fact]
    public async Task MultiPart_Form_Request()
    {
        var idmpKey = Guid.NewGuid().ToString();
        var url = $"{Endpoint.BaseRoute}/321";

        using var fileContent = new ByteArrayContent(
            await new StreamContent(File.OpenRead("test.png"))
                .ReadAsByteArrayAsync());

        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

        using var form = new MultipartFormDataContent();
        form.Add(fileContent, "File", "test.png");
        form.Add(new StringContent("500"), "Width");

        var req1 = new HttpRequestMessage(HttpMethod.Get, url);
        req1.Content = form;
        req1.Headers.Add("Idempotency-Key", idmpKey);

        //initial request - uncached response
        var res1 = await App.Client.SendAsync(req1);
        res1.IsSuccessStatusCode.Should().BeTrue();
        res1.Headers.Any(h => h.Key == "Idempotency-Key" && h.Value.First() == idmpKey).Should().BeTrue();

        var rsp1 = await res1.Content.ReadFromJsonAsync<Response>();
        rsp1.Should().NotBeNull();
        rsp1!.Id.Should().Be("321");

        var ticks = rsp1.Ticks;
        ticks.Should().BeGreaterThan(0);

        //duplicate request - cached response
        var req2 = new HttpRequestMessage(HttpMethod.Get, url);
        req2.Content = form;
        req2.Headers.Add("Idempotency-Key", idmpKey);

        var res2 = await App.Client.SendAsync(req2);
        res2.IsSuccessStatusCode.Should().BeTrue();
        var rsp2 = await res2.Content.ReadFromJsonAsync<Response>();
        rsp2.Should().NotBeNull();
        rsp2!.Id.Should().Be("321");
        rsp2.Ticks.Should().Be(ticks);

        //changed request - uncached response
        var req3 = new HttpRequestMessage(HttpMethod.Get, url);
        form.Add(new StringContent("500"), "Height"); // the change
        req3.Content = form;
        req3.Headers.Add("Idempotency-Key", idmpKey);

        var res3 = await App.Client.SendAsync(req3);
        res3.IsSuccessStatusCode.Should().BeTrue();

        var rsp3 = await res3.Content.ReadFromJsonAsync<Response>();
        rsp3.Should().NotBeNull();
        rsp3!.Id.Should().Be("321");
        rsp3.Ticks.Should().NotBe(ticks);
    }

    [Fact]
    public async Task Json_Body_Request()
    {
        var idmpKey = Guid.NewGuid().ToString();
        var client = App.CreateClient(c => c.DefaultRequestHeaders.Add("Idempotency-Key", idmpKey));
        var req = new Request { Content = "hello" };

        //initial request - uncached response
        var (res1, rsp1) = await client.GETAsync<Endpoint, Request, Response>(req);
        res1.IsSuccessStatusCode.Should().BeTrue();

        var ticks = rsp1.Ticks;
        ticks.Should().BeGreaterThan(0);

        //duplicate request - cached response
        var (res2, rsp2) = await client.GETAsync<Endpoint, Request, Response>(req);
        res2.IsSuccessStatusCode.Should().BeTrue();

        rsp2.Ticks.Should().Be(ticks);

        //changed request - uncached response
        req.Content = "bye"; //the change
        var (res3, rsp3) = await client.GETAsync<Endpoint, Request, Response>(req);
        res3.IsSuccessStatusCode.Should().BeTrue();

        rsp3.Ticks.Should().NotBe(ticks);
    }
}