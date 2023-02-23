namespace TestCases.RefreshTokensTest;

public class LoginEndpoint : EndpointWithoutRequest<TokenResponse>
{
    public override void Configure()
    {
        Get("tokens/login");
        AllowAnonymous();
    }

    public async override Task HandleAsync(CancellationToken ct)
    {
        Response = await CreateTokenWith<TokenService>("usr001", p =>
        {
            p.Roles.Add("role1");
            p.Permissions.Add("perm1");
            p.Claims.Add(new("claim1", "val1"));
        });
    }
}