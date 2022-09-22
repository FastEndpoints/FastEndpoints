using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    /// <param name="issuer">validates issuer if set</param>
    /// <param name="audience">validates audience if set</param>
    /// <param name="tokenSigningStyle">specify the toke signing style</param>
    public static IServiceCollection AddAuthenticationJWTBearer(this IServiceCollection services,
                                                                string tokenSigningKey,
                                                                string? issuer = null,
                                                                string? audience = null,
                                                                TokenSigningStyle tokenSigningStyle = TokenSigningStyle.Symmetric)
    {
        services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(o =>
        {
            SecurityKey key;
            if (tokenSigningStyle == TokenSigningStyle.Symmetric)
            {
                key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(tokenSigningKey));
            }
            else
            {
                using var rsa = RSA.Create();
                rsa.ImportRSAPublicKey(Convert.FromBase64String(tokenSigningKey), out _);
                key = new RsaSecurityKey(rsa);
            }
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateAudience = audience is not null,
                ValidAudience = audience,
                ValidateIssuer = issuer is not null,
                ValidIssuer = issuer,
                IssuerSigningKey = key,
            };
        });

        return services;
    }

    /// <summary>
    /// returns true of the current user principal has a given permission code.
    /// </summary>
    /// <param name="permissionCode">the permission code to check for</param>
    public static bool HasPermission(this ClaimsPrincipal principal, string permissionCode)
        => principal.FindAll(Constants.PermissionsClaimType).Select(c => c.Value).Contains(permissionCode);

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
}
