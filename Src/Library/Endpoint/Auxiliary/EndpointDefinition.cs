using FluentValidation;
using Microsoft.AspNetCore.Authorization;
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
    public Type? MapperType { get; internal set; }
    public Type ReqDtoType { get; internal set; }
    public string[]? Routes { get; internal set; }
    public string SecurityPolicyName => $"epPolicy:{EndpointType.FullName}";
    public Type? ValidatorType { get; internal set; }
    public string[]? Verbs { get; internal set; }
    public EpVersion Version { get; } = new();

    //these props can be changed in global config using methods below
    public bool AllowAnyPermission { get; private set; }
    public List<string>? AllowedPermissions { get; private set; }
    public bool AllowAnyClaim { get; private set; }
    public List<string>? AllowedClaimTypes { get; private set; }
    public List<string>? AllowedRoles { get; private set; }
    public string[]? AnonymousVerbs { get; private set; }
    public List<string>? AuthSchemeNames { get; private set; }
    public bool DontAutoTagEndpoints { get; private set; }
    public bool DontBindFormData { get; private set; }
    public bool DoNotCatchExceptions { get; private set; }
    public EndpointSummary? EndpointSummary { get; private set; }
    public List<string>? EndpointTags { get; private set; }
    public string? FormDataContentType { get; private set; }
    public string? OverriddenRoutePrefix { get; private set; }
    public List<string>? PreBuiltUserPolicies { get; private set; }
    public Action<AuthorizationPolicyBuilder>? PolicyBuilder { get; private set; }
    public bool ThrowIfValidationFails { get; private set; } = true;

    //only accessible to internal code
    internal bool AcceptsAnyContentType;
    internal bool? AcceptsMetaDataPresent;
    internal object[]? EpAttributes;
    internal bool ExecuteAsyncImplemented;
    internal bool FoundDuplicateValidators;
    internal HitCounter? HitCounter { get; private set; }
    internal Action<RouteHandlerBuilder> InternalConfigAction;
    internal bool ImplementsConfigure;
    internal bool IsInitialized;
    internal object? RequestBinder;
    internal List<object> PreProcessorList = new();
    internal List<object> PostProcessorList = new();
    internal ServiceBoundEpProp[]? ServiceBoundEpProps;
    internal JsonSerializerContext? SerializerContext;
    internal ResponseCacheAttribute? ResponseCacheSettings { get; private set; }
    internal IResponseInterceptor? ResponseIntrcptr { get; private set; }
    internal Action<RouteHandlerBuilder>? UserConfigAction { get; private set; }

    private object? mapper;
    internal object? GetMapper()
    {
        if (mapper is null && MapperType is not null)
            mapper = Config.ServiceResolver.CreateSingleton(MapperType);

        return mapper;
    }

    private object? validator;
    internal object? GetValidator()
    {
        if (validator is null && ValidatorType is not null)
            validator = Config.ServiceResolver.CreateSingleton(ValidatorType);

        return validator;
    }

    private static readonly Action<RouteHandlerBuilder> ClearDefaultAcceptsProducesMetadata = b =>
    {
        b.Add(epBuilder =>
        {
            foreach (var m in epBuilder.Metadata.Where(o => o.GetType().Name is ProducesMetadata or AcceptsMetaData).ToArray())
                epBuilder.Metadata.Remove(m);
        });
    };

    private static void AddProcessor(Order order, object[] processors, List<object> list)
    {
        var pos = 0;
        for (var i = 0; i < processors.Length; i++)
        {
            var p = processors[i];
            if (!list.Contains(p, TypeEqualityComparer.Instance))
            {
                if (order == Order.Before)
                    list.Insert(pos, p);
                else
                    list.Add(p);
                pos++;
            }
        }
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
    /// allow unauthenticated requests to this endpoint for a specified set of http verbs.
    /// </summary>
    public void AllowAnonymous(string[] verbs)
    {
        AnonymousVerbs = verbs;
    }

    /// <summary>
    /// enable file uploads with multipart/form-data content type
    /// </summary>
    /// <param name="dontAutoBindFormData">
    /// set 'true' to disable auto binding of form data which enables uploading and reading of large files without buffering to memory/disk.
    /// you can access the multipart sections for reading via the FormFileSectionsAsync() method.
    /// </param>
    public void AllowFileUploads(bool dontAutoBindFormData = false)
    {
        FormDataContentType = "multipart/form-data";
        DontBindFormData = dontAutoBindFormData;
    }

    /// <summary>
    /// enable form-data submissions
    /// </summary>
    /// <param name="urlEncoded">set to true to accept `application/x-www-form-urlencoded` content instead of `multipart/form-data` content.</param>
    public void AllowFormData(bool urlEncoded = false) => FormDataContentType = urlEncoded ? "application/x-www-form-urlencoded" : "multipart/form-data";

    /// <summary>
    /// specify which authentication schemes to use for authenticating requests to this endpoint
    /// <para>HINT: these auth schemes will be applied in addition to endpoint level auth schemes if there's any</para>
    /// </summary>
    /// <param name="authSchemeNames">the authentication scheme names</param>
    public void AuthSchemes(params string[] authSchemeNames)
    {
        AuthSchemeNames?.AddRange(authSchemeNames);
        AuthSchemeNames ??= new(authSchemeNames);
    }

    /// <summary>
    /// allows access if the claims principal has ANY of the given claim types
    /// <para>HINT: these claims will be applied in addition to endpoint level claims if there's any</para>
    /// </summary>
    /// <param name="claimTypes">the claim types</param>
    public void Claims(params string[] claimTypes)
    {
        AllowAnyClaim = true;
        AllowedClaimTypes?.AddRange(claimTypes);
        AllowedClaimTypes ??= new(claimTypes);
    }

    /// <summary>
    /// allows access if the claims principal has ALL of the given claim types
    /// <para>HINT: these claims will be applied in addition to endpoint level claims if there's any</para>
    /// </summary>
    /// <param name="claimTypes">the claim types</param>
    public void ClaimsAll(params string[] claimTypes)
    {
        AllowAnyClaim = false;
        AllowedClaimTypes?.AddRange(claimTypes);
        AllowedClaimTypes ??= new(claimTypes);
    }

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

    /// <summary>
    /// if swagger auto tagging based on path segment is enabled, calling this method will prevent a tag from being added to this endpoint.
    /// </summary>
    public void DontAutoTag() => DontAutoTagEndpoints = true;

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
    /// specify the version of the endpoint if versioning is enabled
    /// </summary>
    /// <param name="version">the version of this endpoint</param>
    /// <param name="deprecateAt">the version group number starting at which this endpoint should not be included in swagger document</param>
    public void EndpointVersion(int version, int? deprecateAt = null)
    {
        Version.Current = version;
        Version.DeprecatedAt = deprecateAt ?? 0;
    }

    /// <summary>
    /// set endpoint configurations options using an endpoint builder action ///
    /// </summary>
    /// <param name="builder">the builder for this endpoint</param>
    public void Options(Action<RouteHandlerBuilder> builder) => UserConfigAction = builder + UserConfigAction;

    /// <summary>
    /// allows access if the claims principal has ANY of the given permissions
    /// <para>HINT: these permissions will be applied in addition to endpoint level permissions if there's any</para>
    /// </summary>
    /// <param name="permissions">the permissions</param>
    public void Permissions(params string[] permissions)
    {
        AllowAnyPermission = true;
        AllowedPermissions?.AddRange(permissions);
        AllowedPermissions ??= new(permissions);
    }

    /// <summary>
    /// allows access if the claims principal has ALL of the given permissions
    /// <para>HINT: these permissions will be applied in addition to endpoint level permissions if there's any</para>
    /// </summary>
    /// <param name="permissions">the permissions</param>
    public void PermissionsAll(params string[] permissions)
    {
        AllowAnyPermission = false;
        AllowedPermissions?.AddRange(permissions);
        AllowedPermissions ??= new(permissions);
    }

    /// <summary>
    /// specify an action for building an authorization requirement which should be added to all endpoints globally.
    /// <para>HINT: these global level requirements will be combined with the requirements specified at the endpoint level if there's any.</para>
    /// </summary>
    /// <param name="policy">th policy builder action</param>
    public void Policy(Action<AuthorizationPolicyBuilder> policy) => PolicyBuilder = policy + PolicyBuilder;

    /// <summary>
    /// specify one or more authorization policy names you have added to the middleware pipeline during app startup/ service configuration that should be applied to this endpoint.
    /// <para>HINT: these policies will be applied in addition to endpoint level policies if there's any</para>
    /// </summary>
    /// <param name="policyNames">one or more policy names (must have been added to the pipeline on startup)</param>
    public void Policies(params string[] policyNames)
    {
        PreBuiltUserPolicies?.AddRange(policyNames);
        PreBuiltUserPolicies ??= new(policyNames);
    }

    /// <summary>
    /// adds global post-processors to an endpoint definition which are to be executed in addition to the ones configured at the endpoint level.
    /// </summary>
    /// <param name="order">set to <see cref="Order.Before"/> if the global post-processors should be executed before endpoint post-processors. <see cref="Order.After"/> will execute global processors after endpoint level processors</param>
    /// <param name="postProcessors">the post-processors to add</param>
    public void PostProcessors(Order order, params IGlobalPostProcessor[] postProcessors)
    {
        AddProcessor(order, postProcessors, PostProcessorList);
    }

    /// <summary>
    /// adds global pre-processors to an endpoint definition which are to be executed in addition to the ones configured at the endpoint level.
    /// </summary>
    /// <param name="order">set to <see cref="Order.Before"/> if the global pre-processors should be executed before endpoint pre-processors. <see cref="Order.After"/> will execute global processors after endpoint level processors</param>
    /// <param name="preProcessors">the pre-processors to add</param>
    public void PreProcessors(Order order, params IGlobalPreProcessor[] preProcessors)
    {
        AddProcessor(order, preProcessors, PreProcessorList);
    }

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
    /// configure a response interceptor to be called before any SendAsync() methods are called.
    /// if the interceptor sends a response to the client, the SendAsync() will be ignored.
    /// </summary>
    /// <param name="responseInterceptor">the response interceptor to be configured for the endpoint</param>
    public void ResponseInterceptor(IResponseInterceptor responseInterceptor) => ResponseIntrcptr = responseInterceptor;

    /// <summary>
    /// allows access if the claims principal has ANY of the given roles
    /// <para>HINT: these roles will be applied in addition to endpoint level roles if there's any</para>
    /// </summary>
    /// <param name="rolesNames">one or more roles that has access</param>
    public void Roles(params string[] rolesNames)
    {
        AllowedRoles?.AddRange(rolesNames);
        AllowedRoles ??= new(rolesNames);
    }

    /// <summary>
    /// specify an override route prefix for this endpoint if a global route prefix is enabled.
    /// this is ignored if a global route prefix is not configured.
    /// global prefix can be ignored by setting <c>string.Empty</c>
    /// <para>WARNING: setting a route prefix override globally makes the endpoint level override ineffective. i.e. RoutePrefixOverride() method call on endpoint level will be ignored.</para>
    /// </summary>
    /// <param name="routePrefix">route prefix value</param>
    public void RoutePrefixOverride(string routePrefix) => OverriddenRoutePrefix = routePrefix;

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an action that sets values of an endpoint summary object</param>
    public void Summary(Action<EndpointSummary> endpointSummary) => endpointSummary(EndpointSummary ??= new());

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an action that sets values of an endpoint summary object</param>
    public void Summary<TRequest>(Action<EndpointSummary<TRequest>> endpointSummary) where TRequest : notnull
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
    /// <para>HINT: these tags will be applied in addition to endpoint level tags if there's any</para>
    /// <para>TIP: these tags have nothing to do with swagger tags!</para>
    /// </summary>
    /// <param name="endpointTags">the tag values to associate with this endpoint</param>
    public void Tags(params string[] endpointTags)
    {
        EndpointTags?.AddRange(endpointTags);
        EndpointTags ??= new(endpointTags);
    }

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
    public void Validator<TValidator>() where TValidator : IValidator
    {
        ValidatorType = typeof(TValidator);
    }
}

/// <summary>
/// represents an enpoint version
/// </summary>
public sealed class EpVersion
{
    public int Current { get; internal set; }
    public int DeprecatedAt { get; internal set; }

    internal void Init()
    {
        if (Current == 0)
            Current = VerOpts.DefaultVersion;
    }
}

internal sealed class ServiceBoundEpProp
{
    public string PropName { get; set; }
    public Type PropType { get; set; }
    public Action<object, object>? PropSetter { get; set; }
}

internal class TypeEqualityComparer : IEqualityComparer<object>
{
    internal static readonly TypeEqualityComparer Instance = new();

    public new bool Equals(object? x, object? y) => x?.GetType() == y?.GetType();
    public int GetHashCode([DisallowNull] object obj) => obj.GetType().GetHashCode();
}
