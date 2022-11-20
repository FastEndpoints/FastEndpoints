using System.Text.Json.Serialization;

namespace FastEndpoints.Security;

/// <summary>
/// base dto for access/refresh token responses
/// </summary>
public class TokenResponse : TokenRequest
{
    /// <summary>
    /// the jwt access token which will be valid for the duration specified by <see cref="AccessExpiry"/>
    /// </summary>
    public string AccessToken { get; set; } = null!;

    /// <summary>
    /// the expiry date-time of the access token
    /// </summary>
    [JsonIgnore] public DateTime AccessExpiry { get; internal set; }

    /// <summary>
    /// the expiry date-time of the refresh token
    /// </summary>
    [JsonIgnore] public DateTime RefreshExpiry { get; internal set; }
}
