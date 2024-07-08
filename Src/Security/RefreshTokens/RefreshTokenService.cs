// ReSharper disable MemberCanBeProtected.Global

using System.Diagnostics.CodeAnalysis;

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
    RefreshServiceOptions? _opts;

    /// <summary>
    /// WARNING: do not call this method!
    /// </summary>
    [HideFromDocs]
    public sealed override void Configure()
    {
        if (_opts is null)
            throw new InvalidOperationException("Refresh token service is not configured!");

        _opts.EpSettings?.Invoke(Definition);

        Post(_opts.RefreshRoute);
        AllowAnonymous();
    }

    /// <summary>
    /// WARNING: do not call this method!
    /// </summary>
    [HideFromDocs]
    public sealed override async Task HandleAsync(TRequest req, CancellationToken ct)
    {
        await RefreshRequestValidationAsync(req);
        ThrowIfAnyErrors();
        var res = await ((IRefreshTokenService<TResponse>)this).CreateToken(req.UserId, null, true, req);
        await SendAsync(res, 200, ct);
    }

    /// <summary>
    /// configure the refresh token service options
    /// </summary>
    /// <param name="options">action to be performed on the refresh service options object</param>
    public void Setup(Action<RefreshServiceOptions> options)
    {
        _opts = new();
        options(_opts);
    }

    /// <summary>
    /// a hook for modifying jwt creation options per request. this method is called right before the actual jwt token is created allowing you to override token creation
    /// parameters per request if needed.
    /// </summary>
    /// <param name="isRefreshRequest">will be <c>true</c> if this is a refresh token request. will be false for initial token creation</param>
    /// <param name="jwtOptions">jwt token creation options which you can modify per request</param>
    /// <param name="request">the request dto. maybe null unless you supply it to the <see cref="Endpoint{TRequest,TResponse}.CreateTokenWith{TService}" /> method.</param>
    [SuppressMessage("ReSharper", "UnusedParameter.Global")]
    public virtual void OnTokenCreation(bool isRefreshRequest, JwtCreationOptions jwtOptions, TRequest? request) { }

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
    /// <param name="privileges">the user privileges to be embedded in the jwt such as roles/claims/permissions</param>
    public abstract Task SetRenewalPrivilegesAsync(TRequest request, UserPrivileges privileges);

    /// <summary>
    /// create a token response and map it to a different type. useful if you need to create the token manually by yourself.
    /// </summary>
    /// <typeparam name="T">the type to map to</typeparam>
    /// <param name="userId">the id of the user to create the token for</param>
    /// <param name="privileges">the user privileges to be embedded in the jwt such as roles/claims/permissions</param>
    /// <param name="map">a func that maps properties from <typeparamref name="TResponse" /> to <typeparamref name="T" /></param>
    /// <param name="isRenewal">specify if this is an initial login request or a renewal/refresh request</param>
    public async Task<T> CreateCustomToken<T>(string userId, Action<UserPrivileges> privileges, Func<TResponse, T> map, bool isRenewal = false)
    {
        var res = await ((IRefreshTokenService<TResponse>)this).CreateToken(userId, privileges, isRenewal, null);

        return map(res);
    }

    [HideFromDocs]
    async Task<TResponse> IRefreshTokenService<TResponse>.CreateToken(string userId, Action<UserPrivileges>? privileges, bool isRenewal, object? request)
    {
        if (_opts?.TokenSigningKey is null)
            throw new ArgumentNullException($"{nameof(_opts.TokenSigningKey)} must be specified for [{Definition.EndpointType.FullName}]");

        var privs = new UserPrivileges();

        privileges?.Invoke(privs);

        if (isRenewal && request is not null)
            await SetRenewalPrivilegesAsync((TRequest)request, privs);

        var time = Conf.ServiceResolver.TryResolve<TimeProvider>() ?? TimeProvider.System;
        var accessExpiry = time.GetUtcNow().Add(_opts.AccessTokenValidity).UtcDateTime;
        var refreshExpiry = time.GetUtcNow().Add(_opts.RefreshTokenValidity).UtcDateTime;
        var token = new TResponse
        {
            UserId = userId,
            AccessToken = JwtBearer.CreateToken(
                o =>
                {
                    o.SigningKey = _opts.TokenSigningKey;
                    o.SigningStyle = _opts.TokenSigningStyle;
                    o.SigningAlgorithm = _opts.TokenSigningAlgorithm;
                    o.KeyIsPemEncoded = _opts.SigningKeyIsPemEncoded;
                    o.ExpireAt = accessExpiry;
                    o.User.Permissions.AddRange(privs.Permissions);
                    o.User.Roles.AddRange(privs.Roles);
                    o.User.Claims.AddRange(privs.Claims);
                    o.Issuer = _opts.Issuer;
                    o.Audience = _opts.Audience;
                    o.CompressionAlgorithm = _opts.TokenCompressionAlgorithm;
                    OnTokenCreation(isRenewal, o, request as TRequest);
                }),
            AccessExpiry = accessExpiry,
            RefreshToken = Guid.NewGuid().ToString("N"),
            RefreshExpiry = refreshExpiry
        };

        await PersistTokenAsync(token);

        return token;
    }
}