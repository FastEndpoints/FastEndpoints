namespace Web.Auth;

public class JwtBlacklistChecker(RequestDelegate next) : JwtRevocationMiddleware(next)
{
    protected override Task<bool> JwtTokenIsValidAsync(string jwtToken, CancellationToken ct)
        => Task.FromResult(!jwtToken.Equals("revoked token"));
}