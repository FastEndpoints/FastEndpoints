using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using static FastEndpoints.Security.JWTBearer;

namespace FastEndpoints.Security;

/// <summary>
/// a set of auth related extensions
/// </summary>
public static class AuthExtensions
{
    /// <summary>
    /// configure and enable jwt bearer authentication
    /// </summary>
    /// <param name="tokenSigningKey">the secret key to use for verifying the jwt tokens</param>
    /// <param name="tokenSigningStyle">specify the token signing style</param>
    /// <param name="tokenValidation">configuration action to specify additional token validation parameters</param>
    /// <param name="bearerEvents">configuration action to specify custom jwt bearer events</param>
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

    /// <summary>
    /// configure and enable jwt bearer authentication
    /// </summary>
    /// <param name="tokenSigningKey">the secret key to use for verifying the jwt tokens</param>
    /// <param name="jwtOptions">configuration action to specify options for the authentication scheme</param>
    public static IServiceCollection AddJWTBearerAuth(this IServiceCollection services,
                                                      string tokenSigningKey,
                                                      Action<JwtBearerOptions> jwtOptions)
        => AddJWTBearerAuth(services, tokenSigningKey, TokenSigningStyle.Asymmetric, jwtOptions);

    /// <summary>
    /// configure and enable jwt bearer authentication
    /// </summary>
    /// <param name="tokenSigningKey">the secret key to use for verifying the jwt tokens</param>
    /// <param name="tokenSigningStyle">specify the token signing style</param>
    /// <param name="jwtOptions">configuration action to specify options for the authentication scheme</param>
    public static IServiceCollection AddJWTBearerAuth(this IServiceCollection services,
                                                      string tokenSigningKey,
                                                      TokenSigningStyle tokenSigningStyle,
                                                      Action<JwtBearerOptions>? jwtOptions = null)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(
                    o =>
                    {
                        SecurityKey key;

                        if (tokenSigningStyle == TokenSigningStyle.Symmetric)
                            key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(tokenSigningKey));
                        else
                        {
                            var rsa = RSA.Create();
                            rsa.ImportRSAPublicKey(Convert.FromBase64String(tokenSigningKey), out _);
                            key = new RsaSecurityKey(rsa);
                        }

                        //set defaults
                        o.TokenValidationParameters.IssuerSigningKey = key;
                        o.TokenValidationParameters.ValidateIssuerSigningKey = true;
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

                        jwtOptions?.Invoke(o);

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
    public static IServiceCollection AddCookieAuth(this IServiceCollection services,
                                                   TimeSpan validFor,
                                                   Action<CookieAuthenticationOptions>? options = null)
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
    {
        list.AddRange(claims);
    }

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
    {
        list.AddRange(values);
    }
}