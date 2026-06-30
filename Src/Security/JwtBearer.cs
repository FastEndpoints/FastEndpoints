using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;

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
        var opts = options ?? new JwtCreationOptions(ServiceResolver.Instance.TryResolve<IOptions<JwtCreationOptions>>()?.Value);
        optsAction?.Invoke(opts);

        if (string.IsNullOrEmpty(opts.SigningKey))
            throw new InvalidOperationException($"'{nameof(JwtCreationOptions)}.{nameof(JwtCreationOptions.SigningKey)}' is required!");

        if (opts.SigningStyle is TokenSigningStyle.Asymmetric && opts.SigningAlgorithm is SecurityAlgorithms.HmacSha256)
        {
            throw new InvalidOperationException(
                $"Please set an appropriate '{nameof(JwtCreationOptions)}.{nameof(JwtCreationOptions.SigningAlgorithm)}' when creating Asymmetric JWTs!");
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
            IssuedAt = (ServiceResolver.Instance.TryResolve<TimeProvider>() ?? TimeProvider.System).GetUtcNow().UtcDateTime,
            Subject = new(claimList),
            Expires = opts.ExpireAt,
            SigningCredentials = GetSigningCredentials(opts)
        };

        var handler = new JsonWebTokenHandler();

        return handler.CreateToken(descriptor);

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