using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using TestCases.EmptyRequestTest;

namespace EndpointTests;

public class EndpointTests(AppFixture App) : TestBase<AppFixture>
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

        var response = await App.AdminClient.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Host.Should().Be("localhost");
    }

    [Fact]
    public async Task GlobalRoutePrefixOverride()
    {
        using var stringContent = new StringContent("this is the body content");
        stringContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

        var rsp = await App.AdminClient.PostAsync("/mobile/api/test-cases/global-prefix-override/12345", stringContent);

        var res = await rsp.Content.ReadFromJsonAsync<TestCases.PlainTextRequestTest.Response>();

        res!.BodyContent.Should().Be("this is the body content");
        res.Id.Should().Be(12345);
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
            NullableString = null,
            FromClaim = "fromClaim",
            FromHeader = "fromHeader",
            HasPermission = true
        };

        // Act
        var getResp = await App.AdminClient.GETAsync<TestCases.HydratedTestUrlGeneratorTest.Endpoint, TestCases.HydratedTestUrlGeneratorTest.Request, string>(req);

        var postResp = await App.AdminClient.POSTAsync<TestCases.HydratedTestUrlGeneratorTest.Endpoint, TestCases.HydratedTestUrlGeneratorTest.Request, string>(req);

        var putResp = await App.AdminClient.PUTAsync<TestCases.HydratedTestUrlGeneratorTest.Endpoint, TestCases.HydratedTestUrlGeneratorTest.Request, string>(req);

        var patchResp = await App.AdminClient.PATCHAsync<TestCases.HydratedTestUrlGeneratorTest.Endpoint, TestCases.HydratedTestUrlGeneratorTest.Request, string>(req);

        var deleteResp = await App.AdminClient.DELETEAsync<TestCases.HydratedTestUrlGeneratorTest.Endpoint, TestCases.HydratedTestUrlGeneratorTest.Request, string>(req);

        // Assert
        var expectedPath = "/api/test/hydrated-test-url-generator-test/123/00000000-0000-0000-0000-000000000000/string/{nullableString}/{fromClaim}/{fromHeader}/True";
        getResp.Result.Should().BeEquivalentTo(expectedPath);
        postResp.Result.Should().BeEquivalentTo(expectedPath);
        putResp.Result.Should().BeEquivalentTo(expectedPath);
        patchResp.Result.Should().BeEquivalentTo(expectedPath);
        deleteResp.Result.Should().BeEquivalentTo(expectedPath);
    }
}