using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace FastEndpoints.Security;

/// <summary>
/// options for creating jwt tokens
/// </summary>
public sealed class JwtCreationOptions
{
    public JwtCreationOptions() { } //for DI to be able to instantiate this class

    internal JwtCreationOptions(JwtCreationOptions? globalInstance)
    {
        if (globalInstance is null)
            return;

        SigningKey = globalInstance.SigningKey;
        SigningStyle = globalInstance.SigningStyle;
        SigningAlgorithm = globalInstance.SigningAlgorithm;
        KeyIsPemEncoded = globalInstance.KeyIsPemEncoded;
        AsymmetricKidGenerator = globalInstance.AsymmetricKidGenerator;
        Audience = globalInstance.Audience;
        Issuer = globalInstance.Issuer;
        CompressionAlgorithm = globalInstance.CompressionAlgorithm;

        //NOTE:
        // we're skipping ExpireAt and User properties because they need to set by the user everytime a token is created.
        // without this kind of cloning, the global instance would be used everywhere causing issues.
    }

    /// <summary>
    /// the key used to sign jwts symmetrically or the base64 encoded private-key when jwts are signed asymmetrically.
    /// </summary>
    /// <remarks>the key can be in PEM format. make sure to set <see cref="KeyIsPemEncoded" /> to <c>true</c> if the key is PEM encoded.</remarks>
    public string SigningKey { get; set; } = default!;

    /// <summary>
    /// specifies how tokens are to be signed. symmetrically or asymmetrically.
    /// </summary>
    /// <remarks>don't forget to set an appropriate <see cref="SigningAlgorithm" /> if changing to <see cref="TokenSigningStyle.Symmetric" /></remarks>
    public TokenSigningStyle SigningStyle { get; set; } = TokenSigningStyle.Symmetric;

    /// <summary>
    /// security algorithm used to sign keys.
    /// </summary>
    /// <remarks>
    /// defaults to HmacSha256 for symmetric keys. don't forget to set an appropriate algorithm when changing <see cref="SigningStyle" /> to
    /// <see cref="TokenSigningStyle.Asymmetric" />
    /// </remarks>
    public string SigningAlgorithm { get; set; } = SecurityAlgorithms.HmacSha256Signature;

    /// <summary>
    /// specifies whether the key is pem encoded.
    /// </summary>
    public bool KeyIsPemEncoded { get; set; }

#pragma warning disable CS1574
    /// <summary>
    /// if specified, this function will be used to generate a <c>kid</c> for asymmetric key generation.
    /// the <c>string</c> value returned from this function will be set on the <see cref="RsaSecurityKey" />.<see cref="RsaSecurityKey.KeyId" /> property.
    /// </summary>
    public Func<RSA, string>? AsymmetricKidGenerator { get; set; }

    /// <summary>
    /// specify the privileges of the user
    /// NOTE: this should be specified at the time of jwt creation.
    /// </summary>
    public UserPrivileges User { get; } = new();

    /// <summary>
    /// the value for the 'audience' claim.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// the issuer
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// the value of the 'expiration' claim. should be in utc.
    /// NOTE: this should be set at the time of token creation.
    /// </summary>
    public DateTime? ExpireAt { get; set; }

    /// <summary>
    /// the compression algorithm  compressing the token payload.
    /// </summary>
    public string? CompressionAlgorithm { get; set; }
}