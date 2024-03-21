using FastEndpoints;
using RichardSzalay.MockHttp;
using Xunit;

// ReSharper disable InconsistentNaming

namespace Unit.FastEndpoints;

public class HttpClientExtensionsTests
{
    [Fact]
    public async Task GETAsyncGeneratesUrlWithRouteArguments()
    {
        // Arrange
        MockHttpMessageHandler mockHttp = new();
        mockHttp.Expect(HttpMethod.Get, "http://localhost/api/test/1/00000000-0000-0000-0000-000000000000/stringValue/{NullableString}")
                .Respond("application/json", "{}");
        var http = mockHttp.ToHttpClient();
        http.BaseAddress = new("http://localhost");

        const string route = "/api/test/{id}/{guid:guid}/{stringBindFrom}/{NullableString}";
        IEndpoint.SetTestUrl(typeof(Endpoint), route);

        await http.GETAsync<Endpoint, Request, Response>(
            new()
            {
                Id = 1,
                Guid = Guid.Empty,
                String = "stringValue",
                NullableString = null
            });

        mockHttp.VerifyNoOutstandingExpectation();
    }
}

file class Endpoint : Endpoint<Request, Response>;

file class Request
{
    public int Id { get; set; }
    public Guid Guid { get; set; }

    [BindFrom("stringBindFrom")]
    public string String { get; set; } = null!;

    public string? NullableString { get; set; }
}

file class Response { }