using System.Net;
using System.Net.Http.Json;
using Login = Admin.Login;

namespace Web;

public class AdminTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task AdminLoginWithBadInput()
    {
        var (resp, result) = await App.GuestClient.POSTAsync<Login.Endpoint, Login.Request, ErrorResponse>(
                                 new()
                                 {
                                     UserName = "x",
                                     Password = "y"
                                 });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Errors.Count.Should().Be(2);
    }

    [Fact]
    public async Task AdminLoginSuccess()
    {
        var (resp, result) = await App.GuestClient.POSTAsync<Login.Endpoint, Login.Request, Login.Response>(
                                 new()
                                 {
                                     UserName = "admin",
                                     Password = "pass"
                                 });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Permissions.Count().Should().Be(8);
        result.JWTToken.Should().NotBeNull();
    }

    [Fact]
    public async Task AdminLoginInvalidCreds()
    {
        var (rsp, _) = await App.GuestClient.POSTAsync<Login.Endpoint, Login.Request, Login.Response>(
                           new()
                           {
                               UserName = "admin",
                               Password = "xxxxx"
                           });
        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        rsp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        // read(deserialize) the 400 response to see what's actually wrong
        // or change the response DTO type above to ErrorResponse
        var errRsp = await rsp.Content.ReadFromJsonAsync<ErrorResponse>();
        errRsp!.Errors["GeneralErrors"][0].Should().Be("Authentication Failed!");
    }

    [Fact]
    public async Task AdminLoginThrottling()
    {
        var client = App.CreateClient();
        client.DefaultRequestHeaders.Add("X-Custom-Throttle-Header", "TEST");

        var successCount = 0;

        for (var i = 1; i <= 6; i++)
        {
            var (rsp, res) = await client.POSTAsync<Login.Endpoint_V1, Login.Request, Login.Response>(
                                 new()
                                 {
                                     UserName = "admin",
                                     Password = "pass"
                                 });

            if (i <= 5)
            {
                rsp.StatusCode.Should().Be(HttpStatusCode.OK);
                res.JWTToken.Should().NotBeNullOrEmpty();
                successCount++;
            }
            else
            {
                i.Should().Be(6);
                rsp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            }
        }

        successCount.Should().Be(5);
    }

    [Fact]
    public async Task AdminLoginV2()
    {
        var (resp, result) = await App.GuestClient.GETAsync<Login.Endpoint_V2, EmptyRequest, int>(new());
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().Be(2);
    }
}