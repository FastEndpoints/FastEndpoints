using FastEndpoints.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using RefreshTest = TestCases.RefreshTokensTest;
using TestCases.MissingClaimTest;

namespace Security;

public class SecurityTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task MultiVerbEndpointAnonymousUserPutFail()
    {
        using var imageContent = new ByteArrayContent(Array.Empty<byte>());
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

        using var form = new MultipartFormDataContent();
        form.Add(imageContent, "File", "test.png");

        var res = await App.GuestClient.PutAsync("/api/uploads/image/save", form);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ClaimMissing()
    {
        var (_, result) = await App.AdminClient.POSTAsync<
                              ThrowIfMissingEndpoint,
                              ThrowIfMissingRequest,
                              ErrorResponse>(
                              new()
                              {
                                  TestProp = "xyz"
                              });

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.Errors.Should().NotBeNull();
        result.Errors.Count.Should().Be(1);
        result.Errors.Should().ContainKey("null-claim");
    }

    [Fact]
    public async Task ClaimMissingButDontThrow()
    {
        var (res, result) = await App.AdminClient.POSTAsync<
                                DontThrowIfMissingEndpoint,
                                DontThrowIfMissingRequest,
                                string>(
                                new()
                                {
                                    TestProp = "xyz"
                                });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().Be("you sent xyz");
    }

    //refresh tokens
    [Fact]
    public async Task LoginEndpointGeneratesCorrectToken()
    {
        var (rsp, res) = await App.GuestClient.GETAsync<RefreshTest.LoginEndpoint, TokenResponse>();
        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.UserId.Should().Be("usr001");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(res.AccessToken);
        token.Claims.Single(c => c.Type == "claim1").Value.Should().Be("val1");
        token.Claims.Single(c => c.Type == "role").Value.Should().Be("role1");
        token.Claims.Single(c => c.Type == "permissions").Value.Should().Be("perm1");
    }

    [Fact]
    public async Task RefreshEndpointValidationWorks()
    {
        var (rsp, res) = await App.GuestClient.POSTAsync<RefreshTest.TokenService, TokenRequest, ErrorResponse>(
                             new()
                             {
                                 UserId = "bad-id",
                                 RefreshToken = "bad-token"
                             });

        rsp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res.Errors["UserId"][0].Should().Be("invalid user id");
        res.Errors["RefreshToken"][0].Should().Be("invalid refresh token");
    }

    [Fact]
    public async Task RefreshEndpointReturnsCorrectTokenResponse()
    {
        var (rsp, res) = await App.GuestClient.POSTAsync<RefreshTest.TokenService, TokenRequest, TokenResponse>(
                             new()
                             {
                                 UserId = "usr001",
                                 RefreshToken = "xyz"
                             });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.UserId.Should().Be("usr001");
        Guid.TryParse(res.RefreshToken, out _).Should().BeTrue();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(res.AccessToken);
        token.Claims.Count().Should().Be(4);
        token.Claims.Single(c => c.Type == "new-claim").Value.Should().Be("new-value");
    }

    [Fact]
    public async Task Jwt_Revocation()
    {
        var client = App.CreateClient(c => c.DefaultRequestHeaders.Authorization = new("Bearer", "revoked token"));
        var (rsp, _) = await client.GETAsync<Customers.List.Recent.Endpoint, Customers.List.Recent.Response>();
        rsp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var res = await rsp.Content.ReadAsStringAsync();
        res.Should().Be("Bearer token has been revoked!");
    }

    [Fact]
    public async Task IAuthorization_Injection_Pass()
    {
        var (rsp, res) = await App.AdminClient.GETAsync<TestCases.IAuthorizationServiceInjectionTest.Endpoint, bool>();

        rsp.IsSuccessStatusCode.Should().BeTrue();
        res.Should().BeTrue();
    }

    [Fact]
    public async Task IAuthorization_Injection_Fail()
    {
        var (rsp, res) = await App.CustomerClient.GETAsync<TestCases.IAuthorizationServiceInjectionTest.Endpoint, bool>();

        rsp.IsSuccessStatusCode.Should().BeTrue();
        res.Should().BeFalse();
    }
}