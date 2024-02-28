using Microsoft.IdentityModel.Tokens;

namespace FastEndpoints.Security;

/// <summary>
/// options for creating jwt tokens
/// </summary>
public sealed class JwtCreationOptions
{
    /// <summary>
    /// the key used to sign jwts symmetrically or the private-key when jwts are signed asymmetrically.
    /// </summary>
    /// <remarks>the key can be in PEM format. make sure to set <see cref="KeyIsPemEncoded" /> to <c>true</c> if the key is PEM encoded.</remarks>
    public string SigningKey { get; set; } = default!;

    /// <summary>
    /// specifies how tokens are to be signed. symmetrically or asymmetrically.
    /// </summary>
    public TokenSigningStyle SigningStyle { get; set; } = TokenSigningStyle.Symmetric;

    /// <summary>
    /// specifies whether the key is pem encoded.
    /// </summary>
    public bool KeyIsPemEncoded { get; set; }

    /// <summary>
    /// security algo used to sign symmetric keys
    /// </summary>
    public string SymmetricKeyAlgorithm { get; set; } = SecurityAlgorithms.HmacSha256Signature;

    /// <summary>
    /// security algo used to sign asymmetric keys
    /// </summary>
    public string AsymmetricKeyAlgorithm { get; set; } = SecurityAlgorithms.RsaSha256;

    /// <summary>
    /// specify the privileges of the user
    /// </summary>
    public UserPrivileges User { get; } = new();

    /// <summary>the value for the 'audience' claim.</summary>
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