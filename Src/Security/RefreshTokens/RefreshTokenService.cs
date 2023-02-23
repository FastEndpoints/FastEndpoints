namespace FastEndpoints.Security;

/// <summary>
/// implement this class to define your own refresh token endpoints.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto that will be accepted by the refresh endpoint</typeparam>
/// <typeparam name="TResponse">the type of the response dto that will be sent by the refresh endpoint</typeparam>
public abstract class RefreshTokenService<TRequest, TResponse> : Endpoint<TRequest, TResponse>, IRefreshTokenService<TResponse>
    where TRequest : notnull, TokenRequest, new()
    where TResponse : notnull, TokenResponse, new()
{
    private RefreshServiceOptions? opts;

    /// <summary>
    /// WARNING: do not call this method!
    /// </summary>
    [HideFromDocs]
    public sealed override void Configure()
    {
        if (opts is null)
            throw new InvalidOperationException($"Refresh token service is not configured!");

        opts.epSettings?.Invoke(Definition);

        Post(opts.refreshRoute);
        AllowAnonymous();
    }

    /// <summary>
    /// WARNING: do not call this method!
    /// </summary>
    [HideFromDocs]
    public async sealed override Task HandleAsync(TRequest req, CancellationToken ct)
    {
        await RefreshRequestValidationAsync(req);
        ThrowIfAnyErrors();
        var res = await ((IRefreshTokenService<TResponse>)this).CreateToken(req.UserId, null, req);
        await SendAsync(res, 200, ct);
    }

    /// <summary>
    /// configure the refresh token service options
    /// </summary>
    /// <param name="options">action to be performed on the refresh service options object</param>
    public void Setup(Action<RefreshServiceOptions> options)
    {
        opts = new();
        options(opts);
    }

    /// <summary>
    /// this method will be called whenever a new access/refresh token pair is being generated.
    /// store the tokens and expiry dates however you wish for the purpose of verifying future refresh requests.
    /// </summary>
    /// <param name="response">the response dto containing the tokens that's about to be sent to the requesting client</param>
    public abstract Task PersistTokenAsync(TResponse response);

    /// <summary>
    /// validate the incoming refresh request by checking the token and expiry against the previously stored data.
    /// if the token is not valid and a new token pair should not be created, simply add validation errors using the <c>AddError()</c> method.
    /// the failures you add will be sent to the requesting client.
    /// if no failures are added, validation passes and a new token pair will be created and sent to the client.
    /// </summary>
    /// <param name="req">the incoming refresh request dto</param>
    public abstract Task RefreshRequestValidationAsync(TRequest req);

    /// <summary>
    /// specify the user privileges to be embeded in the jwt when a refresh request is received and validation has passed.
    /// this only applies to renewal/refresh requests received to the refresh endpoint and not the initial jwt creation.
    /// </summary>
    /// <param name="request">the request dto received from the client</param>
    /// <param name="privileges">the user priviledges to be embeded in the jwt such as roles/claims/permissions</param>
    public abstract Task SetRenewalPrivilegesAsync(TRequest request, UserPrivileges privileges);

    [HideFromDocs]
    async Task<TResponse> IRefreshTokenService<TResponse>.CreateToken(string userId, Action<UserPrivileges>? privileges, object? request)
    {
        if (opts?.TokenSigningKey is null)
            throw new ArgumentNullException($"{nameof(opts.TokenSigningKey)} must be specified for [{Definition.EndpointType.FullName}]");

        var privs = new UserPrivileges();

        if (privileges is not null) //only true on initial token creation
            privileges(privs);

        if (request is not null) //only true on renewal
            await SetRenewalPrivilegesAsync((TRequest)request, privs);

        var accessExpiry = DateTime.UtcNow.Add(opts.AccessTokenValidity);
        var refreshExpiry = DateTime.UtcNow.Add(opts.RefreshTokenValidity);
        var token = new TResponse()
        {
            UserId = userId,
            AccessToken = JWTBearer.CreateToken(
                opts.TokenSigningKey,
                accessExpiry,
                privs.Permissions,
                privs.Roles,
                privs.Claims,
                opts.Issuer,
                opts.Audience,
                opts.TokenSigningStyle),
            AccessExpiry = accessExpiry,
            RefreshToken = Guid.NewGuid().ToString("N"),
            RefreshExpiry = refreshExpiry
        };

        await PersistTokenAsync(token);

        return token;
    }
}