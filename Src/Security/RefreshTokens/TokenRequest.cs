namespace FastEndpoints.Security;

/// <summary>
/// base dto for access/refresh token renewal requests
/// </summary>
public class TokenRequest
{
    /// <summary>
    /// unique identifier of a user
    /// </summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// a single-use refresh token which will be valid for the duration specified by <see cref="TokenResponse.RefreshExpiry"/>
    /// </summary>
    public string RefreshToken { get; set; } = null!;
}
