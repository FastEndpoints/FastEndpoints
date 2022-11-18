namespace FastEndpoints.Security;

/// <summary>
/// implement this class to define your own refresh token endpoints.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto that will be accepted by the refresh endpoint</typeparam>
/// <typeparam name="TResponse">the type of the response dto that will be sent by the refresh endpoint</typeparam>
public abstract class RefreshTokenService<TRequest, TResponse> : Endpoint<TRequest, TResponse>, IRefreshTokenService<TResponse>
    where TRequest : TokenRequest, new()
    where TResponse : TokenResponse, new()
{
    /// <summary>
    /// specifies the route of the refresh endpoint which will be automatically registered.
    /// </summary>
    [DontInject] public string RefreshEndpointRoute { get; protected init; } = "/api/refresh-token";

    /// <summary>
    /// specifies the secret key used to sign the jwt
    /// </summary>
    [DontInject] public string? TokenSigningKey { get; protected init; }

    /// <summary>
    /// specifies the signing style of the jwt
    /// </summary>
    [DontInject] public JWTBearer.TokenSigningStyle TokenSigningStyle { get; protected init; } = JWTBearer.TokenSigningStyle.Symmetric;

    /// <summary>
    /// specifies how long the access token should be valid for.
    /// </summary>
    [DontInject] public TimeSpan AccessTokenValidity { get; protected init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// specifies how long the refresh token should be valid for.
    /// </summary>
    [DontInject] public TimeSpan RefreshTokenValidity { get; protected init; } = TimeSpan.FromHours(4);

    /// <summary>
    /// specifies the token issuer
    /// </summary>
    [DontInject] public string? Issuer { get; protected init; }

    /// <summary>
    /// specifies the token audience
    /// </summary>
    [DontInject] public string? Audience { get; protected init; }

    /// <summary>
    /// do not call this method in your code!
    /// </summary>
    public sealed override void Configure()
    {
        Post(RefreshEndpointRoute);
        AllowAnonymous();
    }

    /// <summary>
    /// do not call this method in your code!
    /// </summary>
    public async sealed override Task HandleAsync(TRequest r, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(r.UserId))
            AddError(r => r.UserId, "A valid User Id is required for token renewal!");

        if (string.IsNullOrEmpty(r.RefreshToken))
            AddError(r => r.RefreshToken, "A valid refresh token is required for renewal!");

        ThrowIfAnyErrors();

        if (!await TokenIsValidAsync(r))
            ThrowError(r => r.RefreshToken, "This token is either expired or invalid!");

        Response = await ((IRefreshTokenService<TResponse>)this).CreateToken(
            r.UserId,
            async p => await UserPriviledgesForRenewalAsync(r, ref p));
    }

    /// <summary>
    /// this method will be called whenever a new access/refresh token pair has been generated.
    /// you can store the tokens and expiry dates for the purpose of verifying future refresh requests.
    /// </summary>
    /// <param name="response">the response dto that is about to be sent to the requesting client</param>
    public abstract Task StoreTokenForUserAsync(TResponse response);

    /// <summary>
    /// this method will be called when a client requests a new access/refresh token pair.
    /// use the received data to check and validate if a new token pair should be allowed to be created.
    /// return `true` to allow token creation or return `false` to deny token creation. 
    /// </summary>
    /// <param name="request">the request dto sent by the client</param>
    public abstract Task<bool> TokenIsValidAsync(TRequest request);

    /// <summary>
    /// specify the user priviledges to be embeded in the jwt when a refresh request is received and verification has passed in the <see cref="TokenIsValidAsync(TRequest)"/> method.
    /// this only applies to renewal/refresh requests received to this endpoint and not the initial jwt creation.
    /// </summary>
    /// <param name="request">the request dto received from the client</param>
    /// <param name="priviledges">the user priviledges to be embeded in the jwt such as roles/claims/permissions</param>
    public abstract Task UserPriviledgesForRenewalAsync(TRequest request, ref UserPriviledges priviledges);

    [HideFromDocs]
    async Task<TResponse> IRefreshTokenService<TResponse>.CreateToken(string userId, Action<UserPriviledges> userPriviledges)
    {
        if (TokenSigningKey is null)
            throw new ArgumentNullException($"{nameof(TokenSigningKey)} must be specified for [{Definition.EndpointType.FullName}]");

        var privs = new UserPriviledges();
        userPriviledges(privs);

        var accessExpiry = DateTime.UtcNow.Add(AccessTokenValidity);
        var refreshExpiry = DateTime.UtcNow.Add(RefreshTokenValidity);
        var token = new TResponse()
        {
            UserId = userId,
            AccessToken = JWTBearer.CreateToken(
                TokenSigningKey,
                accessExpiry,
                privs.Permissions,
                privs.Roles,
                privs.Claims,
                Issuer,
                Audience,
                TokenSigningStyle),
            AccessExpiry = accessExpiry,
            RefreshToken = Guid.NewGuid().ToString("N"),
            RefreshExpiry = refreshExpiry
        };

        await StoreTokenForUserAsync(token);

        return token;
    }

#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
    [Obsolete("This method is not supported in this class!", true), HideFromDocs]
    protected sealed override Task<TResponse> CreateTokenWith<TService>(string userId, Action<UserPriviledges> userPriviledges)
        => throw new NotSupportedException();
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
}