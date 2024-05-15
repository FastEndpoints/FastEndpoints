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
}