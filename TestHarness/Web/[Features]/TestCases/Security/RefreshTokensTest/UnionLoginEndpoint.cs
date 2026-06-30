using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using static Microsoft.AspNetCore.Http.TypedResults;

namespace TestCases.RefreshTokensTest;

sealed class UnionLoginRequest
{
    public string Username { get; set; }
}

//issue #597 - login endpoint with a union-type response that still needs the refresh token service
[HttpPost("tokens/union-login"), AllowAnonymous]
sealed class UnionLoginEndpoint : Endpoint<UnionLoginRequest, Results<Ok<TokenResponse>, UnauthorizedHttpResult>>
{
    public override async Task<Results<Ok<TokenResponse>, UnauthorizedHttpResult>> ExecuteAsync(UnionLoginRequest req, CancellationToken ct)
    {
        if (req.Username != "usr001")
            return Unauthorized();

        var token = await CreateTokenWith<TokenService, TokenResponse>(
                        "usr001",
                        p =>
                        {
                            p.Roles.Add("role1");
                            p.Permissions.Add("perm1");
                            p.Claims.Add(new("claim1", "val1"));
                        },
                        req); //forwarded to the initial token-creation hooks; login path never casts it to TokenRequest

        return Ok(token);
    }
}
