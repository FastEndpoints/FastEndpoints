namespace FastEndpoints.Security;

public class RefreshServiceOptions
{
    /// <summary>
    /// specifies the secret key used to sign the jwt. an exception will be thrown if a value is not specified.
    /// </summary>
    [DontInject] public string? TokenSigningKey { internal get; set; }

    /// <summary>
    /// specifies the signing style of the jwt. default is symmetric.
    /// </summary>
    [DontInject] public JWTBearer.TokenSigningStyle TokenSigningStyle { internal get; set; } = JWTBearer.TokenSigningStyle.Symmetric;

    /// <summary>
    /// specifies how long the access token should be valid for. default is 5 minutes.
    /// </summary>
    [DontInject] public TimeSpan AccessTokenValidity { internal get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// specifies how long the refresh token should be valid for. default is 4 hours.
    /// </summary>
    [DontInject] public TimeSpan RefreshTokenValidity { internal get; set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// specifies the token issuer
    /// </summary>
    [DontInject] public string? Issuer { internal get; set; }

    /// <summary>
    /// specifies the token audience
    /// </summary>
    [DontInject] public string? Audience { internal get; set; }

    internal string refreshRoute = "/api/refresh-token";
    internal Action<EndpointDefinition>? epSettings;

    /// <summary>
    /// endpoint configuration action
    /// </summary>
    /// <param name="refreshEndpointRoute">the route of the refresh token endpoint</param>
    /// <param name="ep">the action to be performed on the endpoint definition</param>
    public void Endpoint(string refreshEndpointRoute, Action<EndpointDefinition> ep)
    {
        refreshRoute = refreshEndpointRoute;
        epSettings = ep;
    }
}