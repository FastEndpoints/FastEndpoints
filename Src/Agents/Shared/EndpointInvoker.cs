using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using static FastEndpoints.Config;

namespace FastEndpoints.Agents;

/// <summary>
/// invokes a single <see cref="EndpointDefinition" /> in-process, bypassing HTTP routing. the full
/// FastEndpoints pipeline — binder, validator, pre-/post-processors, <c>ExecuteAsync</c>/<c>HandleAsync</c> —
/// runs exactly as it would for a real request. this is the engine shared by <c>FastEndpoints.Mcp</c>
/// and <c>FastEndpoints.A2A</c>.
/// </summary>
sealed class EndpointInvoker(IServiceScopeFactory scopeFactory)
{
    /// <summary>
    /// invokes <paramref name="definition" /> with <paramref name="args" /> as the request body. the body
    /// is fed to the FastEndpoints binder via a <see cref="DefaultHttpContext" /> whose <c>Request.Body</c>
    /// is a <see cref="MemoryStream" /> containing the JSON payload, so <c>[FromBody]</c>, <c>[FromRoute]</c>,
    /// etc. all resolve naturally when the argument JSON uses the DTO's property names.
    /// </summary>
    /// <param name="definition">the endpoint definition (typically sourced from <c>EndpointData.Found[]</c>).</param>
    /// <param name="args">the raw arguments, treated as a JSON object representing the request dto.</param>
    /// <param name="principal">optional caller identity propagated to <c>HttpContext.User</c>.</param>
    /// <param name="ct">cancellation token forwarded to the endpoint pipeline.</param>
    public async Task<InvocationResult> InvokeAsync(EndpointDefinition definition, JsonElement args, System.Security.Claims.ClaimsPrincipal? principal, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        DefaultHttpContext httpContext;

        try
        {
            httpContext = BuildHttpContext(definition, args, principal, scope.ServiceProvider, ct);
        }
        catch (UnknownAgentArgumentsException ex)
        {
            return InvocationResult.Invalid(ex.Failures);
        }

        var accessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();
        var resolverAccessor = FastEndpoints.HttpContextExtensions.TryResolve<IHttpContextAccessor>(httpContext);
        var previousContext = accessor?.HttpContext;
        var previousResolverContext = resolverAccessor?.HttpContext;
        BaseEndpoint? endpoint = null;

        try
        {
            if (accessor is not null)
                accessor.HttpContext = httpContext;
            if (resolverAccessor is not null && !ReferenceEquals(resolverAccessor, accessor))
                resolverAccessor.HttpContext = httpContext;

            endpoint = EndpointBootstrap.CreateEndpoint(httpContext, definition);

            await endpoint.ExecAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return InvocationResult.Faulted(ex, endpoint?.ValidationFailures ?? []);
        }
        finally
        {
            if (accessor is not null)
                accessor.HttpContext = previousContext;
            if (resolverAccessor is not null && !ReferenceEquals(resolverAccessor, accessor))
                resolverAccessor.HttpContext = previousResolverContext;

            if (endpoint is not null && definition.DisposableAsync)
                await ((IAsyncDisposable)endpoint).DisposeAsync();
            if (endpoint is not null && definition.Disposable)
                ((IDisposable)endpoint).Dispose();
        }

        if (endpoint is null)
            throw new InvalidOperationException("Endpoint invocation completed without creating an endpoint instance.");

        // report validation failures regardless of response status: an endpoint that opts into
        // DontThrowIfValidationFails() leaves StatusCode = 200 even when AddError(...) pushed
        // failures onto the collection. gating on StatusCode >= 400 would silently swallow those
        // and hand the agent a "success" response containing failure information it can't see.
        if (endpoint.ValidationFailures.Count > 0)
            return InvocationResult.Invalid(endpoint.ValidationFailures);

        var body = (MemoryStream)httpContext.Response.Body;
        body.Position = 0;
        var payload = body.ToArray();

        return httpContext.Response.StatusCode is >= 200 and < 300
                   ? InvocationResult.Ok(httpContext.Response.StatusCode, httpContext.Response.ContentType, payload)
                   : InvocationResult.HttpError(httpContext.Response.StatusCode, httpContext.Response.ContentType, payload);
    }

    static DefaultHttpContext BuildHttpContext(EndpointDefinition definition,
                                               JsonElement args,
                                               System.Security.Claims.ClaimsPrincipal? principal,
                                               IServiceProvider services,
                                               CancellationToken ct)
    {
        var bindingSerializerOptions = definition.SerializerContext?.Options ?? SerOpts.Options;
        var request = BuildRequest(definition, args, bindingSerializerOptions, SerOpts.Options);
        var ctx = new DefaultHttpContext { RequestServices = services };

        if (principal is not null)
            ctx.User = principal;

        var requestBody = new MemoryStream();

        if (request.Body is { } body)
        {
            using var writer = new Utf8JsonWriter(requestBody);
            body.WriteTo(writer);
            writer.Flush();
            ctx.Request.ContentType = "application/json";
        }

        requestBody.Position = 0;

        ctx.Request.Body = requestBody;
        ctx.Request.ContentLength = requestBody.Length;
        ctx.RequestAborted = ct;
        ctx.Request.Method = request.Method;
        ctx.Request.Path = request.Path;
        var routeValues = new RouteValueDictionary(request.RouteValues);
        ctx.Features.Set<IRouteValuesFeature>(new RouteValuesFeature { RouteValues = routeValues });
        ctx.Request.RouteValues = routeValues;

        if (request.Query.Count > 0)
        {
            ctx.Request.QueryString = QueryString.Create(request.Query);
            ctx.Request.Query = new QueryCollection(
                request.Query.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                       .ToDictionary(g => g.Key, g => new StringValues(g.Select(x => x.Value).ToArray()), StringComparer.OrdinalIgnoreCase));
        }

        foreach (var (key, value) in request.Headers)
            ctx.Request.Headers[key] = value;

        if (request.Cookies.Count > 0)
            ctx.Request.Headers.Cookie = string.Join("; ", request.Cookies.Select(c => $"{Uri.EscapeDataString(c.Key)}={Uri.EscapeDataString(c.Value ?? string.Empty)}"));

        ctx.Response.Body = new MemoryStream();

        var endpointFeature = new AgentEndpointFeature(definition);
        ctx.Features.Set<IEndpointFeature>(endpointFeature);

        return ctx;
    }

    static AgentRequest BuildRequest(EndpointDefinition definition,
                                     JsonElement args,
                                     JsonSerializerOptions bindingSerializerOptions,
                                     JsonSerializerOptions payloadSerializerOptions)
    {
        var verb = definition.Verbs.FirstOrDefault() ?? "POST";
        var routeTemplate = definition.Routes.FirstOrDefault();
        var routeParams = ParseRouteParameters(routeTemplate);
        var request = new AgentRequest { Method = verb };
        var preferQueryByDefault = IsQueryFirstVerb(verb);

        if (args.ValueKind != JsonValueKind.Object)
        {
            if (args.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
                request.Body = args.Clone();

            request.Path = ResolvePath(definition, routeTemplate, request.RouteValues);

            return request;
        }

        var remainingArgs = args.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        var bodyProps = new JsonObject();
        JsonElement? fromBodyPayload = null;

        foreach (var routeParam in routeParams)
        {
            if (!remainingArgs.TryGetValue(routeParam, out var value))
                continue;

            remainingArgs.Remove(routeParam);

            if (TryGetSingleValue(value, out var routeValue))
                request.RouteValues[routeParam] = routeValue;
        }

        foreach (var prop in definition.ReqDtoType.BindableProps())
        {
            var spec = BuildPropertySpec(prop, definition, bindingSerializerOptions);

            if (!TryTakeArgValue(remainingArgs, spec, out var value, out var matchedKey))
                continue;

            if (spec.IgnoreInput)
                continue;

            if (spec.FromBody)
            {
                fromBodyPayload = value;

                continue;
            }

            if (spec.HasHeaderBinding)
            {
                request.Headers[spec.HeaderName] = ToStringValues(value);

                continue;
            }

            if (spec.HasCookieBinding)
            {
                if (TryGetSingleValue(value, out var cookieValue))
                    request.Cookies[spec.CookieName] = cookieValue;

                continue;
            }

            if (spec.RouteOnly && TryMatchRouteParameter(spec, matchedKey, routeParams, out var routeParam))
            {
                if (TryGetSingleValue(value, out var routeValue))
                    request.RouteValues[routeParam] = routeValue;

                continue;
            }

            if (spec.QueryKind is not QueryBindingKind.None)
            {
                AddQueryValues(request.Query, spec.QueryKind == QueryBindingKind.Complex ? string.Empty : spec.FieldName, value, prop.PropertyType, bindingSerializerOptions);

                continue;
            }

            if (TryMatchRouteParameter(spec, matchedKey, routeParams, out routeParam))
            {
                if (TryGetSingleValue(value, out var routeValue))
                    request.RouteValues[routeParam] = routeValue;

                continue;
            }

            if (preferQueryByDefault)
            {
                AddQueryValues(request.Query, spec.FieldName, value, prop.PropertyType, bindingSerializerOptions);

                continue;
            }

            bodyProps[spec.SerializedName] = JsonNode.Parse(value.GetRawText());
        }

        if (remainingArgs.Count > 0)
            throw new UnknownAgentArgumentsException(remainingArgs.Keys);

        request.Body = fromBodyPayload ?? (bodyProps.Count > 0 ? JsonSerializer.SerializeToElement(bodyProps, payloadSerializerOptions) : null);
        request.Path = ResolvePath(definition, routeTemplate, request.RouteValues);

        return request;
    }

    static PropertySpec BuildPropertySpec(PropertyInfo prop, EndpointDefinition definition, JsonSerializerOptions serializerOptions)
    {
        var fieldName = prop.FieldName();
        var serializedName = GetSerializedPropertyName(prop, definition, serializerOptions) ?? prop.Name;
        var header = prop.GetCustomAttribute<FromHeaderAttribute>();
        var cookie = prop.GetCustomAttribute<FromCookieAttribute>();
        var fromClaim = prop.GetCustomAttribute<FromClaimAttribute>();
        var hasPermission = prop.GetCustomAttribute<HasPermissionAttribute>();
        var aliases = new[] { serializedName, prop.Name, fieldName }
                      .Where(n => !string.IsNullOrWhiteSpace(n))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToArray();

        return new(
            aliases,
            fieldName,
            serializedName,
            prop.IsDefined(Types.FromBodyAttribute),
            header is not null,
            header?.HeaderName ?? fieldName,
            cookie is not null,
            cookie?.CookieName ?? fieldName,
            GetQueryBindingKind(prop),
            prop.IsDefined(typeof(RouteParamAttribute), true),
            fromClaim?.IsRequired is true ||
            fromClaim?.RemoveFromSchema is true ||
            hasPermission is not null ||
            header?.RemoveFromSchema is true ||
            cookie?.RemoveFromSchema is true,
            fieldName);
    }

    static string? GetSerializedPropertyName(PropertyInfo prop, EndpointDefinition definition, JsonSerializerOptions serializerOptions)
    {
        var typeInfo = (definition.SerializerContext ?? serializerOptions.TypeInfoResolver)?.GetTypeInfo(definition.ReqDtoType, serializerOptions);

        if (typeInfo is null)
            return null;

        foreach (var jsonProp in typeInfo.Properties)
        {
            if (ReferenceEquals(jsonProp.AttributeProvider, prop))
                return jsonProp.Name;
        }

        return null;
    }

    static QueryBindingKind GetQueryBindingKind(PropertyInfo prop)
    {
        if (prop.IsDefined(typeof(FromQueryAttribute), true))
            return QueryBindingKind.Complex;

        return prop.IsDefined(typeof(QueryParamAttribute), true)
                   ? QueryBindingKind.Simple
                   : QueryBindingKind.None;
    }

    static bool TryTakeArgValue(Dictionary<string, JsonElement> args, PropertySpec spec, out JsonElement value, out string matchedKey)
    {
        foreach (var alias in spec.Aliases)
        {
            if (!args.TryGetValue(alias, out value))
                continue;

            matchedKey = alias;
            args.Remove(alias);

            return true;
        }

        value = default;
        matchedKey = string.Empty;

        return false;
    }

    static bool TryMatchRouteParameter(PropertySpec spec, string matchedKey, IReadOnlyCollection<string> routeParams, out string routeParam)
    {
        foreach (var param in routeParams)
        {
            if (spec.Aliases.Contains(param, StringComparer.OrdinalIgnoreCase) || string.Equals(matchedKey, param, StringComparison.OrdinalIgnoreCase))
            {
                routeParam = param;

                return true;
            }
        }

        routeParam = spec.RouteOnly ? spec.RouteKey : string.Empty;

        return spec.RouteOnly;
    }

    static void AddQueryValues(List<KeyValuePair<string, string?>> query,
                               string key,
                               JsonElement value,
                               Type targetType,
                               JsonSerializerOptions serializerOptions)
    {
        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return;

        if (targetType.IsCollection() && targetType != Types.String)
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                query.Add(new(key, GetScalarValue(value)));

                return;
            }

            var elementType = targetType.IsArray
                                  ? targetType.GetElementType() ?? typeof(object)
                                  : targetType.GetGenericArguments().FirstOrDefault() ?? typeof(object);
            var index = 0;

            foreach (var item in value.EnumerateArray())
            {
                if (elementType.IsComplexType() && !elementType.IsCollection())
                    AddQueryValues(query, $"{key}[{index++}]", item, elementType, serializerOptions);
                else
                    query.Add(new(key, GetScalarValue(item)));
            }

            return;
        }

        if (!targetType.IsComplexType() || value.ValueKind != JsonValueKind.Object)
        {
            query.Add(new(key, GetScalarValue(value)));

            return;
        }

        var childProps = targetType.BindableProps();
        var childArgs = value.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase);

        foreach (var childProp in childProps)
        {
            var childSpec = BuildNestedPropertySpec(childProp, targetType, serializerOptions);

            if (!TryTakeArgValue(childArgs, childSpec, out var childValue, out _))
                continue;

            AddQueryValues(query, CombineQueryKey(key, childSpec.FieldName), childValue, childProp.PropertyType, serializerOptions);
        }

        foreach (var (childKey, childValue) in childArgs)
            AddQueryValues(query, CombineQueryKey(key, childKey), childValue, typeof(object), serializerOptions);
    }

    static string CombineQueryKey(string prefix, string key)
        => string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

    static PropertySpec BuildNestedPropertySpec(PropertyInfo prop, Type declaringType, JsonSerializerOptions serializerOptions)
    {
        var fieldName = prop.FieldName();
        var serializedName = GetSerializedPropertyName(prop, declaringType, serializerOptions) ?? prop.Name;

        return new(
            [serializedName, prop.Name, fieldName],
            fieldName,
            serializedName,
            false,
            false,
            fieldName,
            false,
            fieldName,
            QueryBindingKind.None,
            false,
            false,
            fieldName);
    }

    static string? GetSerializedPropertyName(PropertyInfo prop, Type declaringType, JsonSerializerOptions serializerOptions)
    {
        var typeInfo = serializerOptions.TypeInfoResolver?.GetTypeInfo(declaringType, serializerOptions);

        if (typeInfo is null)
            return null;

        foreach (var jsonProp in typeInfo.Properties)
        {
            if (ReferenceEquals(jsonProp.AttributeProvider, prop))
                return jsonProp.Name;
        }

        return null;
    }

    static StringValues ToStringValues(JsonElement value)
        => value.ValueKind == JsonValueKind.Array
               ? value.EnumerateArray().Select(GetScalarValue).ToArray()
               : new StringValues(GetScalarValue(value));

    static bool TryGetSingleValue(JsonElement value, out string? result)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            result = value.GetArrayLength() > 0 ? GetScalarValue(value.EnumerateArray().First()) : null;

            return result is not null;
        }

        result = GetScalarValue(value);

        return result is not null;
    }

    static string? GetScalarValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => value.GetRawText()
        };

    static bool IsQueryFirstVerb(string verb)
        => verb is "GET" or "HEAD" or "DELETE";

    static List<string> ParseRouteParameters(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return [];

        var parameters = new List<string>();
        var start = 0;

        while ((start = route.IndexOf('{', start)) >= 0)
        {
            var end = route.IndexOf('}', start + 1);

            if (end < 0)
                break;

            parameters.Add(NormalizeRouteParameter(route[(start + 1)..end]));
            start = end + 1;
        }

        return parameters;
    }

    static string NormalizeRouteParameter(string segment)
    {
        var colonIdx = segment.IndexOf(':');
        var equalsIdx = segment.IndexOf('=');
        var splitIdx = colonIdx >= 0 && equalsIdx >= 0
                           ? Math.Min(colonIdx, equalsIdx)
                           : Math.Max(colonIdx, equalsIdx);
        var name = splitIdx >= 0 ? segment[..splitIdx] : segment;

        return name.TrimStart('*').TrimEnd('?');
    }

    static PathString ResolvePath(EndpointDefinition definition, string? routeTemplate, IReadOnlyDictionary<string, object?> routeValues)
    {
        if (string.IsNullOrWhiteSpace(routeTemplate))
            return "/";

        routeTemplate = new StringBuilder().BuildRoute(definition.Version.Current, routeTemplate, definition.OverriddenRoutePrefix);

        var segments = routeTemplate.TrimStart('~').Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            var resolved = ResolvePathSegment(segment, routeValues);

            if (resolved.Length > 0)
                parts.Add(resolved);
        }

        return new("/" + string.Join('/', parts));
    }

    static string ResolvePathSegment(string segment, IReadOnlyDictionary<string, object?> routeValues)
    {
        var builder = new StringBuilder(segment.Length);
        var start = 0;

        while (start < segment.Length)
        {
            var open = segment.IndexOf('{', start);

            if (open < 0)
            {
                builder.Append(segment, start, segment.Length - start);

                break;
            }

            builder.Append(segment, start, open - start);

            var close = segment.IndexOf('}', open + 1);

            if (close < 0)
            {
                builder.Append(segment, open, segment.Length - open);

                break;
            }

            var inner = segment[(open + 1)..close];
            var routeParam = NormalizeRouteParameter(inner);
            var optional = inner.TrimStart('*').Split(':', '=')[0].EndsWith('?');

            if (routeValues.TryGetValue(routeParam, out var value) && value is not null)
                builder.Append(Uri.EscapeDataString(value.ToString()!));
            else if (!optional)
                builder.Append(routeParam);

            start = close + 1;
        }

        return builder.ToString();
    }

    sealed class AgentRequest
    {
        public string Method { get; set; } = "POST";
        public PathString Path { get; set; } = "/";
        public JsonElement? Body { get; set; }
        public Dictionary<string, object?> RouteValues { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<KeyValuePair<string, string?>> Query { get; } = [];
        public Dictionary<string, StringValues> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string?> Cookies { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    sealed class UnknownAgentArgumentsException(IEnumerable<string> names) : Exception("Agent invocation contained unknown arguments.")
    {
        public IReadOnlyList<ValidationFailure> Failures { get; } = names.Select(name => new ValidationFailure(name, $"Unknown argument '{name}'.")).ToArray();
    }

    sealed record PropertySpec(string[] Aliases,
                               string FieldName,
                               string SerializedName,
                               bool FromBody,
                               bool HasHeaderBinding,
                               string HeaderName,
                               bool HasCookieBinding,
                               string CookieName,
                               QueryBindingKind QueryKind,
                               bool RouteOnly,
                               bool IgnoreInput,
                               string RouteKey);

    enum QueryBindingKind
    {
        None,
        Simple,
        Complex
    }

    sealed class AgentEndpointFeature : IEndpointFeature
    {
        public AgentEndpointFeature(EndpointDefinition definition)
        {
            var metadata = new EndpointMetadataCollection(definition);
            Endpoint = new(_ => Task.CompletedTask, metadata, definition.EndpointType.FullName);
        }

        public Endpoint? Endpoint { get; set; }
    }

    sealed class RouteValuesFeature : IRouteValuesFeature
    {
        public RouteValueDictionary RouteValues { get; set; } = [];
    }
}

/// <summary>outcome of <see cref="EndpointInvoker.InvokeAsync" />.</summary>
readonly struct InvocationResult
{
    public InvocationStatus Status { get; }
    public int HttpStatusCode { get; }
    public string? ContentType { get; }
    public byte[] Body { get; }
    public Exception? Exception { get; }
    public IReadOnlyList<ValidationFailure> ValidationFailures { get; }

    InvocationResult(InvocationStatus status, int httpStatusCode, string? contentType, byte[] body, Exception? ex, IReadOnlyList<ValidationFailure> failures)
    {
        Status = status;
        HttpStatusCode = httpStatusCode;
        ContentType = contentType;
        Body = body;
        Exception = ex;
        ValidationFailures = failures;
    }

    public static InvocationResult Ok(int statusCode, string? contentType, byte[] body)
        => new(InvocationStatus.Success, statusCode, contentType, body, null, []);

    public static InvocationResult HttpError(int statusCode, string? contentType, byte[] body)
        => new(InvocationStatus.HttpError, statusCode, contentType, body, null, []);

    public static InvocationResult Invalid(IReadOnlyList<ValidationFailure> failures)
        => new(InvocationStatus.ValidationFailed, 400, null, [], null, failures);

    public static InvocationResult Faulted(Exception ex, IReadOnlyList<ValidationFailure> failures)
        => new(InvocationStatus.Faulted, 500, null, [], ex, failures);
}

enum InvocationStatus
{
    Success,
    HttpError,
    ValidationFailed,
    Faulted
}
