using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TestCases.EmptyRequestTest;
using TestCases.HydratedQueryParamGeneratorTest;
using TestCases.Routing;

namespace EndpointTests;

public class EndpointTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task EmptyRequest()
    {
        var endpointUrl = IEndpoint.TestURLFor<EmptyRequestEndpoint>();

        var requestUri = new Uri(
            App.AdminClient.BaseAddress!.ToString().TrimEnd('/') +
            (endpointUrl.StartsWith('/') ? endpointUrl : "/" + endpointUrl));

        var message = new HttpRequestMessage
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
            Method = HttpMethod.Get,
            RequestUri = requestUri
        };

        var response = await App.AdminClient.SendAsync(message, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OnBeforeOnAfterValidation()
    {
        var (rsp, res) = await App.AdminClient.POSTAsync<
                             TestCases.OnBeforeAfterValidationTest.Endpoint,
                             TestCases.OnBeforeAfterValidationTest.Request,
                             TestCases.OnBeforeAfterValidationTest.Response>(
                             new()
                             {
                                 Host = "blah",
                                 Verb = Http.DELETE
                             });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.Host.ShouldBe("localhost");
    }

    [Fact]
    public async Task GlobalRoutePrefixOverride()
    {
        using var stringContent = new StringContent("this is the body content");
        stringContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

        var rsp = await App.AdminClient.PostAsync("/mobile/api/test-cases/global-prefix-override/12345", stringContent, Cancellation);

        var res = await rsp.Content.ReadFromJsonAsync<TestCases.PlainTextRequestTest.Response>(Cancellation);

        res!.BodyContent.ShouldBe("this is the body content");
        res.Id.ShouldBe(12345);
    }

    [Fact]
    public async Task HydratedTestUrlGeneratorWorksForSupportedVerbs()
    {
        // Arrange
        TestCases.HydratedTestUrlGeneratorTest.Request req = new()
        {
            Id = 123,
            Guid = Guid.Empty,
            String = "string",
            NullableString = "null",
            FromClaim = "fromClaim",
            FromHeader = "fromHeader",
            HasPermission = true
        };

        // Act
        var getResp = await App.AdminClient
                               .GETAsync<TestCases.HydratedTestUrlGeneratorTest.Endpoint, TestCases.HydratedTestUrlGeneratorTest.Request, string>(req);

        var postResp = await App.AdminClient
                                .POSTAsync<TestCases.HydratedTestUrlGeneratorTest.Endpoint, TestCases.HydratedTestUrlGeneratorTest.Request, string>(req);

        var putResp = await App.AdminClient
                               .PUTAsync<TestCases.HydratedTestUrlGeneratorTest.Endpoint, TestCases.HydratedTestUrlGeneratorTest.Request, string>(req);

        var patchResp = await App.AdminClient
                                 .PATCHAsync<TestCases.HydratedTestUrlGeneratorTest.Endpoint, TestCases.HydratedTestUrlGeneratorTest.Request, string>(req);

        var deleteResp = await App.AdminClient
                                  .DELETEAsync<TestCases.HydratedTestUrlGeneratorTest.Endpoint, TestCases.HydratedTestUrlGeneratorTest.Request, string>(req);

        // Assert
        var expectedPath = "/api/test/hydrated-test-url-generator-test/123/00000000-0000-0000-0000-000000000000/string/null/{fromClaim}/{fromHeader}/true";
        getResp.Result.ShouldBeEquivalentTo(expectedPath);
        postResp.Result.ShouldBeEquivalentTo(expectedPath);
        putResp.Result.ShouldBeEquivalentTo(expectedPath);
        patchResp.Result.ShouldBeEquivalentTo(expectedPath);
        deleteResp.Result.ShouldBeEquivalentTo(expectedPath);
    }

    [Fact]
    public async Task HydratedQueryParamGenerationWorks()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var req = new Request
        {
            Nested = new("First", 20),
            Some = "{<some thing>}",
            Guids = [guid1, guid2],
            ComplexId = new() { Number1 = 111, Number2 = 222 },
            ComplexIdString = new() { Number1 = 333, Number2 = 444 }
        };
        var (rsp, res) = await App.GuestClient
                                  .GETAsync<
                                      Endpoint,
                                      Request,
                                      Response
                                  >(req);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.Nested.ShouldBe(JsonSerializer.Serialize(req.Nested), StringCompareShould.IgnoreCase);
        res.Guids.ShouldBe(JsonSerializer.Serialize(req.Guids), StringCompareShould.IgnoreCase);
        res.ComplexId.ShouldBe(JsonSerializer.Serialize(req.ComplexId), StringCompareShould.IgnoreCase);
        res.ComplexIdString.ShouldBe(req.ComplexIdString.ToString(), StringCompareShould.IgnoreCase);
        res.Some.ShouldBe(req.Some);
    }

    [Fact]
    public async Task NonOptionalRouteParamThrowsExceptionIfParamIsNull()
    {
        var request = new NonOptionalRouteParamTest.Request(null!);

        var act = async () => await App.Client.POSTAsync<NonOptionalRouteParamTest, NonOptionalRouteParamTest.Request>(request);

        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldBe("Route param value missing for required param [{UserId}].");
    }

    [Fact]
    public async Task OptionalRouteParamWithNullValueReturnsDefaultValue()
    {
        var request = new OptionalRouteParamTest.Request(null);

        var (rsp, res) = await App.Client.POSTAsync<OptionalRouteParamTest, OptionalRouteParamTest.Request, string>(request);

        rsp.IsSuccessStatusCode.ShouldBeTrue();

        res.ShouldBe("default offer");
    }

    [Fact]
    public async Task OptionalRouteParamWithValueReturnsSentValue()
    {
        var request = new OptionalRouteParamTest.Request("blah blah!");

        var (rsp, res) = await App.Client.POSTAsync<OptionalRouteParamTest, OptionalRouteParamTest.Request, string>(request);

        rsp.IsSuccessStatusCode.ShouldBeTrue();

        res.ShouldBe("blah blah!");
    }

    [Fact]
    public async Task MetadataRegistrationTest()
    {
        var (rsp, res) = await App.GuestClient
            .GETAsync<TestCases.MetadataRegistrationTest.Endpoint, int>();

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.ShouldBe(1);
    }
}