using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FastEndpoints.Security;

/// <summary>
/// a set of auth related extensions
/// </summary>
public static class AuthExtensions
{
    /// <summary>
    /// configure and enable jwt bearer authentication
    /// </summary>
    /// <param name="signingOptions">an action to configure <see cref="JwtSigningOptions" /></param>
    /// <param name="bearerOptions">an action to configure <see cref="JwtBearerOptions" /></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static IServiceCollection AddAuthenticationJwtBearer(this IServiceCollection services,
                                                                Action<JwtSigningOptions> signingOptions,
                                                                Action<JwtBearerOptions>? bearerOptions = null)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(
                    o =>
                    {
                        var sOpts = ServiceResolver.Instance.TryResolve<IOptions<JwtSigningOptions>>()?.Value ?? new JwtSigningOptions();
                        signingOptions(sOpts);
                        sOpts.UpdateSigningKey(sOpts.SigningKey);

                        //set defaults
                        o.TokenValidationParameters.IssuerSigningKeyResolver = JwtSigningOptions.KeyResolver;
                        o.TokenValidationParameters.ValidateIssuerSigningKey = true;
                        o.TokenValidationParameters.ValidateLifetime = true;
                        o.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(60);
                        o.TokenValidationParameters.ValidAudience = null;
                        o.TokenValidationParameters.ValidateAudience = false;
                        o.TokenValidationParameters.ValidIssuer = null;
                        o.TokenValidationParameters.ValidateIssuer = false;

                        //set sensible defaults (based on configuration) for the claim mapping so tokens created with JWTBearer.CreateToken() will not be modified
                        o.TokenValidationParameters.NameClaimType = Cfg.SecOpts.NameClaimType;
                        o.TokenValidationParameters.RoleClaimType = Cfg.SecOpts.RoleClaimType;
                        o.MapInboundClaims = false;

                        bearerOptions?.Invoke(o);

                        //correct any user mistake
                        o.TokenValidationParameters.ValidateAudience = o.TokenValidationParameters.ValidAudience is not null;
                        o.TokenValidationParameters.ValidateIssuer = o.TokenValidationParameters.ValidIssuer is not null;
                    });

        return services;
    }

    /// <summary>
    /// configure and enable cookie based authentication
    /// </summary>
    /// <param name="validFor">specify how long the created cookie is valid for with a <see cref="TimeSpan" /></param>
    /// <param name="options">optional action for configuring cookie authentication options</param>
    public static IServiceCollection AddAuthenticationCookie(this IServiceCollection services,
                                                             TimeSpan validFor,
                                                             Action<CookieAuthenticationOptions>? options = null)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(
                    o =>
                    {
                        //don't set Cookie.Expiration and Cookie.MaxAge here.
                        //allow CookieAuthenticationHandler to take care of setting it depending on IsPersistent.
                        //if we set it here, 'IsPersistent = false' won't have any effect.
                        o.Cookie.Expiration = o.Cookie.MaxAge = null;
                        o.ExpireTimeSpan = validFor;
                        o.Cookie.HttpOnly = true;
                        o.Cookie.SameSite = SameSiteMode.Lax;

                        // ReSharper disable once ArrangeObjectCreationWhenTypeNotEvident
                        o.Events = new CookieAuthenticationEvents
                        {
                            OnRedirectToLogin =
                                ctx =>
                                {
                                    ctx.Response.Headers.Location = ctx.RedirectUri;
                                    ctx.Response.StatusCode = 401;

                                    return Task.CompletedTask;
                                },
                            OnRedirectToAccessDenied =
                                ctx =>
                                {
                                    ctx.Response.Headers.Location = ctx.RedirectUri;
                                    ctx.Response.StatusCode = 403;

                                    return Task.CompletedTask;
                                },
                            OnSigningIn =
                                ctx =>
                                {
                                    if (ctx.Properties.IsPersistent)
                                        ctx.CookieOptions.MaxAge = ctx.Properties.ExpiresUtc?.UtcDateTime - DateTime.UtcNow ?? ctx.Options.ExpireTimeSpan;

                                    return Task.CompletedTask;
                                }
                        };
                        options?.Invoke(o);
                    });

        return services;
    }

    /// <summary>
    /// returns true of the current user principal has a given permission code.
    /// </summary>
    /// <param name="permissionCode">the permission code to check for</param>
    public static bool HasPermission(this ClaimsPrincipal principal, string permissionCode)
        => principal.FindAll(Cfg.SecOpts.PermissionsClaimType).Select(c => c.Value).Contains(permissionCode);

    /// <summary>
    /// determines if the current user principal has the given claim type
    /// </summary>
    /// <param name="claimType">the claim type to check for</param>
    public static bool HasClaimType(this ClaimsPrincipal principal, string claimType)
        => principal.HasClaim(c => c.Type == claimType);

    /// <summary>
    /// get the claim value for a given claim type of the current user principal. if the user doesn't have the requested claim type, a null will be returned.
    /// </summary>
    /// <param name="claimType">the claim type to look for</param>
    public static string? ClaimValue(this ClaimsPrincipal principal, string claimType)
        => principal.FindFirstValue(claimType);

    /// <summary>
    /// adds multiple <see cref="Claim" />s to the list.
    /// </summary>
    /// <param name="claims">the <see cref="Claim" />s to append to the list.</param>
    public static void Add(this List<Claim> list, params Claim[] claims)
        => list.AddRange(claims);

    /// <summary>
    /// adds multiple <see cref="Claim" />s to the list.
    /// </summary>
    /// <param name="claims">the claim <c>Type</c> &amp; <c>Value</c> tuples to add to the list.</param>
    public static void Add(this List<Claim> list, params (string claimType, string claimValue)[] claims)
        => list.AddRange(claims.Select(c => new Claim(c.claimType, c.claimValue)));

    /// <summary>
    /// adds multiple strings to a list.
    /// </summary>
    /// <param name="values">the strings to append to the list.</param>
    public static void Add(this List<string> list, params string[] values)
        => list.AddRange(values);
}