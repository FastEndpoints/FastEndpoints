using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace FastEndpoints.Security;

/// <summary>
/// abstract class for implementing a jwt revocation middleware.
/// <para>
/// the token in the authorization header is checked, as well as any token accepted by a jwt bearer authentication scheme from another source such as the query
/// string or a cookie. the latter requires <see cref="JwtBearerOptions.SaveToken" /> to be enabled, which is the default when using
/// <see cref="AuthExtensions.AddAuthenticationJwtBearer" />.
/// </para>
/// </summary>
/// <param name="next">the next request delegate to execute</param>
public abstract class JwtRevocationMiddleware(RequestDelegate next)
{
    const string Bearer = "Bearer ";

    public async Task Invoke(HttpContext ctx)
    {
        //requests that have not been matched to an endpoint (such as when registered before routing) are checked as well
        if (ctx.GetEndpoint()?.Metadata.OfType<IAllowAnonymous>().Any() is true)
        {
            await next(ctx);

            return;
        }

        var authHeader = ctx.Request.Headers.Authorization;
        string? headerToken = null;

        if (!StringValues.IsNullOrEmpty(authHeader) && authHeader[0]!.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase))
        {
            headerToken = authHeader[0]![Bearer.Length..].Trim();

            if (!await JwtTokenIsValidAsync(headerToken, ctx.RequestAborted))
            {
                await SendTokenRevokedResponseAsync(ctx, ctx.RequestAborted);

                return;
            }
        }

        var schemes = ctx.RequestServices.GetService<IAuthenticationSchemeProvider>();

        if (schemes is not null)
        {
            foreach (var scheme in await schemes.GetAllSchemesAsync())
            {
                if (!typeof(JwtBearerHandler).IsAssignableFrom(scheme.HandlerType))
                    continue;

                //the handler may have taken the token from somewhere other than the authorization header.
                //authentication results are cached per request, so the token is not validated twice.
                var result = await ctx.AuthenticateAsync(scheme.Name);
                var token = result.Succeeded ? result.Properties?.GetTokenValue("access_token") : null;

                if (token is null || token == headerToken)
                    continue;

                if (!await JwtTokenIsValidAsync(token, ctx.RequestAborted))
                {
                    await SendTokenRevokedResponseAsync(ctx, ctx.RequestAborted);

                    return;
                }
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