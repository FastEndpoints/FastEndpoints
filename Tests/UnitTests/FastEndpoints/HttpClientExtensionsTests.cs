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
        mockHttp.Expect(
                    HttpMethod.Get,
                    "http://localhost/api/test/1/00000000-0000-0000-0000-000000000000/stringValue/{fromClaim}/{fromHeader}/{hasPermission}")
                .WithHeaders("Cookie", "cookie=fromCookie")
                .Respond("application/json", "{}");
        var http = mockHttp.ToHttpClient();
        http.BaseAddress = new("http://localhost");

        IEndpoint.SetTestUrl(
            typeof(HydratedRouteArgsEndpoint),
            "/api/test/{id}/{guid:guid}/{stringBindFrom}/{fromClaim}/{fromHeader}/{hasPermission}/{NullableString?}");

        // Act
        await http.GETAsync<HydratedRouteArgsEndpoint, Request>(
            new()
            {
                Id = 1,
                Guid = Guid.Empty,
                String = "stringValue",
                NullableString = null,
                FromClaim = "fromClaim",
                FromHeader = "fromHeader",
                FromCookie = "fromCookie",
                HasPermission = true
            });

        // Assert
        mockHttp.VerifyNoOutstandingExpectation();
    }

    const string NullParamRoute = "null/param/test";

    [Fact]
    public void GetTestUrlForGeneratesUrlWithoutNullQueryParam()
    {
        // Arrange
        MockHttpMessageHandler mockHttp = new();
        var http = mockHttp.ToHttpClient();
        http.BaseAddress = new("http://localhost");

        IEndpoint.SetTestUrl(typeof(NullQueryParamEndpoint), NullParamRoute);

        var req = new NullParamRequest
        {
            QueryParam = null
        };

        // Act
        var testUrl = http.GetTestUrlFor<NullQueryParamEndpoint>(req);

        // Assert
        testUrl.ShouldBe(NullParamRoute);
    }

    [Fact]
    public void GetTestUrlForGeneratesUrlWithQueryParam()
    {
        // Arrange
        MockHttpMessageHandler mockHttp = new();
        var http = mockHttp.ToHttpClient();
        http.BaseAddress = new("http://localhost");

        IEndpoint.SetTestUrl(typeof(NullQueryParamEndpoint), NullParamRoute);

        var req = new NullParamRequest
        {
            QueryParam = NullParamRequest.Guid
        };

        // Act
        var testUrl = http.GetTestUrlFor<NullQueryParamEndpoint>(req);

        // Assert
        testUrl.ShouldBe($"{NullParamRoute}?{nameof(NullParamRequest.QueryParam)}={NullParamRequest.Guid}");
    }

    const string DateTimeParamRoute = "datetime/param/test";

    [Fact]
    public void GetTestUrlForGeneratesUrlWithDateTimeQueryParamInCorrectFormat()
    {
        MockHttpMessageHandler mockHttp = new();
        var http = mockHttp.ToHttpClient();
        http.BaseAddress = new("http://localhost");

        IEndpoint.SetTestUrl(typeof(DateTimeQueryParamEndpoint), DateTimeParamRoute);

        var req = new DateTimeParamRequest
        {
            QueryParam = DateTimeParamRequest.DateTime
        };

        var testUrl = http.GetTestUrlFor<DateTimeQueryParamEndpoint>(req);

        testUrl.ShouldBe($"{DateTimeParamRoute}?{nameof(DateTimeParamRequest.QueryParam)}={DateTimeParamRequest.DateTime:o}");
    }

    [Fact]
    public void GetTestUrlForLoadsTestUrlCacheViaHttpWhenEndpointTestUrlIsUnavailable()
    {
        MockHttpMessageHandler mockHttp = new();
        mockHttp.Expect(HttpMethod.Get, "http://localhost/_test_url_cache_")
                .Respond("application/json", $"[\"{typeof(HttpFallbackEndpoint).FullName}|http-fallback/test-route\"]");

        var http = mockHttp.ToHttpClient();
        http.BaseAddress = new("http://localhost");

        var testUrl = http.GetTestUrlFor<HttpFallbackEndpoint>(EmptyRequest.Instance);

        testUrl.ShouldBe("http-fallback/test-route");
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public void GetTestUrlForThrowsWhenRequestDtoTypeDoesNotMatchEndpoint()
    {
        MockHttpMessageHandler mockHttp = new();
        var http = mockHttp.ToHttpClient();
        http.BaseAddress = new("http://localhost");

        Should.Throw<ArgumentException>(() => http.GetTestUrlFor<DateTimeQueryParamEndpoint>(new NullParamRequest()))
              .Message.ShouldBe("The request object is not the correct DTO type for the endpoint!");
    }
}

file class HydratedRouteArgsEndpoint : Endpoint<Request>;

file class Request
{
    public int Id { get; set; }
    public Guid Guid { get; set; }

    [BindFrom("stringBindFrom")]
    public string String { get; set; } = null!;

    public string? NullableString { get; set; }

    public string? FromQuery { get; set; }

    [FromClaim(Claim.UserType)]
    public string FromClaim { get; set; } = null!;

    [FromHeader("tenant-id")]
    public string FromHeader { get; set; } = null!;

    [FromCookie("cookie")]
    public string FromCookie { get; set; } = null!;

    [HasPermission(Allow.Customers_Create)]
    public bool? HasPermission { get; set; }
}

file class NullQueryParamEndpoint : Endpoint<NullParamRequest>;

file class NullParamRequest
{
    public static Guid Guid { get; } = Guid.NewGuid();

    [QueryParam]
    public Guid? QueryParam { get; set; }
}

file class DateTimeQueryParamEndpoint : Endpoint<DateTimeParamRequest>;

file class DateTimeParamRequest
{
    public static DateTime DateTime { get; } = DateTime.UtcNow;

    [QueryParam]
    public DateTime? QueryParam { get; set; }
}

file class HttpFallbackEndpoint : Endpoint<EmptyRequest>;
