using FastEndpoints.Security;
using Int.FastEndpoints;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using RefreshTest = TestCases.RefreshTokensTest;

namespace Web;

public class RefreshTokenTests : TestClass<Fixture>
{
    public RefreshTokenTests(Fixture f, ITestOutputHelper o) : base(f, o) { }

    [Fact]
    public async Task LoginEndpointGeneratesCorrectToken()
    {
        var (rsp, res) = await Fixture.GuestClient.GETAsync<RefreshTest.LoginEndpoint, TokenResponse>();
        rsp!.StatusCode.Should().Be(HttpStatusCode.OK);
        res!.UserId.Should().Be("usr001");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(res.AccessToken);
        token.Claims.Single(c => c.Type == "claim1").Value.Should().Be("val1");
        token.Claims.Single(c => c.Type == "role").Value.Should().Be("role1");
        token.Claims.Single(c => c.Type == "permissions").Value.Should().Be("perm1");
    }

    [Fact]
    public async Task RefreshEndpointValidationWorks()
    {
        var (rsp, res) = await Fixture.GuestClient.POSTAsync<RefreshTest.TokenService, TokenRequest, ErrorResponse>(new()
        {
            UserId = "bad-id",
            RefreshToken = "bad-token"
        });

        rsp!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        res!.Errors["UserId"][0].Should().Be("invalid user id");
        res!.Errors["RefreshToken"][0].Should().Be("invalid refresh token");
    }

    [Fact]
    public async Task RefreshEndpointReturnsCorrectTokenResponse()
    {
        var (rsp, res) = await Fixture.GuestClient.POSTAsync<RefreshTest.TokenService, TokenRequest, TokenResponse>(new()
        {
            UserId = "usr001",
            RefreshToken = "xyz"
        });

        rsp!.StatusCode.Should().Be(HttpStatusCode.OK);
        res!.UserId.Should().Be("usr001");
        Guid.TryParse(res!.RefreshToken, out _).Should().BeTrue();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(res.AccessToken);
        token.Claims.Count().Should().Be(4);
        token.Claims.Single(c => c.Type == "new-claim").Value.Should().Be("new-value");
    }
}