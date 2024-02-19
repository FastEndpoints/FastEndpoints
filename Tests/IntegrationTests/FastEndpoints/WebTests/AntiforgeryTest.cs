using System.Net;
using System.Net.Http.Json;
using TestClass = TestCases.AntiforgeryTest;

namespace Int.FastEndpoints.WebTests;

public class AntiforgeryTest(Fixture f, ITestOutputHelper o) : TestClass<Fixture>(f, o)
{
    [Fact]
    public async Task Html_Form_Renders_With_Af_Token()
    {
        var content = await Fx.GuestClient.GetStringAsync($"{Fx.GuestClient.BaseAddress}api/{TestClass.Routes.GetFormHtml}");
        content.Should().Contain("__RequestVerificationToken");
    }

    [Fact]
    public async Task Af_Middleware_Blocks_Request_With_Bad_Token()
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("qweryuiopasdfghjklzxcvbnm"), "__RequestVerificationToken" }
        };

        var rsp = await Fx.GuestClient.SendAsync(
                      new()
                      {
                          Content = form,
                          RequestUri = new($"{Fixture.GuestClient.BaseAddress}api/{TestClass.Routes.Validate}"),
                          Method = HttpMethod.Post
                      });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errResponse = await rsp.Content.ReadFromJsonAsync<ErrorResponse>();
        errResponse!.Errors.Count.Should().Be(1);
        errResponse.Errors["GeneralErrors"][0].Should().Be("Anti-forgery token is invalid!");
    }

    [Fact]
    public async Task Af_Token_Verification_Succeeds()
    {
        var (_, tokenRsp) = await Fx.GuestClient.GETAsync<TestClass.GetAfTokenEndpoint, TestClass.TokenResponse>();

        var form = new MultipartFormDataContent
        {
            { new StringContent(tokenRsp.Value!), "__RequestVerificationToken" }
        };

        var rsp = await Fx.GuestClient.SendAsync(
                      new()
                      {
                          Content = form,
                          RequestUri = new($"{Fx.GuestClient.BaseAddress}api/{TestClass.Routes.Validate}"),
                          Method = HttpMethod.Post
                      });

        rsp.IsSuccessStatusCode.Should().BeTrue();
        var content = await rsp.Content.ReadAsStringAsync();
        content.Should().Contain("antiforgery success");
    }
}