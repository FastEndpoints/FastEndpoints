using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FastEndpoints.Security;

/// <summary>
/// abstract class for implementing a jwt revocation middleware
/// </summary>
/// <param name="next">the next request delegate to execute</param>
public abstract class JwtRevocationMiddleware(RequestDelegate next)
{
    const string Bearer = "Bearer ";

    public async Task Invoke(HttpContext ctx)
    {
        if (ctx.GetEndpoint()?.Metadata.OfType<IAllowAnonymous>().Any() is null or true)
        {
            await next(ctx);

            return;
        }

        var authHeader = ctx.Request.Headers.Authorization;

        if (!StringValues.IsNullOrEmpty(authHeader) && authHeader[0]!.StartsWith(Bearer) is true)
        {
            var token = authHeader[0]![Bearer.Length..].Trim();

            if (!await JwtTokenIsValidAsync(token, ctx.RequestAborted))
            {
                await SendTokenRevokedResponseAsync(ctx, ctx.RequestAborted);

                return;
            }
        }

        await next(ctx);
    }

    /// <summary>
    /// implement this method and return whether the supplied jwt token is still valid or not.
    /// </summary>
    /// <param name="jwtToken">the jwt token that should be checked against a blacklist.</param>
    /// <param name="ct">cancellation token</param>
    /// <returns>true if the token is valid</returns>
    protected abstract Task<bool> JwtTokenIsValidAsync(string jwtToken, CancellationToken ct);

    /// <summary>
    /// override this method in order to customize the unauthorized response that is sent when the jwt token is no longer valid.
    /// </summary>
    /// <param name="ctx">the http context</param>
    /// <param name="ct">cancellation token</param>
    protected virtual Task SendTokenRevokedResponseAsync(HttpContext ctx, CancellationToken ct)
        => ctx.Response.SendStringAsync("Bearer token has been revoked!", 401, cancellation: ct);
}