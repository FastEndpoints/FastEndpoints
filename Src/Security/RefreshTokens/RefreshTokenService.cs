using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;

namespace FastEndpoints.Security;

/// <summary>
/// implement this class to define your own refresh token endpoints.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto that will be accepted by the refresh endpoint</typeparam>
/// <typeparam name="TResponse">the type of the response dto that will be sent by the refresh endpoint</typeparam>
public abstract class RefreshTokenService<TRequest, TResponse> : BaseEndpoint, IRefreshTokenService<TResponse>
    where TRequest : notnull, TokenRequest, new()
    where TResponse : notnull, TokenResponse, new()
{
    /// <summary>
    /// specifies the route of the refresh endpoint which will be automatically registered. default is "/api/refresh-token"
    /// </summary>
    [DontInject] public string RefreshEndpointRoute { get; protected set; } = "/api/refresh-token";

    /// <summary>
    /// specifies the secret key used to sign the jwt. an exception will be thrown if a value is not specified.
    /// </summary>
    [DontInject] public string? TokenSigningKey { get; protected set; }

    /// <summary>
    /// specifies the signing style of the jwt. default is symmetric.
    /// </summary>
    [DontInject] public JWTBearer.TokenSigningStyle TokenSigningStyle { get; protected set; } = JWTBearer.TokenSigningStyle.Symmetric;

    /// <summary>
    /// specifies how long the access token should be valid for. default is 5 minutes.
    /// </summary>
    [DontInject] public TimeSpan AccessTokenValidity { get; protected set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// specifies how long the refresh token should be valid for. default is 4 hours.
    /// </summary>
    [DontInject] public TimeSpan RefreshTokenValidity { get; protected set; } = TimeSpan.FromHours(4);

    /// <summary>
    /// specifies the token issuer
    /// </summary>
    [DontInject] public string? Issuer { get; protected set; }

    /// <summary>
    /// specifies the token audience
    /// </summary>
    [DontInject] public string? Audience { get; protected set; }

    private static readonly Type tRequest = typeof(TRequest);

    /// <summary>
    /// additional configuration for the refresh token endpoint
    /// </summary>
    public virtual void Configuration() { }

    public sealed override void Configure()
    {
        Definition.InternalConfigAction = b =>
        {
            b.Accepts<TRequest>("application/json");
            b.Produces<TResponse>(200, "application/json");
            b.ProducesProblemFE();
        };
        Configuration();
        Definition.Verbs = new[] { Http.POST.ToString() };
        Definition.Routes = new[] { RefreshEndpointRoute };
        Definition.AllowAnonymous(Http.POST);
    }

    internal async override Task ExecAsync(CancellationToken ct)
    {
        TRequest req = default!;

        try
        {
            req = await BindRequest<TRequest>(tRequest, ct);

            await ValidateRefreshRequestAsync(req, ValidationFailures);

            if (ValidationFailures.Count > 0)
                throw new ValidationFailureException(ValidationFailures, "Refresh token validation failed!");

            var res = await ((IRefreshTokenService<TResponse>)this)
                .CreateToken(
                    req.UserId,
                    async p => await UserPrivilegesForRenewalAsync(req, p));

            await HttpContext.Response.SendAsync(res, 200, null, ct);
        }
        catch (ValidationFailureException x)
        {
            if (!Definition.DoNotCatchExceptions)
                await HttpContext.Response.SendErrorsAsync(
                    (List<ValidationFailure>)x.Failures!,
                    Config.ErrOpts.StatusCode,
                    null,
                    ct);
            else
                throw;
        }
    }

    /// <summary>
    /// this method will be called whenever a new access/refresh token pair is being generated.
    /// store the tokens and expiry dates however you wish for the purpose of verifying future refresh requests.
    /// </summary>
    /// <param name="response">the response dto containing the tokens that's about to be sent to the requesting client</param>
    public abstract Task StoreTokenAsync(TResponse response);

    /// <summary>
    /// validate the incoming refresh request by checking the token and expiry against the previously stored data.
    /// if the token is not valid and a new token pair should not be created, simply add validation failures to the <paramref name="errors"/> collection.
    /// the failures you add to the <paramref name="errors"/> will be sent to the requesting client.
    /// if no failures are added, validation passes and a new token pair will be created and sent to the client.
    /// </summary>
    /// <param name="req">the incoming refresh request dto</param>
    public abstract Task ValidateRefreshRequestAsync(TRequest req, List<ValidationFailure> errors);

    /// <summary>
    /// specify the user privileges to be embeded in the jwt when a refresh request is received and verification has passed.
    /// this only applies to renewal/refresh requests received to the refresh endpoint and not the initial jwt creation.
    /// </summary>
    /// <param name="request">the request dto received from the client</param>
    /// <param name="privileges">the user priviledges to be embeded in the jwt such as roles/claims/permissions</param>
    public abstract Task UserPrivilegesForRenewalAsync(TRequest request, UserPrivileges privileges);

    [HideFromDocs]
    async Task<TResponse> IRefreshTokenService<TResponse>.CreateToken(string userId, Action<UserPrivileges> privileges)
    {
        if (TokenSigningKey is null)
            throw new ArgumentNullException($"{nameof(TokenSigningKey)} must be specified for [{Definition.EndpointType.FullName}]");

        var privs = new UserPrivileges();
        privileges(privs);

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

        await StoreTokenAsync(token);

        return token;
    }

    /// <summary>
    /// if this endpoint is part of an endpoint group, specify the type of the <see cref="FastEndpoints.Group"/> concrete class where the common configuration for the group is specified.
    /// <para>
    /// WARNING: this method can only be called after the endpoint route has been specified.
    /// </para>
    /// </summary>
    /// <typeparam name="TEndpointGroup">the type of your <see cref="FastEndpoints.Group"/> concrete class</typeparam>
    /// <exception cref="InvalidOperationException">thrown if endpoint route hasn't yet been specified</exception>
    protected sealed override void Group<TEndpointGroup>()
    {
        if (Definition.Routes is null)
        {
            throw new InvalidOperationException($"Endpoint group can only be specified after the route has been configured in the [{Definition.EndpointType.FullName}] endpoint class!");
        }
        new TEndpointGroup().Action(Definition);
    }

    /// <summary>
    /// describe openapi metadata for this endpoint. optionaly specify whether or not you want to clear the default Accepts/Produces metadata.
    /// <para>
    /// EXAMPLE: <c>b => b.Accepts&lt;Request&gt;("text/plain")</c>
    /// </para>
    /// </summary>
    /// <param name="builder">the route handler builder for this endpoint</param>
    /// <param name="clearDefaults">set to true if the defaults should be cleared</param>
    protected void Description(Action<RouteHandlerBuilder> builder, bool clearDefaults = false) => Definition.Description(builder, clearDefaults);

    /// <summary>
    /// if swagger auto tagging based on path segment is enabled, calling this method will prevent a tag from being added to this endpoint.
    /// </summary>
    protected void DontAutoTag() => Definition.DontAutoTag();

    /// <summary>
    /// use this only if you have your own exception catching middleware.
    /// if this method is called in config, an automatic error response will not be sent to the client by the library.
    /// all exceptions will be thrown and it would be the responsibility of your exeception catching middleware to handle them.
    /// </summary>
    protected void DontCatchExceptions() => Definition.DontCatchExceptions();

    /// <summary>
    /// set endpoint configurations options using an endpoint builder action ///
    /// </summary>
    /// <param name="builder">the builder for this endpoint</param>
    protected void Options(Action<RouteHandlerBuilder> builder) => Definition.Options(builder);

    /// <summary>
    /// specify an override route prefix for this endpoint if a global route prefix is enabled.
    /// this is ignored if a global route prefix is not configured.
    /// global prefix can be ignored by setting <c>string.Empty</c>
    /// </summary>
    /// <param name="routePrefix">route prefix value</param>
    protected void RoutePrefixOverride(string routePrefix) => Definition.RoutePrefixOverride(routePrefix);

    /// <summary>
    /// specify the json serializer context if code generation for request/response dtos is being used
    /// </summary>
    /// <typeparam name="TContext">the type of the json serializer context for this endpoint</typeparam>
    protected void SerializerContext<TContext>(TContext serializerContext) where TContext : JsonSerializerContext => Definition.SerializerContext = serializerContext;

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an action that sets values of an endpoint summary object</param>
    protected void Summary(Action<EndpointSummary> endpointSummary) => Definition.Summary(endpointSummary);

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an action that sets values of an endpoint summary object</param>
    protected void Summary(Action<EndpointSummary<TRequest>> endpointSummary) => Definition.Summary(endpointSummary);

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an endpoint summary instance</param>
    protected void Summary(EndpointSummary endpointSummary) => Definition.Summary(endpointSummary);

    /// <summary>
    /// specify one or more string tags for this endpoint so they can be used in the exclusion filter during registration.
    /// <para>HINT: these tags have nothing to do with swagger tags!</para>
    /// </summary>
    /// <param name="endpointTags">the tag values to associate with this endpoint</param>
    protected void Tags(params string[] endpointTags) => Definition.Tags(endpointTags);

    /// <summary>
    /// rate limit requests to this endpoint based on a request http header sent by the client.
    /// </summary>
    /// <param name="hitLimit">how many requests are allowed within the given duration</param>
    /// <param name="durationSeconds">the frequency in seconds where the accrued hit count should be reset</param>
    /// <param name="headerName">
    /// the name of the request header used to uniquely identify clients.
    /// header name can also be configured globally using <c>app.UseFastEndpoints(c=> c.ThrottleOptions...)</c>
    /// not specifying a header name will first look for 'X-Forwarded-For' header and if not present, will use `HttpContext.Connection.RemoteIpAddress`.
    /// </param>
    protected void Throttle(int hitLimit, double durationSeconds, string? headerName = null) => Definition.Throttle(hitLimit, durationSeconds, headerName);

    /// <summary>
    /// specify the version of the endpoint if versioning is enabled
    /// </summary>
    /// <param name="version">the version of this endpoint</param>
    /// <param name="deprecateAt">the version group number starting at which this endpoint should not be included in swagger document</param>
    protected void Version(int version, int? deprecateAt = null) => Definition.EndpointVersion(version, deprecateAt);

    [Obsolete] public Task HandleAsync() => throw new NotSupportedException("This is not the handler you're looking for ;-)");
    [Obsolete] public sealed override void Verbs(params string[] methods) => throw new NotSupportedException("These are not the verbs you're looking for ;-)");
}