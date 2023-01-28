namespace TestCases.RefreshTokensTest;

public class TokenService : RefreshTokenService<TokenRequest, TokenResponse>
{
    public TokenService()
    {
        Setup(o =>
        {
            o.TokenSigningKey = "token_signing_key";
            o.Endpoint("/tokens/refresh-token", ep => { });
        });
    }

    public override Task PersistTokenAsync(TokenResponse response)
    {
        return Task.CompletedTask;
    }

    public override Task RefreshRequestValidationAsync(TokenRequest req)
    {
        if (req.UserId != "usr001")
            AddError(r => r.UserId, "invalid user id");

        if (req.RefreshToken != "xyz")
            AddError(r => r.RefreshToken, "invalid refresh token");

        return Task.CompletedTask;
    }

    public override async Task SetRenewalPrivilegesAsync(TokenRequest request, UserPrivileges privileges)
    {
        await Task.Delay(100); //issue #365
        privileges.Claims.Add(new("new-claim", "new-value"));
    }
}