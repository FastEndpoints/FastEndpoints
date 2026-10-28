using System.Net;
using System.Net.Http.Headers;

namespace Int.FastEndpoints;

public class StreamStatusCodeTests(Sut App) : TestBase<Sut>
{
    [Theory]
    [InlineData("bytes")]
    [InlineData("stream")]
    [InlineData("file")]
    public async Task Custom_Status_Code_Is_Sent_With_The_Body(string mode)
    {
        var res = await App.GuestClient.GetAsync($"api/test-cases/stream-status/{mode}", Cancellation);

        res.StatusCode.ShouldBe(HttpStatusCode.MultiStatus);
        res.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        (await res.Content.ReadAsStringAsync(Cancellation)).ShouldBe("partial-success");
    }

    [Fact]
    public async Task Existing_Stream_Send_Stays_200()
    {
        var res = await App.GuestClient.GetAsync("api/test-cases/stream-status/default", Cancellation);

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync(Cancellation)).ShouldBe("partial-success");
    }

    [Fact]
    public async Task Range_Processing_Replaces_The_Custom_Status_Code()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/test-cases/stream-status/range");
        req.Headers.Range = new RangeHeaderValue(0, 6);

        var res = await App.GuestClient.SendAsync(req, Cancellation);

        res.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        (await res.Content.ReadAsStringAsync(Cancellation)).ShouldBe("partial");
    }
}
