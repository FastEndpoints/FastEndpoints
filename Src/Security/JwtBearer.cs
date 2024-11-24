using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
#if NET8_0_OR_GREATER
using Microsoft.IdentityModel.JsonWebTokens;

#else
using System.IdentityModel.Tokens.Jwt;
#endif

namespace FastEndpoints.Security;

/// <summary>
/// static class for easy creation of jwt bearer tokens
/// </summary>
public static class JwtBearer
{
    /// <summary>
    /// generates jwt tokens with supplied settings.
    /// </summary>
    /// <param name="options">action to configure jwt creation options.</param>
    /// <exception cref="InvalidOperationException">thrown if a token signing key is not supplied.</exception>
    public static string CreateToken(Action<JwtCreationOptions> options)
        => CreateToken(null, options);

    internal static string CreateToken(JwtCreationOptions? options = null, Action<JwtCreationOptions>? optsAction = null)
    {
        //TODO: remove all other overloads in favor of this at v6.0

        var opts = options ?? new JwtCreationOptions(Cfg.ServiceResolver.TryResolve<IOptions<JwtCreationOptions>>()?.Value);
        optsAction?.Invoke(opts);

        if (string.IsNullOrEmpty(opts.SigningKey))
            throw new InvalidOperationException($"'{nameof(JwtCreationOptions.SigningKey)}' is required!");

        if (opts.SigningStyle is TokenSigningStyle.Asymmetric && opts.SigningAlgorithm is SecurityAlgorithms.HmacSha256Signature)
        {
            throw new InvalidOperationException(
                $"Please set an appropriate '{nameof(JwtCreationOptions.SigningAlgorithm)}' when creating Asymmetric JWTs!");
        }

        var claimList = new List<Claim>();

        if (opts.User.Claims.Count > 0)
            claimList.AddRange(opts.User.Claims);

        if (opts.User.Permissions.Count > 0)
            claimList.AddRange(opts.User.Permissions.Select(p => new Claim(Cfg.SecOpts.PermissionsClaimType, p)));

        if (opts.User.Roles.Count > 0)
            claimList.AddRange(opts.User.Roles.Select(r => new Claim(Cfg.SecOpts.RoleClaimType, r)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = opts.Issuer,
            Audience = opts.Audience,
            IssuedAt = (Cfg.ServiceResolver.TryResolve<TimeProvider>() ?? TimeProvider.System).GetUtcNow().UtcDateTime,
            Subject = new(claimList),
            Expires = opts.ExpireAt,
            SigningCredentials = GetSigningCredentials(opts)
        };

    #if NET8_0_OR_GREATER
        var handler = new JsonWebTokenHandler();

        return handler.CreateToken(descriptor);
    #else
        var handler = new JwtSecurityTokenHandler();

        return handler.WriteToken(handler.CreateToken(descriptor));
    #endif

        static SigningCredentials GetSigningCredentials(JwtCreationOptions opts)
        {
            if (opts.SigningStyle == TokenSigningStyle.Symmetric)
                return new(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(opts.SigningKey)), opts.SigningAlgorithm);

            var rsa = RSA.Create(); // don't dispose this
            if (opts.KeyIsPemEncoded)
                rsa.ImportFromPem(opts.SigningKey);
            else
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(opts.SigningKey), out _);

            return new(
                new RsaSecurityKey(rsa)
                {
                    KeyId = opts.AsymmetricKidGenerator?.Invoke(rsa)
                },
                opts.SigningAlgorithm);
        }
    }
}

/// <summary>
/// static class for easy creation of jwt bearer tokens
/// </summary>
public static class JWTBearer
{
    [Obsolete("Use JwtBearer.CreateToken() method.")]
    public static string CreateToken(string signingKey,
                                     DateTime? expireAt = null,
                                     IEnumerable<string>? permissions = null,
                                     IEnumerable<string>? roles = null,
                                     params (string claimType, string claimValue)[] claims)
        => CreateToken(signingKey, expireAt, permissions, roles, claims.Select(c => new Claim(c.claimType, c.claimValue)));

    [Obsolete("Use JwtBearer.CreateToken() method.")]
    public static string CreateToken(string signingKey,
                                     string? issuer,
                                     string? audience,
                                     DateTime? expireAt = null,
                                     IEnumerable<string>? permissions = null,
                                     IEnumerable<string>? roles = null,
                                     params (string claimType, string claimValue)[] claims)
        => CreateToken(signingKey, expireAt, permissions, roles, claims.Select(c => new Claim(c.claimType, c.claimValue)), issuer, audience);

    [Obsolete("Use JwtBearer.CreateToken() method.")]
    public static string CreateToken(string signingKey,
                                     TokenSigningStyle signingStyle,
                                     string? issuer = null,
                                     string? audience = null,
                                     DateTime? expireAt = null,
                                     IEnumerable<string>? permissions = null,
                                     IEnumerable<string>? roles = null,
                                     params (string claimType, string claimValue)[] claims)
        => CreateToken(signingKey, expireAt, permissions, roles, claims.Select(c => new Claim(c.claimType, c.claimValue)), issuer, audience, signingStyle);

    [Obsolete("Use JwtBearer.CreateToken() method.")]
    public static string CreateToken(string signingKey,
                                     Action<UserPrivileges> privileges,
                                     string? issuer = null,
                                     string? audience = null,
                                     DateTime? expireAt = null,
                                     TokenSigningStyle signingStyle = TokenSigningStyle.Symmetric)
    {
        var privs = new UserPrivileges();
        privileges(privs);

        return CreateToken(signingKey, expireAt, privs.Permissions, privs.Roles, privs.Claims, issuer, audience, signingStyle);
    }

    [Obsolete("Use JwtBearer.CreateToken() method.")]
    public static string CreateToken(string signingKey,
                                     DateTime? expireAt = null,
                                     IEnumerable<string>? permissions = null,
                                     IEnumerable<string>? roles = null,
                                     IEnumerable<Claim>? claims = null,
                                     string? issuer = null,
                                     string? audience = null,
                                     TokenSigningStyle signingStyle = TokenSigningStyle.Symmetric)
    {
        return JwtBearer.CreateToken(
            o =>
            {
                o.SigningKey = signingKey;
                o.SigningStyle = signingStyle;
                o.ExpireAt = expireAt;
                if (permissions?.Any() is true)
                    o.User.Permissions.AddRange(permissions);
                if (roles?.Any() is true)
                    o.User.Roles.AddRange(roles);
                if (claims?.Any() is true)
                    o.User.Claims.AddRange(claims);
                o.Issuer = issuer;
                o.Audience = audience;
            });
    }
}