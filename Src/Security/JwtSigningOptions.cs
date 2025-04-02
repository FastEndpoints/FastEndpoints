using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FastEndpoints.Security;

/// <summary>
/// jwt signing options for consuming jwts.
/// </summary>
public sealed class JwtSigningOptions
{
    /// <summary>
    /// the key used to sign jwts symmetrically or the base64 encoded public-key when jwts are signed asymmetrically.
    /// the key can be optional when used to verify tokens issued by an idp where public key retrieval happens dynamically.
    /// </summary>
    /// <remarks>the key can be in PEM format. make sure to set <see cref="KeyIsPemEncoded" /> to <c>true</c> if the key is PEM encoded.</remarks>
    public string? SigningKey { get; set; }

    /// <summary>
    /// specifies how tokens were signed. symmetrically or asymmetrically.
    /// </summary>
    public TokenSigningStyle SigningStyle { get; set; } = TokenSigningStyle.Symmetric;

    /// <summary>
    /// specifies whether the key is pem encoded.
    /// </summary>
    public bool KeyIsPemEncoded { get; set; }

    static SecurityKey? _securityKey;

    /// <summary>
    /// call this method to update the jwt signing key during runtime. all future token verifications will use the supplied key.
    /// </summary>
    /// <param name="key">the new jwt signing key to use for generating a <see cref="SecurityKey" /></param>
    public void UpdateSigningKey(string? key)
    {
        if (key is null)
            return; // SigningKey can be null if user sets their own IssuerSigningKeyResolver

        switch (SigningStyle)
        {
            case TokenSigningStyle.Symmetric:
                _securityKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key));

                break;
            case TokenSigningStyle.Asymmetric:
            {
                var rsa = RSA.Create(); //do not dispose
                if (KeyIsPemEncoded)
                    rsa.ImportFromPem(key);
                else
                    rsa.ImportRSAPublicKey(Convert.FromBase64String(key), out _);
                _securityKey = new RsaSecurityKey(rsa);

                break;
            }
            default:
                throw new InvalidOperationException("Jwt signing style not specified!");
        }
    }

    internal static IEnumerable<SecurityKey> KeyResolver(string _, SecurityToken __, string ___, TokenValidationParameters ____)
    {
        if (_securityKey is null)
        {
            throw new InvalidOperationException(
                $"{nameof(JwtSigningOptions)}.{nameof(SigningKey)} has not been set! " +
                $"Make sure to set the key with {nameof(AuthExtensions.AddAuthenticationJwtBearer)}(...) method.");
        }

        return [_securityKey];
    }
}