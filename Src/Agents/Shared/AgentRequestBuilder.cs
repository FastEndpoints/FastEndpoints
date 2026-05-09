using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FastEndpoints.Agents;

sealed class AgentRequestBuilder
{
    static readonly ConcurrentDictionary<(Type DeclaringType, JsonSerializerOptions SerializerOptions), AgentRequestPropertySpec[]> _nestedPropertySpecs = new();

    readonly ConcurrentDictionary<(EndpointDefinition Definition, JsonSerializerOptions SerializerOptions), AgentRequestSpec> _requestSpecs = new();

    public AgentRequest Build(EndpointDefinition definition,
                              JsonElement args,
                              JsonSerializerOptions bindingSerializerOptions,
                              JsonSerializerOptions payloadSerializerOptions)
    {
        var requestSpec = _requestSpecs.GetOrAdd((definition, bindingSerializerOptions), static key => BuildRequestSpec(key.Definition, key.SerializerOptions));

        return BuildRequest(definition, args, requestSpec, bindingSerializerOptions, payloadSerializerOptions);
    }

    static AgentRequest BuildRequest(EndpointDefinition definition,
                                     JsonElement args,
                                     AgentRequestSpec requestSpec,
                                     JsonSerializerOptions bindingSerializerOptions,
                                     JsonSerializerOptions payloadSerializerOptions)
    {
        var routeParams = requestSpec.RouteParams;
        var request = new AgentRequest { Method = requestSpec.Verb };

        if (args.ValueKind != JsonValueKind.Object)
        {
            if (args.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
                request.Body = args.Clone();

            request.Path = ResolvePath(requestSpec.RouteSegments, request.RouteValues);

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

        foreach (var spec in requestSpec.Properties)
        {
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

            if (spec.QueryKind is not AgentQueryBindingKind.None)
            {
                AddQueryValues(request.Query, spec.QueryKind == AgentQueryBindingKind.Complex ? string.Empty : spec.FieldName, value, spec.PropertyType, bindingSerializerOptions);

                continue;
            }

            if (TryMatchRouteParameter(spec, matchedKey, routeParams, out routeParam))
            {
                if (TryGetSingleValue(value, out var routeValue))
                    request.RouteValues[routeParam] = routeValue;

                continue;
            }

            if (requestSpec.PreferQueryByDefault)
            {
                AddQueryValues(request.Query, spec.FieldName, value, spec.PropertyType, bindingSerializerOptions);

                continue;
            }

            bodyProps[spec.SerializedName] = JsonNode.Parse(value.GetRawText());
        }

        if (remainingArgs.Count > 0)
            throw new UnknownAgentArgumentsException(remainingArgs.Keys);

        request.Body = fromBodyPayload ?? (bodyProps.Count > 0 ? JsonSerializer.SerializeToElement(bodyProps, payloadSerializerOptions) : null);
        request.Path = ResolvePath(requestSpec.RouteSegments, request.RouteValues);

        return request;
    }

    static AgentRequestSpec BuildRequestSpec(EndpointDefinition definition, JsonSerializerOptions serializerOptions)
    {
        var verb = definition.Verbs.FirstOrDefault() ?? "POST";
        var routeTemplate = definition.Routes.FirstOrDefault();

        return new(
            verb,
            ParseRouteParameters(routeTemplate),
            BuildRouteSegments(definition, routeTemplate),
            IsQueryFirstVerb(verb),
            definition.ReqDtoType.BindableProps().Select(prop => BuildPropertySpec(prop, definition, serializerOptions)).ToArray());
    }

    static AgentRequestPropertySpec BuildPropertySpec(PropertyInfo prop, EndpointDefinition definition, JsonSerializerOptions serializerOptions)
    {
        var header = prop.GetCustomAttribute<FromHeaderAttribute>();
        var cookie = prop.GetCustomAttribute<FromCookieAttribute>();

        return BuildPropertySpec(
            prop,
            definition.ReqDtoType,
            serializerOptions,
            prop.IsDefined(Types.FromBodyAttribute),
            header is not null,
            header?.HeaderName,
            cookie is not null,
            cookie?.CookieName,
            GetQueryBindingKind(prop),
            prop.IsDefined(typeof(RouteParamAttribute), true),
            AgentInputPropertyRules.ShouldIgnoreClientInput(prop));
    }

    static AgentQueryBindingKind GetQueryBindingKind(PropertyInfo prop)
    {
        if (prop.IsDefined(typeof(FromQueryAttribute), true))
            return AgentQueryBindingKind.Complex;

        return prop.IsDefined(typeof(QueryParamAttribute), true)
                   ? AgentQueryBindingKind.Simple
                   : AgentQueryBindingKind.None;
    }

    static bool TryTakeArgValue(Dictionary<string, JsonElement> args, AgentRequestPropertySpec spec, out JsonElement value, out string matchedKey)
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

    static bool TryMatchRouteParameter(AgentRequestPropertySpec spec, string matchedKey, IReadOnlyCollection<string> routeParams, out string routeParam)
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

        var childSpecs = _nestedPropertySpecs.GetOrAdd(
            (targetType, serializerOptions),
            static key => key.DeclaringType.BindableProps().Select(prop => BuildNestedPropertySpec(prop, key.DeclaringType, key.SerializerOptions)).ToArray());
        var childArgs = value.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase);

        foreach (var childSpec in childSpecs)
        {
            if (!TryTakeArgValue(childArgs, childSpec, out var childValue, out _))
                continue;

            AddQueryValues(query, CombineQueryKey(key, childSpec.FieldName), childValue, childSpec.PropertyType, serializerOptions);
        }

        foreach (var (childKey, childValue) in childArgs)
            AddQueryValues(query, CombineQueryKey(key, childKey), childValue, typeof(object), serializerOptions);
    }

    static string CombineQueryKey(string prefix, string key)
        => string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

    static AgentRequestPropertySpec BuildNestedPropertySpec(PropertyInfo prop, Type declaringType, JsonSerializerOptions serializerOptions)
        => BuildPropertySpec(prop, declaringType, serializerOptions, false, false, null, false, null, AgentQueryBindingKind.None, false, false);

    static AgentRequestPropertySpec BuildPropertySpec(PropertyInfo prop,
                                                      Type declaringType,
                                                      JsonSerializerOptions serializerOptions,
                                                      bool fromBody,
                                                      bool hasHeaderBinding,
                                                      string? headerName,
                                                      bool hasCookieBinding,
                                                      string? cookieName,
                                                      AgentQueryBindingKind queryKind,
                                                      bool routeOnly,
                                                      bool ignoreInput)
    {
        var fieldName = prop.FieldName();
        var serializedName = AgentJsonPropertyNames.GetSerializedName(prop, declaringType, serializerOptions) ?? prop.Name;
        var aliases = new[] { serializedName, prop.Name, fieldName }
                      .Where(n => !string.IsNullOrWhiteSpace(n))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToArray();

        return new(
            aliases,
            prop.PropertyType,
            fieldName,
            serializedName,
            fromBody,
            hasHeaderBinding,
            headerName ?? fieldName,
            hasCookieBinding,
            cookieName ?? fieldName,
            queryKind,
            routeOnly,
            ignoreInput,
            fieldName);
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

    static string[] BuildRouteSegments(EndpointDefinition definition, string? routeTemplate)
    {
        if (string.IsNullOrWhiteSpace(routeTemplate))
            return [];

        routeTemplate = new StringBuilder().BuildRoute(definition.Version.Current, routeTemplate, definition.OverriddenRoutePrefix);

        return routeTemplate.TrimStart('~').Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    static PathString ResolvePath(string[] routeSegments, IReadOnlyDictionary<string, object?> routeValues)
    {
        if (routeSegments.Length == 0)
            return "/";

        var parts = new List<string>(routeSegments.Length);

        foreach (var segment in routeSegments)
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

sealed record AgentRequestSpec(string Verb,
                               IReadOnlyList<string> RouteParams,
                               string[] RouteSegments,
                               bool PreferQueryByDefault,
                               AgentRequestPropertySpec[] Properties);

sealed record AgentRequestPropertySpec(string[] Aliases,
                                       Type PropertyType,
                                       string FieldName,
                                       string SerializedName,
                                       bool FromBody,
                                       bool HasHeaderBinding,
                                       string HeaderName,
                                       bool HasCookieBinding,
                                       string CookieName,
                                       AgentQueryBindingKind QueryKind,
                                       bool RouteOnly,
                                       bool IgnoreInput,
                                       string RouteKey);

enum AgentQueryBindingKind
{
    None,
    Simple,
    Complex
}
