namespace NativeAotCheckerTests;

public class EventStreamTests(App app) : TestBase<App>
{
    [Fact]
    public async Task Event_Stream_Serializes_Custom_Data_Type()
    {
        using var rsp = await app.Client.GetAsync("event-stream", Cancellation);
        var body = await rsp.Content.ReadAsStringAsync(Cancellation);

        rsp.IsSuccessStatusCode.ShouldBeTrue(body);
        rsp.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        body.Split("event: price-update").Length.ShouldBe(4);
        body.ShouldContain("""data: {"symbol":"SYM1","last_price":10.5}""");
        body.ShouldContain("""data: {"symbol":"SYM3","last_price":31.5}""");
    }
}
