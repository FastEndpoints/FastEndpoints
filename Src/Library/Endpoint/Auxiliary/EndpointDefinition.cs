using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using static FastEndpoints.Config;
using static FastEndpoints.Constants;

namespace FastEndpoints;

/// <summary>
/// represents the configuration settings of an endpoint
/// </summary>
public sealed class EndpointDefinition
{
    //these can only be set from internal code but accessible for user
    public Type EndpointType { get; internal set; }
    public Type ReqDtoType { get; internal set; }
    public string[]? Routes { get; internal set; }
    public Type? ValidatorType { get; internal set; }
    public Http[]? Verbs { get; internal set; }

    //these props can be changed in global config using methods below
    public bool AllowAnyPermission { get; private set; }
    public string[]? AllowedPermissions { get; private set; }
    public bool AllowAnyClaim { get; private set; }
    public string[]? AllowedClaimTypes { get; private set; }
    public string[]? AllowedRoles { get; private set; }
    public string[]? AnonymousVerbs { get; private set; }
    public string[]? AuthSchemeNames { get; private set; }
    public bool DontAutoTagEndpoints { get; private set; }
    public bool DontBindFormData { get; private set; }
    public bool DoNotCatchExceptions { get; private set; }
    public EndpointSummary? EndpointSummary { get; private set; }
    public string[]? EndpointTags { get; private set; }
    public bool FormDataAllowed { get; private set; }
    public string? OverriddenRoutePrefix { get; private set; }
    public string[]? PreBuiltUserPolicies { get; private set; }
    public string SecurityPolicyName => $"epPolicy:{EndpointType.FullName}";
    public bool ThrowIfValidationFails { get; private set; } = true;
    public EpVersion Version { get; } = new();
    [Obsolete("This property will be removed in the next major version!")] //todo: remove ability to make validators scoped in favor of CreateScope() method
    public bool ValidatorIsScoped { get; private set; }

    //only accessible to internal code
    internal bool ExecuteAsyncImplemented;
    internal HitCounter? HitCounter { get; private set; }
    internal Action<RouteHandlerBuilder> InternalConfigAction;
    internal object? RequestBinder;
    internal HashSet<object> PreProcessorList = new(new TypeEqualityComparer());
    internal HashSet<object> PostProcessorList = new(new TypeEqualityComparer());
    internal ServiceBoundEpProp[]? ServiceBoundEpProps;
    internal JsonSerializerContext? SerializerContext;
    internal ResponseCacheAttribute? ResponseCacheSettings { get; private set; }
    internal Action<RouteHandlerBuilder>? UserConfigAction { get; private set; }

    /// <summary>
    /// enable file uploads with multipart/form-data content type
    /// </summary>
    /// <param name="dontAutoBindFormData">
    /// set 'true' to disable auto binding of form data which enables uploading and reading of large files without buffering to memory/disk.
    /// you can access the multipart sections for reading via the FormFileSectionsAsync() method.
    /// </param>
    public void AllowFileUploads(bool dontAutoBindFormData = false)
    {
        FormDataAllowed = true;
        DontBindFormData = dontAutoBindFormData;
    }

    /// <summary>
    /// enable multipart/form-data submissions
    /// </summary>
    public void AllowFormData() => FormDataAllowed = true;

    /// <summary>
    /// allows access if the claims principal has ANY of the given permissions
    /// <para>WARNING: setting permissions globally will make the endpoint level call ineffective. i.e. the endpoint level call will be ignored.</para>
    /// </summary>
    /// <param name="permissions">the permissions</param>
    public void Permissions(params string[] permissions)
    {
        AllowAnyPermission = true;
        AllowedPermissions = permissions;
    }

    /// <summary>
    /// allows access if the claims principal has ALL of the given permissions
    /// <para>WARNING: setting permissions globally will make the endpoint level call ineffective. i.e. the endpoint level call will be ignored.</para>
    /// </summary>
    /// <param name="permissions">the permissions</param>
    public void PermissionsAll(params string[] permissions)
    {
        AllowAnyPermission = false;
        AllowedPermissions = permissions;
    }

    /// <summary>
    /// allows access if the claims principal has ANY of the given claim types
    /// <para>WARNING: setting claims globally will make the endpoint level call ineffective. i.e. the endpoint level call will be ignored.</para>
    /// </summary>
    /// <param name="claimTypes">the claim types</param>
    public void Claims(params string[] claimTypes)
    {
        AllowAnyClaim = true;
        AllowedClaimTypes = claimTypes;
    }

    /// <summary>
    /// allows access if the claims principal has ALL of the given claim types
    /// <para>WARNING: setting claims globally will make the endpoint level call ineffective. i.e. the endpoint level call will be ignored.</para>
    /// </summary>
    /// <param name="claimTypes">the claim types</param>
    public void ClaimsAll(params string[] claimTypes)
    {
        AllowAnyClaim = false;
        AllowedClaimTypes = claimTypes;
    }

    /// <summary>
    /// allow unauthenticated requests to this endpoint. optionally specify a set of verbs to allow unauthenticated access with.
    /// i.e. if the endpoint is listening to POST, PUT &amp; PATCH and you specify AllowAnonymous(Http.POST), then only PUT &amp; PATCH will require authentication.
    /// </summary>
    public void AllowAnonymous(params Http[] verbs)
    {
        AnonymousVerbs =
            verbs.Length > 0
            ? verbs.Select(v => v.ToString()).ToArray()
            : Enum.GetNames(Types.Http);
    }

    /// <summary>
    /// specify which authentication schemes to use for authenticating requests to this endpoint
    /// <para>WARNING: setting auth schemes globally will make the endpoint level call ineffective. i.e. the endpoint level call will be ignored.</para>
    /// </summary>
    /// <param name="authSchemeNames">the authentication scheme names</param>
    public void AuthSchemes(params string[]? authSchemeNames) => AuthSchemeNames = authSchemeNames;

    /// <summary>
    /// if swagger auto tagging based on path segment is enabled, calling this method will prevent a tag from being added to this endpoint.
    /// </summary>
    public void DontAutoTag() => DontAutoTagEndpoints = true;

    /// <summary>
    /// specify one or more authorization policy names you have added to the middleware pipeline during app startup/ service configuration that should be applied to this endpoint.
    /// <para>WARNING: setting policies globally will make the endpoint level call ineffective. i.e. the endpoint level call will be ignored.</para>
    /// </summary>
    /// <param name="policyNames">one or more policy names (must have been added to the pipeline on startup)</param>
    public void Policies(params string[] policyNames) => PreBuiltUserPolicies = policyNames;

    /// <summary>
    /// allows access if the claims principal has ANY of the given roles
    /// <para>WARNING: setting roles globally will make the endpoint level call ineffective. i.e. the endpoint level call will be ignored.</para>
    /// </summary>
    /// <param name="rolesNames">one or more roles that has access</param>
    public void Roles(params string[]? rolesNames) => AllowedRoles = rolesNames;

    /// <summary>
    /// specify an override route prefix for this endpoint if a global route prefix is enabled.
    /// this is ignored if a global route prefix is not configured.
    /// global prefix can be ignored by setting <c>string.Empty</c>
    /// <para>WARNING: setting a route prefix override globally makes the endpoint level override ineffective. i.e. RoutePrefixOverride() method call on endpoint level will be ignored.</para>
    /// </summary>
    /// <param name="routePrefix">route prefix value</param>
    public void RoutePrefixOverride(string routePrefix) => OverriddenRoutePrefix = routePrefix;

    //todo: remove ability to make validators scoped in favor of CreateScope() method
    [Obsolete("Ability to register validators as scoped will be removed in next major version. Use CreateScope() method instead.")]
    public void ScopedValidator() => ValidatorIsScoped = true;

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an action that sets values of an endpoint summary object</param>
    public void Summary(Action<EndpointSummary> endpointSummary) => endpointSummary(EndpointSummary ??= new());

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an action that sets values of an endpoint summary object</param>
    public void Summary<TRequest>(Action<EndpointSummary<TRequest>> endpointSummary) where TRequest : notnull, new()
    {
        var summary = EndpointSummary as EndpointSummary<TRequest> ?? new EndpointSummary<TRequest>();
        endpointSummary(summary);
        EndpointSummary = summary;
    }

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an endpoint summary instance</param>
    public void Summary(EndpointSummary endpointSummary) => EndpointSummary = endpointSummary;

    /// <summary>
    /// specify one or more string tags for this endpoint so they can be used in the exclusion filter during registration.
    /// <para>HINT: these tags have nothing to do with swagger tags!</para>
    /// </summary>
    /// <param name="endpointTags">the tag values to associate with this endpoint</param>
    public void Tags(params string[] endpointTags) => EndpointTags = endpointTags;

    /// <summary>
    /// use this only if you have your own exception catching middleware.
    /// if this method is called in config, an automatic error response will not be sent to the client by the library.
    /// all exceptions will be thrown and it would be your exeception catching middleware to handle them.
    /// </summary>
    public void DontCatchExceptions() => DoNotCatchExceptions = true;

    /// <summary>
    /// disable auto validation failure responses (400 bad request with error details) for this endpoint.
    /// <para>HINT: this only applies to request dto validation.</para>
    /// </summary>
    public void DontThrowIfValidationFails() => ThrowIfValidationFails = false;

    /// <summary>
    /// specify response caching settings for this endpoint
    /// </summary>
    /// <param name="durationSeconds">the duration in seconds for which the response is cached</param>
    /// <param name="location">the location where the data from a particular URL must be cached</param>
    /// <param name="noStore">specify whether the data should be stored or not</param>
    /// <param name="varyByHeader">the value for the Vary response header</param>
    /// <param name="varyByQueryKeys">the query keys to vary by</param>
    public void ResponseCache(int durationSeconds, ResponseCacheLocation location = ResponseCacheLocation.Any, bool noStore = false, string? varyByHeader = null, string[]? varyByQueryKeys = null)
    {
        ResponseCacheSettings = new()
        {
            Duration = durationSeconds,
            Location = location,
            NoStore = noStore,
            VaryByHeader = varyByHeader,
            VaryByQueryKeys = varyByQueryKeys
        };
    }

    /// <summary>
    /// set endpoint configurations options using an endpoint builder action ///
    /// </summary>
    /// <param name="builder">the builder for this endpoint</param>
    public void Options(Action<RouteHandlerBuilder> builder) => UserConfigAction = builder + UserConfigAction;

    /// <summary>
    /// describe openapi metadata for this endpoint. optionaly specify whether or not you want to clear the default Accepts/Produces metadata.
    /// <para>
    /// EXAMPLE: <c>b => b.Accepts&lt;Request&gt;("text/plain")</c>
    /// </para>
    /// </summary>
    /// <param name="builder">the route handler builder for this endpoint</param>
    /// <param name="clearDefaults">set to true if the defaults should be cleared</param>
    public void Description(Action<RouteHandlerBuilder> builder, bool clearDefaults = false)
    {
        UserConfigAction = clearDefaults
                                  ? ClearDefaultAcceptsProducesMetadata + builder + UserConfigAction
                                  : builder + UserConfigAction;
    }

    private static readonly Action<RouteHandlerBuilder> ClearDefaultAcceptsProducesMetadata = b =>
    {
        b.Add(epBuilder =>
        {
            foreach (var m in epBuilder.Metadata.Where(o => o.GetType().Name is ProducesMetadata or AcceptsMetaData).ToArray())
                epBuilder.Metadata.Remove(m);
        });
    };

    /// <summary>
    /// rate limit requests to this endpoint based on a request http header sent by the client.
    /// </summary>
    /// <param name="hitLimit">how many requests are allowed within the given duration</param>
    /// <param name="durationSeconds">the frequency in seconds where the accrued hit count should be reset</param>
    /// <param name="headerName">
    /// the name of the request header used to uniquely identify clients.
    /// header name can also be configured globally using <c>app.UseFastEndpoints(c=> c.Throttle...)</c>
    /// not specifying a header name will first look for 'X-Forwarded-For' header and if not present, will use `HttpContext.Connection.RemoteIpAddress`.
    /// </param>
    public void Throttle(int hitLimit, double durationSeconds, string? headerName = null) => HitCounter = new(headerName, durationSeconds, hitLimit);

    /// <summary>
    /// validator that should be used for this endpoint
    /// </summary>
    /// <typeparam name="TValidator">the type of the validator</typeparam>
    /// <param name="isScoped">set to true if you want to register the validator as scoped instead of singleton. which will enable constructor injection at the cost of performance.</param>
    public void Validator<TValidator>(bool isScoped = false)
    {
        ValidatorType = typeof(TValidator);
        if (isScoped)
            ScopedValidator();
    }

    /// <summary>
    /// adds global pre-processors to this endpoint definition. these pre-processors are executed before the pre-processors configured at the endpoint level.
    /// </summary>
    /// <param name="preProcessors">the pre-processors to add</param>
    public void PreProcessors(params IGlobalPreProcessor[] preProcessors)
    {
        for (var i = 0; i < preProcessors.Length; i++)
        {
            PreProcessorList.Add(preProcessors[i]);
        }
    }

    /// <summary>
    /// adds global post-processors to this endpoint definition. these post-processors are executed before the post-processors configured at the endpoint level.
    /// </summary>
    /// <param name="postProcessors">the post-processors to add</param>
    public void PostProcessors(params IGlobalPostProcessor[] postProcessors)
    {
        for (var i = 0; i < postProcessors.Length; i++)
        {
            PostProcessorList.Add(postProcessors[i]);
        }
    }

    /// <summary>
    /// specify the version of the endpoint if versioning is enabled
    /// </summary>
    /// <param name="version">the version of this endpoint</param>
    /// <param name="deprecateAt">the version group number starting at which this endpoint should not be included in swagger document</param>
    public void EndpointVersion(int version, int? deprecateAt = null)
    {
        Version.Current = version;
        Version.DeprecatedAt = deprecateAt ?? 0;
    }
}

/// <summary>
/// represents an enpoint version
/// </summary>
public sealed class EpVersion
{
    public int Current { get; internal set; }
    public int DeprecatedAt { get; internal set; }

    internal void Setup()
    {
        if (Current == 0)
            Current = VerOpts.DefaultVersion;
    }
}

internal sealed class ServiceBoundEpProp
{
    public Type PropType { get; set; }
    public Action<object, object> PropSetter { get; set; }
}

internal class TypeEqualityComparer : IEqualityComparer<object>
{
    public new bool Equals(object? x, object? y) => x?.GetType() == y?.GetType();
    public int GetHashCode([DisallowNull] object obj) => obj.GetType().GetHashCode();
}