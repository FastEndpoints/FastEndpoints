using Microsoft.IdentityModel.Tokens;

namespace FastEndpoints.Security;

/// <summary>
/// options for creating jwt tokens
/// </summary>
public sealed class JwtCreationOptions
{
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

    /// <summary>
    /// specify the privileges of the user
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
    /// </summary>
    public DateTime? ExpireAt { get; set; }

    /// <summary>
    /// the compression algorithm  compressing the token payload.
    /// </summary>
    public string? CompressionAlgorithm { get; set; }
}