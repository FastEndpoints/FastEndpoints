using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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
    public static string CreateToken(string signingKey,
                                     DateTime? expireAt = null,
                                     IEnumerable<string>? permissions = null,
                                     IEnumerable<string>? roles = null,
                                     params (string claimType, string claimValue)[] claims)
    => CreateToken(signingKey,
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
    public static string CreateToken(string signingKey,
                                     string? issuer,
                                     string? audience,
                                     DateTime? expireAt = null,
                                     IEnumerable<string>? permissions = null,
                                     IEnumerable<string>? roles = null,
                                     params (string claimType, string claimValue)[] claims)
     => CreateToken(signingKey,
                    expireAt,
                    permissions,
                    roles,
                    claims.Select(c => new Claim(c.claimType, c.claimValue)),
                    issuer,
                    audience);

    /// <summary>
    /// generate a jwt token with the supplied parameters and token signing style
    /// </summary>
    /// <param name="signingKey">the secret key to use for signing the tokens</param>
    /// <param name="signingStyle">the signing style to use (Symmertic or Asymmetric)</param>
    /// <param name="issuer">the issue</param>
    /// <param name="audience">the audience</param>
    /// <param name="expireAt">the expiry date</param>
    /// <param name="permissions">one or more permissions to assign to the user principal</param>
    /// <param name="roles">one or more roles to assign the user principal</param>
    /// <param name="claims">one or more claims to assign to the user principal</param>
    public static string CreateToken(string signingKey,
                                     TokenSigningStyle signingStyle,
                                     string? issuer = null,
                                     string? audience = null,
                                     DateTime? expireAt = null,
                                     IEnumerable<string>? permissions = null,
                                     IEnumerable<string>? roles = null,
                                     params (string claimType, string claimValue)[] claims)
     => CreateToken(signingKey,
                    expireAt,
                    permissions,
                    roles,
                    claims.Select(c => new Claim(c.claimType, c.claimValue)),
                    issuer,
                    audience,
                    signingStyle);

    /// <summary>
    /// generate a jwt token with the supplied parameters
    /// </summary>
    /// <param name="signingKey">the secret key to use for signing the tokens</param>
    /// <param name="priviledges">an action to specify the privileges of the user</param>
    /// <param name="issuer">the issuer</param>
    /// <param name="audience">the audience</param>
    /// <param name="expireAt">the expiry date</param>
    /// <param name="signingStyle">the signing style to use (Symmertic or Asymmetric)</param>
    public static string CreateToken(string signingKey,
                                     Action<UserPrivileges> priviledges,
                                     string? issuer = null,
                                     string? audience = null,
                                     DateTime? expireAt = null,
                                     TokenSigningStyle signingStyle = TokenSigningStyle.Symmetric)
    {
        var privs = new UserPrivileges();
        priviledges(privs);

        return CreateToken(
            signingKey,
            expireAt,
            privs.Permissions,
            privs.Roles,
            privs.Claims,
            issuer,
            audience,
            signingStyle);
    }

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
    /// <param name="signingStyle">the signing style to use (Symmertic or Asymmetric)</param>
    public static string CreateToken(string signingKey,
                                     DateTime? expireAt = null,
                                     IEnumerable<string>? permissions = null,
                                     IEnumerable<string>? roles = null,
                                     IEnumerable<Claim>? claims = null,
                                     string? issuer = null,
                                     string? audience = null,
                                     TokenSigningStyle signingStyle = TokenSigningStyle.Symmetric)
    {
        var claimList = new List<Claim>();

        if (claims != null)
            claimList.AddRange(claims);

        if (permissions != null)
            claimList.AddRange(permissions.Select(p => new Claim(Config.SecOpts.PermissionsClaimType, p)));

        if (roles != null)
            claimList.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = DateTime.UtcNow,
            Subject = new ClaimsIdentity(claimList),
            Expires = expireAt,
            SigningCredentials = GetSigningCredentials(signingKey, signingStyle)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static SigningCredentials GetSigningCredentials(string key, TokenSigningStyle style)
    {
        if (style == TokenSigningStyle.Asymmetric)
        {
            var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(Convert.FromBase64String(key), out _);
            return new SigningCredentials(
                new RsaSecurityKey(rsa),
                SecurityAlgorithms.RsaSha256);
        }

        return new SigningCredentials(
            new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key)),
            SecurityAlgorithms.HmacSha256Signature);
    }

    /// <summary>
    /// token signing style enum
    /// </summary>
    public enum TokenSigningStyle
    {
        Symmetric,
        Asymmetric
    }
}
