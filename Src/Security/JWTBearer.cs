using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FastEndpoints.Security;

/// <summary>
/// static class for easy creation of jwt bearer auth tokens
/// </summary>
public static class JWTBearer
{
    /// <summary>
    /// generate a jwt token with the supplied parameters
    /// </summary>
    /// <param name="signingKey">the secret key to use for signing the tokens</param>
    /// <param name="expireAt">the expiry date</param>
    /// <param name="permissions">one or more permissions to assign to the user principal</param>
    /// <param name="roles">one or more roles to assign the user principal</param>
    /// <param name="claims">one or more claims to assign to the user principal</param>
    public static string CreateToken(
        string signingKey,
        DateTime? expireAt = null,
        IEnumerable<string>? permissions = null,
        IEnumerable<string>? roles = null,
        params (string claimType, string claimValue)[] claims)
            => CreateToken(
                signingKey,
                expireAt,
                permissions,
                roles,
                claims.Select(c => new Claim(c.claimType, c.claimValue)));

    /// <summary>
    /// generate a jwt token with the supplied parameters
    /// </summary>
    /// <param name="signingKey">the secret key to use for signing the tokens</param>
    /// <param name="issuer">the issue</param>
    /// <param name="audience">the audience</param>
    /// <param name="expireAt">the expiry date</param>
    /// <param name="permissions">one or more permissions to assign to the user principal</param>
    /// <param name="roles">one or more roles to assign the user principal</param>
    /// <param name="claims">one or more claims to assign to the user principal</param>
    public static string CreateToken(
        string signingKey,
        string issuer,
        string audience,
        DateTime? expireAt = null,
        IEnumerable<string>? permissions = null,
        IEnumerable<string>? roles = null,
        params (string claimType, string claimValue)[] claims)
            => CreateToken(
                signingKey,
                expireAt,
                permissions,
                roles,
                claims.Select(c => new Claim(c.claimType, c.claimValue)),
                issuer,
                audience);

    /// <summary>
    /// generate a jwt token with the supplied parameters
    /// </summary>
    /// <param name="signingKey">the secret key to use for signing the tokens</param>
    /// <param name="expireAt">the expiry date</param>
    /// <param name="permissions">one or more permissions to assign to the user principal</param>
    /// <param name="roles">one or more roles to assign the user principal</param>
    /// <param name="claims">one or more claims to assign to the user principal</param>
    /// <param name="issuer">the issuer</param>
    /// <param name="audience">the audience</param>
    public static string CreateToken(
        string signingKey,
        DateTime? expireAt = null,
        IEnumerable<string>? permissions = null,
        IEnumerable<string>? roles = null,
        IEnumerable<Claim>? claims = null,
        string? issuer = null,
        string? audience = null)
    {
        var claimList = new List<Claim>();

        if (claims != null)
            claimList.AddRange(claims);

        if (permissions != null)
            claimList.AddRange(permissions.Select(p => new Claim(Constants.PermissionsClaimType, p)));

        if (roles != null)
            claimList.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = DateTime.UtcNow,
            Subject = new ClaimsIdentity(claimList),
            Expires = expireAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}