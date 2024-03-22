using FastEndpoints;
using RichardSzalay.MockHttp;
using Web.Auth;
using Xunit;

// ReSharper disable InconsistentNaming

namespace Unit.FastEndpoints;

public class HttpClientExtensionsTests
{
    [Fact]
    public async Task GETAsyncGeneratesUrlWithHydratedRouteArguments()
    {
        // Arrange
        MockHttpMessageHandler mockHttp = new();
        mockHttp.Expect(HttpMethod.Get, "http://localhost/api/test/1/00000000-0000-0000-0000-000000000000/stringValue/{NullableString}/{fromClaim}/{fromHeader}/{hasPermission}")
                .Respond("application/json", "{}");
        var http = mockHttp.ToHttpClient();
        http.BaseAddress = new("http://localhost");

        const string route = "/api/test/{id}/{guid:guid}/{stringBindFrom}/{NullableString}/{fromClaim}/{fromHeader}/{hasPermission}";
        IEndpoint.SetTestUrl(typeof(Endpoint), route);

        // Act
        await http.GETAsync<Endpoint, Request, Response>(
            new()
            {
                Id = 1,
                Guid = Guid.Empty,
                String = "stringValue",
                NullableString = null,
                FromClaim = "fromClaim",
                FromHeader = "fromHeader",
                HasPermission = true
            });

        // Assert
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

    [FromClaim(Claim.UserType)]
    public string FromClaim { get; set; } = null!;

    [FromHeader("tenant-id")]
    public string FromHeader { get; set; } = null!;

    [HasPermission(Allow.Customers_Create)]
    public bool? HasPermission { get; set; }
}

file class Response { }