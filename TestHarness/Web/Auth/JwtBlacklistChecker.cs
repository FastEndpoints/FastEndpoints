using System.Collections.Concurrent;

namespace Web.Auth;

public class JwtBlacklistChecker(RequestDelegate next) : JwtRevocationMiddleware(next)
{
    public static readonly ConcurrentDictionary<string, bool> RevokedTokens = new(StringComparer.Ordinal);

    protected override Task<bool> JwtTokenIsValidAsync(string jwtToken, CancellationToken ct)
        => Task.FromResult(!jwtToken.Equals("revoked token") && !RevokedTokens.ContainsKey(jwtToken));
}