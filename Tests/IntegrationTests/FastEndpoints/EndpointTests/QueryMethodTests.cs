using System.Net;
using System.Net.Http.Json;
using TestCases.QueryMethodTest;

namespace EndpointTests;

public class QueryMethodTests(Sut App) : TestBase<Sut>
{
    static readonly HttpMethod _query = new("QUERY");

    [Fact]
    public async Task Fluent_Setup_Binds_Body_Route_Query_Header_And_HttpMethod()
    {
        var msg = new HttpRequestMessage
        {
            Method = _query,
            RequestUri = new($"{App.AdminClient.BaseAddress}api/test-cases/query-method/fluent/42?filter=from-query"),
            Content = JsonContent.Create(new QueryRequest { Name = "from-body" })
        };
        msg.Headers.Add("x-query-token", "from-header");

        var rsp = await App.AdminClient.SendAsync(msg, Cancellation);
        var res = await rsp.Content.ReadFromJsonAsync<QueryResponse>(Cancellation);

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res!.Id.ShouldBe(42);
        res.Name.ShouldBe("from-body");
        res.Filter.ShouldBe("from-query");
        res.Token.ShouldBe("from-header");
        res.Method.ShouldBe(nameof(Http.QUERY));
    }

    [Fact]
    public async Task Attribute_Setup_Works()
    {
        var msg = new HttpRequestMessage
        {
            Method = _query,
            RequestUri = new($"{App.AdminClient.BaseAddress}api/test-cases/query-method/attribute/24?filter=attr-query"),
            Content = JsonContent.Create(new QueryRequest { Name = "attr-body" })
        };
        msg.Headers.Add("x-query-token", "attr-header");

        var rsp = await App.AdminClient.SendAsync(msg, Cancellation);
        var res = await rsp.Content.ReadFromJsonAsync<QueryResponse>(Cancellation);

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res!.Id.ShouldBe(24);
        res.Name.ShouldBe("attr-body");
        res.Filter.ShouldBe("attr-query");
        res.Method.ShouldBe(nameof(Http.QUERY));
    }

    [Fact]
    public async Task Missing_Content_Type_Is_Rejected_For_Body_Dto()
    {
        var content = new ByteArrayContent("{}"u8.ToArray());
        content.Headers.ContentType = null;

        var rsp = await App.AdminClient.SendAsync(
                      new()
                      {
                          Method = _query,
                          RequestUri = new($"{App.AdminClient.BaseAddress}api/test-cases/query-method/fluent/42"),
                          Content = content
                      },
                      Cancellation);

        rsp.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Antiforgery_Is_Skipped_For_Query()
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("bad-token"), "__RequestVerificationToken" }
        };

        var rsp = await App.AdminClient.SendAsync(
                      new()
                      {
                          Method = _query,
                          RequestUri = new($"{App.AdminClient.BaseAddress}api/test-cases/query-method/antiforgery"),
                          Content = form
                      },
                      Cancellation);

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await rsp.Content.ReadAsStringAsync(Cancellation);
        content.ShouldContain("query antiforgery skipped");
    }
}