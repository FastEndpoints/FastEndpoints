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
        using var imageContent = new ByteArrayContent([]);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

        using var form = new MultipartFormDataContent();
        form.Add(imageContent, "File", "test.png");

        var res = await App.GuestClient.PutAsync("/api/uploads/image/save", form, Cancellation);

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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

        result.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        result.Errors.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldContainKey("null-claim");
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

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldBe("you sent xyz");
    }

    //refresh tokens
    [Fact]
    public async Task LoginEndpointGeneratesCorrectToken()
    {
        var (rsp, res) = await App.GuestClient.GETAsync<RefreshTest.LoginEndpoint, TokenResponse>();
        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.UserId.ShouldBe("usr001");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(res.AccessToken);
        token.Claims.Single(c => c.Type == "claim1").Value.ShouldBe("val1");
        token.Claims.Single(c => c.Type == "role").Value.ShouldBe("role1");
        token.Claims.Single(c => c.Type == "permissions").Value.ShouldBe("perm1");
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

        rsp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        res.Errors["userId"][0].ShouldBe("invalid user id");
        res.Errors["refreshToken"][0].ShouldBe("invalid refresh token");
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

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.UserId.ShouldBe("usr001");
        Guid.TryParse(res.RefreshToken, out _).ShouldBeTrue();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(res.AccessToken);
        token.Claims.Count().ShouldBe(4);
        token.Claims.Single(c => c.Type == "new-claim").Value.ShouldBe("new-value");
    }

    [Fact]
    public async Task Jwt_Revocation()
    {
        var client = App.CreateClient(c => c.DefaultRequestHeaders.Authorization = new("Bearer", "revoked token"));
        var (rsp, _) = await client.GETAsync<Customers.List.Recent.Endpoint, Customers.List.Recent.Response>();
        rsp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var res = await rsp.Content.ReadAsStringAsync(Cancellation);
        res.ShouldBe("Bearer token has been revoked!");
    }

    [Fact]
    public async Task IAuthorization_Injection_Pass()
    {
        var (rsp, res) = await App.AdminClient.GETAsync<TestCases.IAuthorizationServiceInjectionTest.Endpoint, bool>();

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.ShouldBeTrue();
    }

    [Fact]
    public async Task IAuthorization_Injection_Fail()
    {
        var (rsp, res) = await App.CustomerClient.GETAsync<TestCases.IAuthorizationServiceInjectionTest.Endpoint, bool>();

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.ShouldBeFalse();
    }
}