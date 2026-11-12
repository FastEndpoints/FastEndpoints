using System.Net;

namespace OpenApi;

public class FixtureDocumentCacheTests
{
    [Fact]
    public async Task concurrent_inspections_reuse_json_but_parse_independent_documents()
    {
        using var handler = new DocumentHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("http://localhost") };
        var fixture = new Fixture { DocClient = client };

        var jsons = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => fixture.GetCachedDocumentJsonAsync("first")));

        handler.Requests.ShouldBe(1);
        jsons.ShouldAllBe(json => json == jsons[0]);
        var document = JsonNode.Parse(jsons[0])!;
        document["request"] = -1;
        JsonNode.Parse(await fixture.GetCachedDocumentJsonAsync("first"))!["request"]!.GetValue<int>().ShouldBe(1);

        JsonNode.Parse(await fixture.GetCachedDocumentJsonAsync("second"))!["request"]!.GetValue<int>().ShouldBe(2);
        handler.Requests.ShouldBe(2);
    }

    [Fact]
    public async Task fresh_fetches_and_other_fixtures_do_not_reuse_cached_json()
    {
        using var handler = new DocumentHandler();
        using var client = new HttpClient(handler) { BaseAddress = new("http://localhost") };
        var fixture = new Fixture { DocClient = client };

        var cached = await fixture.GetCachedDocumentJsonAsync("first");
        (await fixture.GetDocumentJsonAsync("first")).ShouldNotBe(cached);
        (await fixture.GetDocumentJsonUnlockedAsync("first")).ShouldNotBe(cached);
        (await fixture.GetCachedDocumentJsonAsync("first")).ShouldBe(cached);
        (await new Fixture { DocClient = client }.GetCachedDocumentJsonAsync("first")).ShouldNotBe(cached);
        handler.Requests.ShouldBe(4);
    }

    [Fact]
    public async Task failed_fetch_is_not_cached()
    {
        using var handler = new DocumentHandler { FailFirstRequest = true };
        using var client = new HttpClient(handler) { BaseAddress = new("http://localhost") };
        var fixture = new Fixture { DocClient = client };

        await Should.ThrowAsync<HttpRequestException>(() => fixture.GetCachedDocumentJsonAsync("first"));
        var json = await fixture.GetCachedDocumentJsonAsync("first");
        (await fixture.GetCachedDocumentJsonAsync("first")).ShouldBe(json);
        handler.Requests.ShouldBe(2);
    }

    sealed class DocumentHandler : HttpMessageHandler
    {
        int _requests;
        public int Requests => _requests;
        public bool FailFirstRequest { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var number = Interlocked.Increment(ref _requests);
            await Task.Yield();

            return new(FailFirstRequest && number == 1 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"request":{{number}}}""")
            };
        }
    }
}
