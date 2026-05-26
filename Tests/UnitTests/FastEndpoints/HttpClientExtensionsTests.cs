using FastEndpoints;
using Microsoft.AspNetCore.Http;
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

        testUrl.ShouldBe($"{DateTimeParamRoute}?{nameof(DateTimeParamRequest.QueryParam)}={System.Net.WebUtility.UrlEncode($"{DateTimeParamRequest.DateTime:o}")}");
    }

    [Fact]
    public void GetTestUrlForUrlEncodesQueryParamValues()
    {
        MockHttpMessageHandler mockHttp = new();
        var http = mockHttp.ToHttpClient();
        http.BaseAddress = new("http://localhost");

        IEndpoint.SetTestUrl(typeof(StringQueryParamEndpoint), NullParamRoute);

        var req = new StringQueryParamRequest
        {
            QueryParam = "2026-05-21 08:56:33.684+0100"
        };

        var testUrl = http.GetTestUrlFor<StringQueryParamEndpoint>(req);

        testUrl.ShouldBe($"{NullParamRoute}?{nameof(StringQueryParamRequest.QueryParam)}=2026-05-21+08%3A56%3A33.684%2B0100");
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

    [Fact]
    public async Task SendAsFormDataHonorsBindingSourceMetadata()
    {
        var handler = new MultipartCaptureHandler();
        var http = new HttpClient(handler) { BaseAddress = new("http://localhost") };

        await http.SENDAsync<MultipartBindingSourceRequest, EmptyResponse>(
            HttpMethod.Post,
            "api/test",
            new()
            {
                Normal = "normal",
                ExplicitFormField = "form-field",
                Route = "route",
                Query = "query",
                RequiredHeader = "header",
                RequiredCookie = "cookie",
                RequiredClaim = "claim",
                RequiredPermission = true,
                Promoted = new()
                {
                    Normal = "promoted-normal",
                    ExplicitFormField = "promoted-form-field",
                    Route = "promoted-route",
                    Query = "promoted-query",
                    RequiredHeader = "promoted-header",
                    RequiredCookie = "promoted-cookie",
                    RequiredClaim = "promoted-claim",
                    RequiredPermission = true
                }
            },
            sendAsFormData: true);

        handler.FieldNames.OrderBy(n => n).ShouldBe(
        [
            nameof(MultipartBindingSourceRequest.ExplicitFormField),
            nameof(MultipartPromotedBindingSourceRequest.ExplicitFormField),
            nameof(MultipartBindingSourceRequest.Normal),
            nameof(MultipartPromotedBindingSourceRequest.Normal)
        ]);
    }

    [Fact]
    public async Task SendAsFormDataOnlyPromotesFromFormOnRootRequestDto()
    {
        var handler = new MultipartCaptureHandler();
        var http = new HttpClient(handler) { BaseAddress = new("http://localhost") };

        await http.SENDAsync<MultipartRootPromotionRequest, EmptyResponse>(
            HttpMethod.Post,
            "api/test",
            new()
            {
                Form = new()
                {
                    Name = "root",
                    Child = new()
                    {
                        Name = "child",
                        File = CreateFile("child-file.png"),
                        Files = new FormFileCollection { CreateFile("child-files.png") }
                    },
                    Items =
                    [
                        new() { Name = "item" }
                    ]
                }
            },
            sendAsFormData: true);

        handler.FieldNames.OrderBy(n => n).ShouldBe(
        [
            "Child.File",
            "Child.Files",
            "Child.Name",
            "Items[0].Name",
            "Name"
        ]);

        static FormFile CreateFile(string fileName)
        {
            var stream = new MemoryStream([1]);

            return new(stream, 0, stream.Length, fileName, fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };
        }
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

file class StringQueryParamEndpoint : Endpoint<StringQueryParamRequest>;

file class DateTimeParamRequest
{
    public static DateTime DateTime { get; } = DateTime.UtcNow;

    [QueryParam]
    public DateTime? QueryParam { get; set; }
}

file class StringQueryParamRequest
{
    [QueryParam]
    public string? QueryParam { get; set; }
}

file class HttpFallbackEndpoint : Endpoint<EmptyRequest>;

file class MultipartBindingSourceRequest
{
    public string Normal { get; set; } = null!;

    [FormField]
    public string ExplicitFormField { get; set; } = null!;

    [RouteParam]
    public string Route { get; set; } = null!;

    [QueryParam]
    public string Query { get; set; } = null!;

    [FromHeader]
    public string RequiredHeader { get; set; } = null!;

    [FromCookie]
    public string RequiredCookie { get; set; } = null!;

    [FromClaim]
    public string RequiredClaim { get; set; } = null!;

    [HasPermission(Allow.Customers_Create)]
    public bool? RequiredPermission { get; set; }

    [FromForm]
    public MultipartPromotedBindingSourceRequest Promoted { get; set; } = null!;
}

file class MultipartPromotedBindingSourceRequest
{
    public string Normal { get; set; } = null!;

    [FormField]
    public string ExplicitFormField { get; set; } = null!;

    [RouteParam]
    public string Route { get; set; } = null!;

    [QueryParam]
    public string Query { get; set; } = null!;

    [FromHeader]
    public string RequiredHeader { get; set; } = null!;

    [FromCookie]
    public string RequiredCookie { get; set; } = null!;

    [FromClaim]
    public string RequiredClaim { get; set; } = null!;

    [HasPermission(Allow.Customers_Create)]
    public bool? RequiredPermission { get; set; }
}

file class MultipartRootPromotionRequest
{
    [FromForm]
    public MultipartRootPromotionForm Form { get; set; } = null!;
}

file class MultipartRootPromotionForm
{
    public string Name { get; set; } = null!;

    [FromForm]
    public MultipartNestedFromFormChild Child { get; set; } = null!;

    public List<MultipartRootPromotionItem> Items { get; set; } = [];
}

file class MultipartNestedFromFormChild
{
    public string Name { get; set; } = null!;

    public IFormFile File { get; set; } = null!;

    public IFormFileCollection Files { get; set; } = null!;
}

file class MultipartRootPromotionItem
{
    public string Name { get; set; } = null!;
}

file sealed class MultipartCaptureHandler : HttpMessageHandler
{
    public string[] FieldNames { get; private set; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Content.ShouldBeOfType<MultipartFormDataContent>();

        FieldNames = ((MultipartFormDataContent)request.Content!)
                     .Select(c => c.Headers.ContentDisposition?.Name?.Trim('"'))
                     .Where(n => n is not null)
                     .ToArray()!;

        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
}
