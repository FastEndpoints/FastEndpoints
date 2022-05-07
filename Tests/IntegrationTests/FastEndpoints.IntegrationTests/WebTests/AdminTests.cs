using IntegrationTests.Shared.Fixtures;
using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace FastEndpoints.IntegrationTests.WebTests;

public class AdminTests : EndToEndTestBase
{
    public AdminTests(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) :
        base(endToEndTestFixture, outputHelper)
    {
        endToEndTestFixture.RegisterTestServices(services => { });
    }

    [Fact]
    public async Task AdminLoginWithBadInput()
    {
        var (resp, result) = await base.GuestClient.POSTAsync<
            Admin.Login.Endpoint,
            Admin.Login.Request,
            ErrorResponse>(new()
            {
                UserName = "x",
                Password = "y"
            });

        resp?.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result?.Errors.Count.Should().Be(2);
    }

    [Fact]
    public async Task AdminLoginSuccess()
    {
        var (resp, result) = await GuestClient.POSTAsync<
            Admin.Login.Endpoint,
            Admin.Login.Request,
            Admin.Login.Response>(new()
            {
                UserName = "admin",
                Password = "pass"
            });

        resp?.StatusCode.Should().Be(HttpStatusCode.OK);
        result?.Permissions?.Count().Should().Be(7);
        result?.JWTToken.Should().NotBeNull();
    }

    [Fact]
    public async Task AdminLoginInvalidCreds()
    {
        var (res, _) = await GuestClient.POSTAsync<
            Admin.Login.Endpoint,
            Admin.Login.Request,
            Admin.Login.Response>(new()
            {
                UserName = "admin",
                Password = "xxxxx"
            });

        res?.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminLoginThrottling()
    {
        var guest = EndToEndTestFixture.CreateNewClient();
        guest.DefaultRequestHeaders.Add("X-Custom-Throttle-Header", "TEST");

        int successCount = 0;

        for (int i = 1; i <= 6; i++)
        {
            try
            {
                var (rsp, _) = await guest.POSTAsync<
                    Admin.Login.Endpoint_V1,
                    Admin.Login.Request,
                    Admin.Login.Response>(new()
                    {
                        UserName = "admin",
                        Password = "pass"
                    });

                rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
                successCount++;
            }
            catch (Exception x)
            {
                i.Should().Be(6);
                x.Should().BeOfType<InvalidOperationException>();
            }
        }

        successCount.Should().Be(5);
    }

    [Fact]
    public async Task AdminLoginV2()
    {
        var (resp, result) = await GuestClient.GETAsync<
            Admin.Login.Endpoint_V2,
            EmptyRequest,
            int>(new());

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().Be(2);
    }
}