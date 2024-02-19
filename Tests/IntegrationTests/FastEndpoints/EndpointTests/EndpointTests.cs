using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using TestCases.EmptyRequestTest;

namespace EndpointTests;

public class EndpointTests(Fixture f, ITestOutputHelper o) : TestClass<Fixture>(f, o)
{
    [Fact]
    public async Task EmptyRequest()
    {
        var endpointUrl = IEndpoint.TestURLFor<EmptyRequestEndpoint>();

        var requestUri = new Uri(
            Fixture.AdminClient.BaseAddress!.ToString().TrimEnd('/') +
            (endpointUrl.StartsWith('/') ? endpointUrl : "/" + endpointUrl));

        var message = new HttpRequestMessage
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
            Method = HttpMethod.Get,
            RequestUri = requestUri
        };

        var response = await Fixture.AdminClient.SendAsync(message);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OnBeforeOnAfterValidation()
    {
        var (rsp, res) = await Fixture.AdminClient.POSTAsync<
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

        var rsp = await Fixture.AdminClient.PostAsync("/mobile/api/test-cases/global-prefix-override/12345", stringContent);

        var res = await rsp.Content.ReadFromJsonAsync<TestCases.PlainTextRequestTest.Response>();

        res!.BodyContent.Should().Be("this is the body content");
        res.Id.Should().Be(12345);
    }
}