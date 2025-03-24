using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using static FastEndpoints.Config;
using static FastEndpoints.Constants;

namespace FastEndpoints;

/// <summary>
/// represents the configuration settings of an endpoint
/// </summary>
public sealed class EndpointDefinition(Type endpointType, Type requestDtoType, Type responseDtoType)
{
    //these can only be set from internal code but accessible for user
    public Type EndpointType { get; init; } = endpointType;
    public Type? MapperType { get; internal set; }
    public IEnumerable<IPreProcessor> PreProcessorsList => PreProcessorList.Cast<IPreProcessor>();
    public IEnumerable<IPostProcessor> PostProcessorsList => PostProcessorList.Cast<IPostProcessor>();
    public Type ReqDtoType { get; init; } = requestDtoType;
    public Type ResDtoType { get; init; } = responseDtoType;
    public string[] Routes { get; internal set; } = [];
    public string SecurityPolicyName => $"epPolicy:{EndpointType.FullName}";
    public Type? ValidatorType { get; internal set; }
    public Http[] Verbs { get; internal set; } = [];
    public EpVersion Version { get; } = new();

    //these props can be changed in global config using methods below
    public bool AllowAnyPermission { get; private set; }
    public List<string>? AllowedPermissions { get; private set; }
    public bool AllowAnyClaim { get; private set; }
    public List<string>? AllowedClaimTypes { get; private set; }
    public List<string>? AllowedRoles { get; private set; }
    public Http[]? AnonymousVerbs { get; private set; }
    public bool AntiforgeryEnabled { get; private set; }
    public List<string>? AuthSchemeNames { get; private set; }
    public bool DontAutoSend { get; private set; }
    public bool DontAutoTagEndpoints { get; private set; }
    public bool DontBindFormData { get; private set; }
    public bool DoNotCatchExceptions { get; private set; }
    public object[]? EndpointAttributes { get; internal set; }
    public EndpointSummary? EndpointSummary { get; private set; }
    public List<string>? EndpointTags { get; private set; }
    public string? FormDataContentType { get; private set; }
    public IdempotencyOptions? IdempotencyOptions { get; private set; }
    public string? OverriddenRoutePrefix { get; private set; }
    public List<string>? PreBuiltUserPolicies { get; private set; }
    public Action<AuthorizationPolicyBuilder>? PolicyBuilder { get; private set; }
    public bool ThrowIfValidationFails { get; private set; } = true;

    //only accessible to internal code
    internal bool AcceptsAnyContentType;
    internal bool AcceptsMetaDataPresent;
    internal List<object>? AttribsToForward;
    internal readonly bool Disposable = endpointType.IsAssignableTo(typeof(IDisposable));
    internal readonly bool DisposableAsync = endpointType.IsAssignableTo(typeof(IAsyncDisposable));
    internal bool ExecuteAsyncImplemented;
    bool? _execReturnsIResults;
    internal bool ExecuteAsyncReturnsIResult => _execReturnsIResults ??= ResDtoType.IsAssignableTo(Types.IResult);
    internal bool FoundDuplicateValidators;
    internal HitCounter? HitCounter { get; private set; }
    internal Action<RouteHandlerBuilder> InternalConfigAction = null!;
    internal bool ImplementsConfigure;
    internal readonly List<IProcessor> PreProcessorList = [];
    internal int PreProcessorPosition;
    internal readonly List<IProcessor> PostProcessorList = [];
    internal object? RequestBinder;
    string? _reqDtoFromBodyPropName;
    internal string ReqDtoFromBodyPropName => _reqDtoFromBodyPropName ??= GetFromBodyPropName();
    ServiceBoundEpProp[]? _serviceBoundEpProps;
    internal ServiceBoundEpProp[] ServiceBoundEpProps => _serviceBoundEpProps ??= GetServiceBoundEpProps();
    internal JsonSerializerContext? SerializerContext;
    internal ResponseCacheAttribute? ResponseCacheSettings { get; private set; }
    internal IResponseInterceptor? ResponseIntrcptr { get; private set; }
    ToHeaderProp[]? _toHeaderProps;
    internal ToHeaderProp[] ToHeaderProps => _toHeaderProps ??= GetToHeaderProps();
    internal Action<RouteHandlerBuilder>? UserConfigAction { get; private set; }

    /// <summary>
    /// allow unauthenticated requests to this endpoint. optionally specify a set of verbs to allow unauthenticated access with.
    /// i.e. if the endpoint is listening to POST, PUT &amp; PATCH and you specify AllowAnonymous(Http.POST), then only PUT &amp; PATCH will require
    /// authentication.
    /// </summary>
    public void AllowAnonymous(params Http[] verbs)
    {
        AnonymousVerbs =
            verbs.Length > 0
                ? verbs
                : Enum.GetValues<Http>();
    }

    /// <summary>
    /// enable file uploads with multipart/form-data content type
    /// </summary>
    /// <param name="dontAutoBindFormData">
    /// set 'true' to disable auto binding of form data which enables uploading and reading of large files without buffering to memory/disk.
    /// you can access the multipart sections for reading via the <see cref="Endpoint{TRequest,TResponse}.FormFileSectionsAsync" /> method.
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
    public void AllowFormData(bool urlEncoded = false)
        => FormDataContentType = urlEncoded ? "application/x-www-form-urlencoded" : "multipart/form-data";

    /// <summary>
    /// specify which authentication schemes to use for authenticating requests to this endpoint
    /// <para>HINT: these auth schemes will be applied in addition to endpoint level auth schemes if there's any</para>
    /// </summary>
    /// <param name="authSchemeNames">the authentication scheme names</param>
    public void AuthSchemes(params string[] authSchemeNames)
    {
        AuthSchemeNames?.AddRange(authSchemeNames);
        AuthSchemeNames ??= [..authSchemeNames];
    }

    /// <summary>
    /// specify extra http verbs in addition to the endpoint level verbs.
    /// </summary>
    public void AdditionalVerbs(params Http[] verbs)
        => Verbs = [..Verbs, ..verbs];

    /// <summary>
    /// allows access if the claims principal has ANY of the given claim types
    /// <para>HINT: these claims will be applied in addition to endpoint level claims if there's any</para>
    /// </summary>
    /// <param name="claimTypes">the claim types</param>
    public void Claims(params string[] claimTypes)
    {
        AllowAnyClaim = true;
        AllowedClaimTypes?.AddRange(claimTypes);
        AllowedClaimTypes ??= [..claimTypes];
    }

    /// <summary>
    /// allows access if the claims principal has ALL the given claim types
    /// <para>HINT: these claims will be applied in addition to endpoint level claims if there's any</para>
    /// </summary>
    /// <param name="claimTypes">the claim types</param>
    public void ClaimsAll(params string[] claimTypes)
    {
        AllowAnyClaim = false;
        AllowedClaimTypes?.AddRange(claimTypes);
        AllowedClaimTypes ??= [..claimTypes];
    }

    static readonly Action<RouteHandlerBuilder> _clearDefaultAcceptsProducesMetadata
        = b =>
          {
              b.Add(
                  epBuilder =>
                  {
                      foreach (var m in epBuilder.Metadata.Where(o => o.GetType().Name is ProducesMetadata or AcceptsMetaData).ToArray())
                          epBuilder.Metadata.Remove(m);
                  });
          };

    /// <summary>
    /// describe openapi metadata for this endpoint. optionally specify whether you want to clear the default Accepts/Produces metadata.
    /// <para>
    /// EXAMPLE: <c>b => b.Accepts&lt;Request&gt;("text/plain")</c>
    /// </para>
    /// </summary>
    /// <param name="builder">the route handler builder for this endpoint</param>
    /// <param name="clearDefaults">set to true if the defaults should be cleared</param>
    public void Description(Action<RouteHandlerBuilder> builder, bool clearDefaults = false)
    {
        UserConfigAction = clearDefaults
                               ? _clearDefaultAcceptsProducesMetadata + builder + UserConfigAction
                               : builder + UserConfigAction;
    }

    /// <summary>
    /// disables auto sending of responses when the endpoint handler doesn't explicitly send a response. most useful for allowing a post-processor to
    /// handle sending of the response.
    /// </summary>
    public void DontAutoSendResponse()
        => DontAutoSend = true;

    /// <summary>
    /// if swagger auto tagging based on path segment is enabled, calling this method will prevent a tag from being added to this endpoint.
    /// </summary>
    public void DontAutoTag()
        => DontAutoTagEndpoints = true;

    /// <summary>
    /// use this only if you have your own exception catching middleware.
    /// if this method is called in config, an automatic error response will not be sent to the client by the library.
    /// all exceptions will be thrown, and it would be your exception catching middleware to handle them.
    /// </summary>
    public void DontCatchExceptions()
        => DoNotCatchExceptions = true;

    /// <summary>
    /// disable auto validation failure responses (400 bad request with error details) for this endpoint.
    /// <para>HINT: this only applies to request dto validation.</para>
    /// </summary>
    public void DontThrowIfValidationFails()
        => ThrowIfValidationFails = false;

    /// <summary>
    /// enable antiforgery token verification for an endpoint
    /// </summary>
    public void EnableAntiforgery()
        => AntiforgeryEnabled = true;

    /// <summary>
    /// specify the version of this endpoint.
    /// </summary>
    /// <param name="version">the version of this endpoint</param>
    /// <param name="deprecateAt">the version number starting at which this endpoint should not be included in swagger document</param>
    public EpVersion EndpointVersion(int version, int deprecateAt = 0)
    {
        Version.Current = version;
        Version.StartingReleaseVersion = version;
        Version.DeprecatedAt = deprecateAt;

        return Version;
    }

    /// <summary>
    /// if this endpoint is part of an endpoint group, specify the type of the <see cref="FastEndpoints.Group" /> concrete class where the common
    /// configuration for the group is specified.
    /// </summary>
    /// <typeparam name="TEndpointGroup">the type of your <see cref="FastEndpoints.Group" /> concrete class</typeparam>
    /// <exception cref="InvalidOperationException">thrown if endpoint route hasn't yet been specified</exception>
    public void Group<TEndpointGroup>() where TEndpointGroup : Group, new()
    {
        if (Routes.Length == 0)
        {
            throw new InvalidOperationException(
                $"Endpoint group can only be specified after the route has been configured in the [{EndpointType.FullName}] endpoint class!");
        }
        new TEndpointGroup().Action(this);
    }

    /// <summary>
    /// specify idempotency requirements for this endpoint
    /// </summary>
    /// <param name="options">the idempotency options</param>
    public void Idempotency(Action<IdempotencyOptions>? options = null)
    {
        IdempotencyOptions ??= new();
        options?.Invoke(IdempotencyOptions);
    }

    /// <summary>
    /// set endpoint configurations options using an endpoint builder action
    /// </summary>
    /// <param name="builder">the builder for this endpoint</param>
    public void Options(Action<RouteHandlerBuilder> builder)
        => UserConfigAction = builder + UserConfigAction;

    /// <summary>
    /// allows access if the claims principal has ANY of the given permissions
    /// <para>HINT: these permissions will be applied in addition to endpoint level permissions if there's any</para>
    /// </summary>
    /// <param name="permissions">the permissions</param>
    public void Permissions(params string[] permissions)
    {
        AllowAnyPermission = true;
        AllowedPermissions?.AddRange(permissions);
        AllowedPermissions ??= [..permissions];
    }

    /// <summary>
    /// allows access if the claims principal has ALL the given permissions
    /// <para>HINT: these permissions will be applied in addition to endpoint level permissions if there's any</para>
    /// </summary>
    /// <param name="permissions">the permissions</param>
    public void PermissionsAll(params string[] permissions)
    {
        AllowAnyPermission = false;
        AllowedPermissions?.AddRange(permissions);
        AllowedPermissions ??= [..permissions];
    }

    /// <summary>
    /// specify an action for building an authorization requirement which should be added to all endpoints globally.
    /// <para>HINT: these global level requirements will be combined with the requirements specified at the endpoint level if there's any.</para>
    /// </summary>
    /// <param name="policy">th policy builder action</param>
    public void Policy(Action<AuthorizationPolicyBuilder> policy)
        => PolicyBuilder = policy + PolicyBuilder;

    /// <summary>
    /// specify one or more authorization policy names you have added to the middleware pipeline during app startup/ service configuration that should be
    /// applied to this endpoint.
    /// <para>HINT: these policies will be applied in addition to endpoint level policies if there's any</para>
    /// </summary>
    /// <param name="policyNames">one or more policy names (must have been added to the pipeline on startup)</param>
    public void Policies(params string[] policyNames)
    {
        PreBuiltUserPolicies?.AddRange(policyNames);
        PreBuiltUserPolicies ??= [..policyNames];
    }

    int _postProcessorPosition;

    /// <summary>
    /// adds global post-processors to an endpoint definition which are to be executed in addition to the ones configured at the endpoint level.
    /// </summary>
    /// <param name="order">
    /// set to <see cref="Order.Before" /> if the global post-processors should be executed before endpoint post-processors.
    /// <see cref="Order.After" /> will execute global processors after endpoint level processors
    /// </param>
    /// <param name="postProcessors">the post-processors to add</param>
    public void PostProcessors(Order order, params IGlobalPostProcessor[] postProcessors)
        => AddProcessors(order, postProcessors, PostProcessorList, ref _postProcessorPosition);

    /// <summary>
    /// adds global post-processor to an endpoint definition which are to be executed in addition to the ones configured at the endpoint level.
    /// </summary>
    /// <param name="order">
    /// set to <see cref="Order.Before" /> if the global post-processors should be executed before endpoint post-processors.
    /// <see cref="Order.After" /> will execute global processors after endpoint level processors
    /// </param>
    /// <typeparam name="TPostProcessor">the post-processor to add</typeparam>
    public void PostProcessor<TPostProcessor>(Order order) where TPostProcessor : class, IGlobalPostProcessor
        => AddProcessor<TPostProcessor>(order, PostProcessorList, ref _postProcessorPosition);

    /// <summary>
    /// adds open-generic post-processors to the endpoint definition which are to be executed in addition to the ones configured at the endpoint level.
    /// </summary>
    /// <param name="order">
    /// set to <see cref="Order.Before" /> if the global pre-processors should be executed before endpoint pre-processors.
    /// <see cref="Order.After" /> will execute global processors after endpoint level processors
    /// </param>
    /// <param name="processorTypes">open generic post-processor types</param>
    /// <exception cref="InvalidOperationException">thrown if the supplied post-processor types are not open generic.</exception>
    public void PostProcessors(Order order, params Type[] processorTypes)
    {
        foreach (var tProc in processorTypes)
        {
            if (!tProc.IsGenericType ||
                tProc.GetGenericArguments().Length != 2 ||
                !tProc.GetInterfaces().Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == Types.PostProcessorOf2))
                throw new InvalidOperationException($"[{tProc.FullName}] is not a valid open-generic post-processor for registering globally!");

            var tFinal = tProc.MakeGenericType(ReqDtoType, ResDtoType);

            try
            {
                var processor = (IProcessor)Cfg.ServiceResolver.CreateSingleton(tFinal);
                AddProcessor(order, processor, PreProcessorList, ref PreProcessorPosition);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not find/construct processor type {tFinal.FullName}", ex);
            }
        }
    }

    /// <summary>
    /// adds global pre-processors to an endpoint definition which are to be executed in addition to the ones configured at the endpoint level.
    /// </summary>
    /// <param name="order">
    /// set to <see cref="Order.Before" /> if the global pre-processors should be executed before endpoint pre-processors.
    /// <see cref="Order.After" /> will execute global processors after endpoint level processors
    /// </param>
    /// <param name="preProcessors">the pre-processors to add</param>
    public void PreProcessors(Order order, params IGlobalPreProcessor[] preProcessors)
        => AddProcessors(order, preProcessors, PreProcessorList, ref PreProcessorPosition);

    /// <summary>
    /// adds global pre-processor to an endpoint definition which are to be executed in addition to the ones configured at the endpoint level.
    /// </summary>
    /// <param name="order">
    /// set to <see cref="Order.Before" /> if the global pre-processors should be executed before endpoint pre-processors.
    /// <see cref="Order.After" /> will execute global processors after endpoint level processors
    /// </param>
    /// <typeparam name="TPreProcessor">the pre-processor to add</typeparam>
    public void PreProcessor<TPreProcessor>(Order order) where TPreProcessor : class, IGlobalPreProcessor
        => AddProcessor<TPreProcessor>(order, PreProcessorList, ref PreProcessorPosition);

    /// <summary>
    /// adds open-generic pre-processors to the endpoint definition which are to be executed in addition to the ones configured at the endpoint level.
    /// </summary>
    /// <param name="order">
    /// set to <see cref="Order.Before" /> if the global pre-processors should be executed before endpoint pre-processors.
    /// <see cref="Order.After" /> will execute global processors after endpoint level processors
    /// </param>
    /// <param name="processorTypes">open generic pre-processor types</param>
    /// <exception cref="InvalidOperationException">thrown if the supplied pre-processor types are not open generic.</exception>
    public void PreProcessors(Order order, params Type[] processorTypes)
    {
        foreach (var tProc in processorTypes)
        {
            if (!tProc.IsGenericType ||
                tProc.GetGenericArguments().Length != 1 ||
                !tProc.GetInterfaces().Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == Types.PreProcessorOf1))
                throw new InvalidOperationException($"[{tProc.FullName}] is not a valid open generic pre-processor for registering globally!");

            var tFinal = tProc.MakeGenericType(ReqDtoType);

            try
            {
                var processor = (IProcessor)Cfg.ServiceResolver.CreateSingleton(tFinal);
                AddProcessor(order, processor, PreProcessorList, ref PreProcessorPosition);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not find/construct processor type {tFinal.FullName}", ex);
            }
        }
    }

    /// <summary>
    /// specify response caching settings for this endpoint
    /// </summary>
    /// <param name="durationSeconds">the duration in seconds for which the response is cached</param>
    /// <param name="location">the location where the data from a particular URL must be cached</param>
    /// <param name="noStore">specify whether the data should be stored or not</param>
    /// <param name="varyByHeader">the value for the Vary response header</param>
    /// <param name="varyByQueryKeys">the query keys to vary by</param>
    public void ResponseCache(int durationSeconds,
                              ResponseCacheLocation location = ResponseCacheLocation.Any,
                              bool noStore = false,
                              string? varyByHeader = null,
                              string[]? varyByQueryKeys = null)
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
    public void ResponseInterceptor(IResponseInterceptor responseInterceptor)
        => ResponseIntrcptr = responseInterceptor;

    /// <summary>
    /// allows access if the claims principal has ANY of the given roles
    /// <para>HINT: these roles will be applied in addition to endpoint level roles if there's any</para>
    /// </summary>
    /// <param name="rolesNames">one or more roles that has access</param>
    public void Roles(params string[] rolesNames)
    {
        AllowedRoles?.AddRange(rolesNames);
        AllowedRoles ??= [..rolesNames];
    }

    /// <summary>
    /// specify an override route prefix for this endpoint if a global route prefix is enabled.
    /// this is ignored if a global route prefix is not configured.
    /// global prefix can be ignored by setting <c>string.Empty</c>
    /// <para>
    /// WARNING: setting a route prefix override globally makes the endpoint level override ineffective. i.e. RoutePrefixOverride() method call on
    /// endpoint level will be ignored.
    /// </para>
    /// </summary>
    /// <param name="routePrefix">route prefix value</param>
    public void RoutePrefixOverride(string routePrefix)
        => OverriddenRoutePrefix = routePrefix;

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an action that sets values of an endpoint summary object</param>
    public void Summary(Action<EndpointSummary> endpointSummary)
        => endpointSummary(EndpointSummary ??= new());

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
    public void Summary(EndpointSummary endpointSummary)
        => EndpointSummary = endpointSummary;

    /// <summary>
    /// specify one or more string tags for this endpoint so they can be used in the exclusion filter during registration.
    /// <para>HINT: these tags will be applied in addition to endpoint level tags if there's any</para>
    /// <para>TIP: these tags have nothing to do with swagger tags!</para>
    /// </summary>
    /// <param name="endpointTags">the tag values to associate with this endpoint</param>
    public void Tags(params string[] endpointTags)
    {
        EndpointTags?.AddRange(endpointTags);
        EndpointTags ??= [..endpointTags];
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
    public void Throttle(int hitLimit, double durationSeconds, string? headerName = null)
        => HitCounter = new(headerName, durationSeconds, hitLimit);

    /// <summary>
    /// validator that should be used for this endpoint
    /// </summary>
    /// <typeparam name="TValidator">the type of the validator</typeparam>
    public void Validator<TValidator>() where TValidator : IValidator
        => ValidatorType = typeof(TValidator);

    internal void InitAcceptsMetaData(RouteHandlerBuilder hb)
    {
        //this work is added as a convention due to: https://github.com/FastEndpoints/FastEndpoints/issues/661
        //downside of doing this here is it's executed at startup (instead of at first request), adding a minor perf hit.
        hb.Add(
            b =>
            {
                for (var i = 0; i < b.Metadata.Count; i++)
                {
                    if (b.Metadata[i] is not IAcceptsMetadata meta)
                        continue;

                    AcceptsMetaDataPresent = true;
                    AcceptsAnyContentType = meta.ContentTypes.Contains("*/*");
                }
            });
    }

    object? _mapper;

    internal object? GetMapper()
    {
        if (_mapper is null && MapperType is not null)
            _mapper = Cfg.ServiceResolver.CreateSingleton(MapperType);

        return _mapper;
    }

    object? _validator;

    internal object? GetValidator()
    {
        if (_validator is null && ValidatorType is not null)
            _validator = Cfg.ServiceResolver.CreateSingleton(ValidatorType);

        return _validator;
    }

    internal static void AddProcessor<TProcessor>(Order order, IList<IProcessor> list, ref int pos)
    {
        try
        {
            var processor = (IProcessor)Cfg.ServiceResolver.CreateSingleton(typeof(TProcessor));
            AddProcessor(order, processor, list, ref pos);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not find/construct processor type {typeof(TProcessor).FullName}", ex);
        }
    }

    internal static void AddProcessors(Order order, IReadOnlyList<IProcessor> processors, IList<IProcessor> list, ref int pos)
    {
        for (var i = 0; i < processors.Count; i++)
            AddProcessor(order, processors[i], list, ref pos);
    }

    static void AddProcessor(Order order, IProcessor processor, IList<IProcessor> list, ref int pos)
    {
        if (list.Contains(processor, TypeEqualityComparer.Instance))
            return;

        if (order == Order.Before)
            list.Insert(pos++, processor);
        else
            list.Add(processor);
    }

    string GetFromBodyPropName()
        => $"{ReqDtoType.BindableProps().FirstOrDefault(p => p.IsDefined(Types.FromBodyAttribute))?.Name}.";

    ServiceBoundEpProp[] GetServiceBoundEpProps()
        => EndpointType.BindableProps()
                       .Select(p => new ServiceBoundEpProp(p, p.GetCustomAttribute<KeyedServiceAttribute>()?.Key))
                       .ToArray();

    ToHeaderProp[] GetToHeaderProps()
    {
        return GetProps(
            SerializerContext ?? SerOpts.Options.TypeInfoResolver,
            ResDtoType,
            SerOpts.Options);

        static ToHeaderProp[] GetProps(IJsonTypeInfoResolver? resolver, Type tResDto, JsonSerializerOptions opts)
        {
            return resolver?
                   .GetTypeInfo(tResDto, opts)?
                   .Properties.Where(p => p.AttributeProvider?.IsDefined(Types.ToHeaderAttribute, true) is true)
                   .Select(CreateProps)
                   .ToArray() ??
                   [];

            ToHeaderProp CreateProps(JsonPropertyInfo p)
                => new(
                    headerName: p.AttributeProvider?.GetCustomAttributes(Types.ToHeaderAttribute, true)
                                 .Cast<ToHeaderAttribute>()
                                 .FirstOrDefault()?.HeaderName ??
                                p.Name,
                    getter: p.Get);
        }
    }
}

/// <summary>
/// represents an endpoint version
/// </summary>
public sealed class EpVersion
{
    public int Current { get; internal set; }
    public int StartingReleaseVersion { get; internal set; }
    public int DeprecatedAt { get; internal set; }

    internal void Init()
    {
        if (Current == 0)
            Current = VerOpts.DefaultVersion;
    }

    /// <summary>
    /// specify the "release" number of the swagger document where this endpoint should start showing up in.
    /// for example, if a swagger doc such as the following is defined:
    /// <code>
    /// bld.Services.SwaggerDocument(
    ///        o =>
    ///        {
    ///            o.DocumentSettings = d => d.DocumentName = "Release 2";
    ///            o.ReleaseVersion = 2;
    ///        })
    ///  </code>
    /// this endpoint will only show up for the above doc and later if you do the following:
    /// <code>
    /// Version(n).StartingRelease(2);
    /// </code>
    /// </summary>
    /// <param name="version">the starting release version number of the swagger doc</param>
    public EpVersion StartingRelease(int version)
    {
        StartingReleaseVersion = version;

        return this;
    }

    /// <summary>
    /// specify starting at which version this endpoint should be considered deprecated.
    /// <para>
    /// NOTE: it would be the endpoint version to deprecate at for "release group" strategy, and the "release version" of the swagger doc when using the
    /// "release version" strategy.
    /// </para>
    /// </summary>
    /// <param name="version"></param>
    public EpVersion DeprecateAt(int version)
    {
        DeprecatedAt = version;

        return this;
    }
}

sealed class ServiceBoundEpProp(PropertyInfo propertyInfo, string? serviceKey)
{
    public PropertyInfo PropertyInfo { get; init; } = propertyInfo;
    public string? ServiceKey { get; init; } = serviceKey;
    public Action<object, object>? PropSetter { get; set; }
}

sealed class ToHeaderProp(string headerName, Func<object, object?>? getter)
{
    public string HeaderName { get; init; } = headerName;
    public Func<object, object?>? PropGetter { get; init; } = getter;
}

sealed class TypeEqualityComparer : IEqualityComparer<object>
{
    internal static readonly TypeEqualityComparer Instance = new();

    public new bool Equals(object? x, object? y)
        => x?.GetType() == y?.GetType();

    public int GetHashCode(object obj)
        => obj.GetType().GetHashCode();
}