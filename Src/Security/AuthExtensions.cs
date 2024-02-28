using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

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
        //TODO: remove all other overloads in favor of this method at v6.0

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(
                    o =>
                    {
                        var sOpts = new JwtSigningOptions();
                        signingOptions(sOpts);

                        SecurityKey? key = null;

                        if (sOpts.SigningKey is not null)
                        {
                            switch (sOpts.SigningStyle)
                            {
                                case TokenSigningStyle.Symmetric:
                                    key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(sOpts.SigningKey));

                                    break;
                                case TokenSigningStyle.Asymmetric:
                                {
                                    var rsa = RSA.Create(); //do not dispose
                                    if (sOpts.KeyIsPemEncoded)
                                        rsa.ImportFromPem(sOpts.SigningKey);
                                    else
                                        rsa.ImportRSAPublicKey(Convert.FromBase64String(sOpts.SigningKey), out _);
                                    key = new RsaSecurityKey(rsa);

                                    break;
                                }
                                default:
                                    throw new InvalidOperationException("Jwt signing style not specified!");
                            }
                        }

                        //set defaults
                        o.TokenValidationParameters.IssuerSigningKey = key;
                        o.TokenValidationParameters.ValidateIssuerSigningKey = key is not null;
                        o.TokenValidationParameters.ValidateLifetime = true;
                        o.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(60);
                        o.TokenValidationParameters.ValidAudience = null;
                        o.TokenValidationParameters.ValidateAudience = false;
                        o.TokenValidationParameters.ValidIssuer = null;
                        o.TokenValidationParameters.ValidateIssuer = false;

                        //set sensible defaults (based on configuration) for the claim mapping so tokens created with JWTBearer.CreateToken() will not be modified
                        o.TokenValidationParameters.NameClaimType = Conf.SecOpts.NameClaimType;
                        o.TokenValidationParameters.RoleClaimType = Conf.SecOpts.RoleClaimType;
                        o.MapInboundClaims = false;

                        bearerOptions?.Invoke(o);

                        //correct any user mistake
                        o.TokenValidationParameters.ValidateAudience = o.TokenValidationParameters.ValidAudience is not null;
                        o.TokenValidationParameters.ValidateIssuer = o.TokenValidationParameters.ValidIssuer is not null;
                    });

        return services;
    }

    [Obsolete("Use AddAuthenticationJwtBearer() method.")]
    public static IServiceCollection AddJWTBearerAuth(this IServiceCollection services,
                                                      string tokenSigningKey,
                                                      TokenSigningStyle tokenSigningStyle = TokenSigningStyle.Symmetric,
                                                      Action<TokenValidationParameters>? tokenValidation = null,
                                                      Action<JwtBearerEvents>? bearerEvents = null)
    {
        return services.AddJWTBearerAuth(
            tokenSigningKey,
            tokenSigningStyle,
            o =>
            {
                tokenValidation?.Invoke(o.TokenValidationParameters);

                // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
                bearerEvents?.Invoke(o.Events ??= new());
            });
    }

    [Obsolete("Use AddAuthenticationJwtBearer() method.")]
    public static IServiceCollection AddJWTBearerAuth(this IServiceCollection services, string tokenSigningKey, Action<JwtBearerOptions> jwtOptions)
        => AddJWTBearerAuth(services, tokenSigningKey, TokenSigningStyle.Asymmetric, jwtOptions);

    [Obsolete("Use AddAuthenticationJwtBearer() method.")]
    public static IServiceCollection AddJWTBearerAuth(this IServiceCollection services,
                                                      string tokenSigningKey,
                                                      TokenSigningStyle tokenSigningStyle,
                                                      Action<JwtBearerOptions>? jwtOptions = null)
    {
        return AddAuthenticationJwtBearer(
            services,
            s =>
            {
                s.SigningKey = tokenSigningKey;
                s.SigningStyle = tokenSigningStyle;
            },
            jwtOptions);
    }

    /// <summary>
    /// configure and enable cookie based authentication
    /// </summary>
    /// <param name="validFor">specify how long the created cookie is valid for with a <see cref="TimeSpan" /></param>
    /// <param name="options">optional action for configuring cookie authentication options</param>
    public static IServiceCollection AddAuthenticationCookie(this IServiceCollection services, TimeSpan validFor, Action<CookieAuthenticationOptions>? options = null)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(
                    o =>
                    {
                        o.ExpireTimeSpan = validFor;
                        o.Cookie.MaxAge = validFor;
                        o.Cookie.HttpOnly = true;
                        o.Cookie.SameSite = SameSiteMode.Lax;
                        options?.Invoke(o);
                    });

        return services;
    }

    [Obsolete("Use AddAuthenticationCookie() method.")]
    public static IServiceCollection AddCookieAuth(this IServiceCollection services, TimeSpan validFor, Action<CookieAuthenticationOptions>? options = null)
        => AddAuthenticationCookie(services, validFor, options);

    /// <summary>
    /// returns true of the current user principal has a given permission code.
    /// </summary>
    /// <param name="permissionCode">the permission code to check for</param>
    public static bool HasPermission(this ClaimsPrincipal principal, string permissionCode)
        => principal.FindAll(Conf.SecOpts.PermissionsClaimType).Select(c => c.Value).Contains(permissionCode);

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