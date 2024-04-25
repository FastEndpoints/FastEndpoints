using Microsoft.IdentityModel.Tokens;

namespace FastEndpoints.Security;

//this class could inherit from JwtCreationOptions, but we want the lingo to be different for the public api.
//so we need to keep this class in sync manually with JwtCreationOptions and do mapping between the two in RefreshTokenService.CreateToken() method.
public class RefreshServiceOptions
{
    /// <summary>
    /// specifies the secret key used to sign the jwt. an exception will be thrown if a value is not specified.
    /// </summary>
    [DontInject]
    public string? TokenSigningKey { internal get; set; }

    /// <summary>
    /// specifies the signing style of the jwt. default is symmetric.
    /// </summary>
    [DontInject]
    public TokenSigningStyle TokenSigningStyle { internal get; set; } = TokenSigningStyle.Symmetric;

    /// <summary>
    /// security algo used to sign tokens.
    /// defaults to HmacSha256 for symmetric keys and RsaSha256 for asymmetric keys.
    /// </summary>
    [DontInject]
    public string TokenSigningAlgorithm { internal get; set; }

    /// <summary>
    /// specifies whether the key is pem encoded.
    /// </summary>
    [DontInject]
    public bool SigningKeyIsPemEncoded { get; set; }

    /// <summary>
    /// specifies how long the access token should be valid for. default is 5 minutes.
    /// </summary>
    [DontInject]
    public TimeSpan AccessTokenValidity { internal get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// specifies how long the refresh token should be valid for. default is 4 hours.
    /// </summary>
    [DontInject]
    public TimeSpan RefreshTokenValidity { internal get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// specifies the token issuer
    /// </summary>
    [DontInject]
    public string? Issuer { internal get; set; }

    /// <summary>
    /// specifies the token audience
    /// </summary>
    [DontInject]
    public string? Audience { internal get; set; }

    /// <summary>
    /// the compression algorithm  compressing the token payload.
    /// </summary>
    [DontInject]
    public string? TokenCompressionAlgorithm { get; set; }

    internal string RefreshRoute = "/api/refresh-token";
    internal Action<EndpointDefinition>? EpSettings;

    public RefreshServiceOptions()
    {
        TokenSigningAlgorithm = TokenSigningStyle == TokenSigningStyle.Symmetric
                                    ? SecurityAlgorithms.HmacSha256Signature
                                    : SecurityAlgorithms.RsaSha256;
    }

    /// <summary>
    /// endpoint configuration action
    /// </summary>
    /// <param name="refreshEndpointRoute">the route of the refresh token endpoint</param>
    /// <param name="ep">the action to be performed on the endpoint definition</param>
    public void Endpoint(string refreshEndpointRoute, Action<EndpointDefinition> ep)
    {
        RefreshRoute = refreshEndpointRoute;
        EpSettings = ep;
    }
}